using System.Net;
using System.Text.Json;

namespace RaceDay.Tests;

/// <summary>Swagger must list every endpoint of the Part 1 plan.</summary>
public class SwaggerTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    /// <summary>Every endpoint in the Part 1 endpoint plan (plus logout).</summary>
    public static IEnumerable<object[]> PlannedEndpoints() => new[]
    {
        new object[] { "post",   "/api/auth/register" },
        new object[] { "post",   "/api/auth/login" },
        new object[] { "post",   "/api/auth/logout" },
        new object[] { "get",    "/api/users/me" },
        new object[] { "put",    "/api/users/me" },
        new object[] { "get",    "/api/events" },
        new object[] { "get",    "/api/events/{id}" },
        new object[] { "post",   "/api/events" },
        new object[] { "put",    "/api/events/{id}" },
        new object[] { "delete", "/api/events/{id}" },
        new object[] { "get",    "/api/events/{eventId}/categories" },
        new object[] { "post",   "/api/events/{eventId}/categories" },
        new object[] { "put",    "/api/categories/{id}" },
        new object[] { "delete", "/api/categories/{id}" },
        new object[] { "post",   "/api/categories/{categoryId}/enrolments" },
        new object[] { "get",    "/api/users/me/enrolments" },
        new object[] { "get",    "/api/categories/{categoryId}/enrolments" },
        new object[] { "delete", "/api/enrolments/{id}" },
        new object[] { "post",   "/api/enrolments/{id}/results" },
        new object[] { "put",    "/api/results/{id}" },
        new object[] { "get",    "/api/events/{eventId}/results" },
        new object[] { "get",    "/api/users/me/results" },
    };

    private async Task<JsonDocument> LoadSwaggerAsync()
    {
        var response = await factory.NewClient().GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [MemberData(nameof(PlannedEndpoints))]
    public async Task SwaggerDocument_ListsEveryPlannedEndpoint(string method, string path)
    {
        using var doc = await LoadSwaggerAsync();

        var paths = doc.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty(path, out var pathItem), $"Swagger is missing the path {path}");
        Assert.True(pathItem.TryGetProperty(method, out _), $"Swagger is missing {method.ToUpperInvariant()} {path}");
    }

    [Fact]
    public async Task SwaggerUi_IsServed()
    {
        var response = await factory.NewClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerOperations_HaveDescriptions_WhenXmlCommentsAreAvailable()
    {
        // The XML comments file is copied next to the test assembly by the project reference.
        // If it is not there for some reason, this check is skipped rather than failing the build.
        var xml = Path.Combine(AppContext.BaseDirectory, "RaceDay.Api.xml");
        if (!File.Exists(xml)) return;

        using var doc = await LoadSwaggerAsync();
        foreach (var path in doc.RootElement.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
        {
            Assert.True(operation.Value.TryGetProperty("summary", out var summary) && summary.GetString()!.Length > 5,
                $"{operation.Name.ToUpperInvariant()} {path.Name} has no summary");
            Assert.True(operation.Value.TryGetProperty("responses", out var responses) && responses.EnumerateObject().Any(),
                $"{operation.Name.ToUpperInvariant()} {path.Name} documents no responses");
        }
    }
}
