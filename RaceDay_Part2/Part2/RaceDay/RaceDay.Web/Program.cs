using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// MVC session: holds the logged-in user and the API's session cookie across all pages.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.Name = ".RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Typed HttpClient for the Part 2 API. The MVC app never touches the DB or Azure directly.
var apiBase = builder.Configuration["ApiSettings:BaseUrl"]
              ?? throw new InvalidOperationException("ApiSettings:BaseUrl is not configured.");
var allowInvalidCert = builder.Configuration.GetValue<bool>("ApiSettings:AllowInvalidCertificate");

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiBase.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        // UseCookies = false is essential: the handler is shared between users, so cookies are
        // kept per user in their MVC session by ApiClient instead of in a shared cookie jar.
        var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false };
        // Only for local Docker demos against the API's self-signed dev certificate.
        if (allowInvalidCert)
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        return handler;
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
