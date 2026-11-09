using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RaceDay.Web.Services;

public class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public HttpStatusCode? StatusCode { get; init; }
}

public interface IApiClient
{
    Task<ApiResult<T>> GetAsync<T>(string url);
    Task<ApiResult<T>> PostAsync<T>(string url, object? body);
    Task<ApiResult<T>> PutAsync<T>(string url, object? body);
    Task<ApiResult<bool>> DeleteAsync(string url);
    Task<ApiResult<T>> UploadAsync<T>(string url, IFormFile file, string fieldName = "file");
}

/// <summary>
/// The only gateway from the MVC app to the Part 2 API.
/// The API identifies users with a session cookie, so this class keeps each user's API cookies in
/// their own MVC session and sends them on every call. Failures become friendly messages.
/// </summary>
public class ApiClient : IApiClient
{
    private const string CookieKey = "RaceDay.ApiCookies";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _ctx;
    private readonly ILogger<ApiClient> _log;

    public ApiClient(HttpClient http, IHttpContextAccessor ctx, ILogger<ApiClient> log)
    {
        _http = http;
        _ctx = ctx;
        _log = log;
    }

    public Task<ApiResult<T>> GetAsync<T>(string url) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, url));

    public Task<ApiResult<T>> PostAsync<T>(string url, object? body) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, url) { Content = ToJson(body) });

    public Task<ApiResult<T>> PutAsync<T>(string url, object? body) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Put, url) { Content = ToJson(body) });

    public async Task<ApiResult<bool>> DeleteAsync(string url)
    {
        var r = await SendAsync<object>(new HttpRequestMessage(HttpMethod.Delete, url));
        return new ApiResult<bool> { Success = r.Success, Data = r.Success, Error = r.Error, StatusCode = r.StatusCode };
    }

    public async Task<ApiResult<T>> UploadAsync<T>(string url, IFormFile file, string fieldName = "file")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(fileContent, fieldName, file.FileName);
        return await SendAsync<T>(new HttpRequestMessage(HttpMethod.Post, url) { Content = content });
    }

    private static StringContent? ToJson(object? body) =>
        body == null ? null : new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request)
    {
        using (request)
        {
            try
            {
                var jar = LoadCookies();
                if (jar.Count > 0)
                    request.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", jar.Select(kv => $"{kv.Key}={kv.Value}")));

                using var response = await _http.SendAsync(request);
                StoreResponseCookies(response, jar);
                var text = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("API {Method} {Url} returned {Status}", request.Method, request.RequestUri, (int)response.StatusCode);

                    // A 401 on a normal call means the API session ended: drop our copy too.
                    if (response.StatusCode == HttpStatusCode.Unauthorized && jar.Count > 0 && !IsAuthCall(request))
                    {
                        _ctx.HttpContext?.Session.Clear();
                        return Fail<T>(response.StatusCode, "Your session has expired. Please log in again.");
                    }

                    return Fail<T>(response.StatusCode, FriendlyError(response.StatusCode, text));
                }

                if (string.IsNullOrWhiteSpace(text) || typeof(T) == typeof(object))
                    return new ApiResult<T> { Success = true, StatusCode = response.StatusCode };

                var data = JsonSerializer.Deserialize<T>(text, Json);
                return new ApiResult<T> { Success = true, Data = data, StatusCode = response.StatusCode };
            }
            catch (TaskCanceledException)
            {
                return new ApiResult<T> { Success = false, Error = "The server took too long to respond. Please try again." };
            }
            catch (HttpRequestException ex)
            {
                _log.LogError(ex, "Could not reach API");
                return new ApiResult<T> { Success = false, Error = "We couldn't reach the RaceDay service. Please try again shortly." };
            }
            catch (JsonException ex)
            {
                _log.LogError(ex, "Unexpected API response format");
                return new ApiResult<T> { Success = false, Error = "We received an unexpected response. Please try again." };
            }
        }
    }

    private static ApiResult<T> Fail<T>(HttpStatusCode status, string message) =>
        new() { Success = false, StatusCode = status, Error = message };

    private static bool IsAuthCall(HttpRequestMessage r)
    {
        var path = r.RequestUri?.ToString() ?? "";
        return path.EndsWith("auth/login", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("auth/register", StringComparison.OrdinalIgnoreCase);
    }

    // ---- per-user API cookie jar (kept in the MVC session) ----
    private Dictionary<string, string> LoadCookies()
    {
        var raw = _ctx.HttpContext?.Session.GetString(CookieKey);
        if (string.IsNullOrEmpty(raw)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private void StoreResponseCookies(HttpResponseMessage response, Dictionary<string, string> jar)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;

        foreach (var header in values)
        {
            var pair = header.Split(';')[0];
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var name = pair[..eq].Trim();
            var value = pair[(eq + 1)..].Trim();
            if (value.Length == 0) jar.Remove(name); else jar[name] = value;
        }
        _ctx.HttpContext?.Session.SetString(CookieKey, JsonSerializer.Serialize(jar));
    }

    private static string FriendlyError(HttpStatusCode status, string body)
    {
        // The API returns ProblemDetails / ValidationProblemDetails. Use a short, readable message from it.
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object && status != HttpStatusCode.InternalServerError)
            {
                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in errors.EnumerateObject())
                        foreach (var msg in field.Value.EnumerateArray())
                            if (msg.ValueKind == JsonValueKind.String && msg.GetString() is { Length: > 0 and < 200 } text)
                                return text;
                }
                if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String
                    && detail.GetString() is { Length: > 0 and < 200 } d)
                    return d;
            }
        }
        catch (JsonException) { /* not JSON, fall through */ }

        return status switch
        {
            HttpStatusCode.BadRequest => "Some of the information you entered isn't valid. Please check and try again.",
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.NotFound => "We couldn't find what you were looking for.",
            HttpStatusCode.Conflict => "That action conflicts with existing data. Please check and try again.",
            _ => "Something went wrong on our side. Please try again."
        };
    }
}
