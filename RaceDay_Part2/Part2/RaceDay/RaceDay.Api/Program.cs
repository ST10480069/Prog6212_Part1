using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// ---- Database (EF Core + SQL Server) -------------------------------------------------
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();

// ---- Session: remembers who is logged in and which role they have ----------------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ---- Swagger -------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description =
            "REST API for the RaceDay event system. Two roles: **Organiser** (manages events, categories and results) " +
            "and **Participant** (enrols in events and views results).\n\n" +
            "**How to test in Swagger:** call `POST /api/auth/login` first. The session cookie it sets is sent " +
            "automatically with every later request, so you can then try the protected endpoints. " +
            "Use `POST /api/auth/logout` to switch user."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ---- Create / update the database on start-up ------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();

    if (db.Database.IsRelational() && db.Database.GetMigrations().Any())
        await db.Database.MigrateAsync();       // SQL Server: apply the EF migrations (Add-Migration InitialCreate)
    else
        await db.Database.EnsureCreatedAsync(); // fallback + the in-memory database used by the unit tests

    if (app.Environment.IsDevelopment())
        await DbSeeder.SeedAsync(db);           // sample data from the Part 1 script
}

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1"));

app.UseHttpsRedirection();
app.UseSession();       // must come before MapControllers
app.MapControllers();

app.Run();

// Lets the test project start the API with WebApplicationFactory<Program>
public partial class Program { }
