using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>User registration, login, logout and session behaviour.</summary>
public class AuthTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private static object Registration(string email, string role, string? code = null, string password = Api.Password) => new
    {
        fullName = "Thandi Mokoena",
        email,
        password,
        phoneNumber = "0821234567",
        role,
        organiserCode = code
    };

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@test.com";

    // ---------------- Registration ----------------

    [Fact]
    public async Task Register_Participant_Returns201WithProfile()
    {
        var client = factory.NewClient();
        var email = NewEmail();

        var response = await client.PostAsJsonAsync("/api/auth/register", Registration(email, "Participant"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.ReadAsync<UserProfileDto>();
        Assert.Equal(email, profile.Email);
        Assert.Equal("Participant", profile.Role);
        Assert.Equal("Thandi Mokoena", profile.FullName);
    }

    [Fact]
    public async Task Register_Response_NeverContainsPassword()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register", Registration(NewEmail(), "Participant"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_Organiser_WithValidCode_Returns201()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Registration(NewEmail(), "Organiser", TestApiFactory.OrganiserCode));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Organiser", (await response.ReadAsync<UserProfileDto>()).Role);
    }

    [Fact]
    public async Task Register_Organiser_WithoutCode_Returns403()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Registration(NewEmail(), "Organiser"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongCode_Returns403()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Registration(NewEmail(), "Organiser", "NOT-THE-CODE"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_RoleIsCaseInsensitive()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Registration(NewEmail(), "participant"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Participant", (await response.ReadAsync<UserProfileDto>()).Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = factory.NewClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", Registration(email, "Participant"));

        var second = await client.PostAsJsonAsync("/api/auth/register", Registration(email, "Participant"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmailWithDifferentCase_Returns409()
    {
        var client = factory.NewClient();
        var email = NewEmail();
        await client.PostAsJsonAsync("/api/auth/register", Registration(email, "Participant"));

        var second = await client.PostAsJsonAsync("/api/auth/register", Registration(email.ToUpperInvariant(), "Participant"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("")]
    [InlineData("Spectator")]
    public async Task Register_InvalidRole_Returns400(string role)
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register", Registration(NewEmail(), role));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Registration(NewEmail(), "Participant", password: "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_Returns400()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register",
            Registration("not-an-email", "Participant"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MissingFields_Returns400()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/register", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NotThePlainText()
    {
        var email = NewEmail();
        await factory.NewClient().PostAsJsonAsync("/api/auth/register", Registration(email, "Participant"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = db.Users.Single(u => u.Email == email).PasswordHash;

        Assert.NotEqual(Api.Password, stored);
        Assert.DoesNotContain(Api.Password, stored);
        Assert.StartsWith("$2", stored);                                   // BCrypt hash format
        Assert.True(BCrypt.Net.BCrypt.Verify(Api.Password, stored));
    }

    // ---------------- Login ----------------

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithProfileAndRole()
    {
        var client = factory.NewClient();
        var profile = await client.RegisterAsync("Participant");

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = profile.Email, password = Api.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.ReadAsync<LoginResponse>();
        Assert.Equal(profile.UserId, login.User.UserId);
        Assert.Equal("Participant", login.User.Role);
    }

    [Fact]
    public async Task Login_EmailIsNotCaseSensitive()
    {
        var client = factory.NewClient();
        var profile = await client.RegisterAsync("Participant");

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = profile.Email.ToUpperInvariant(), password = Api.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = factory.NewClient();
        var profile = await client.RegisterAsync("Participant");

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = profile.Email, password = "WrongPassword1!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/login",
            new { email = NewEmail(), password = Api.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingFields_Returns400()
    {
        var response = await factory.NewClient().PostAsJsonAsync("/api/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------------- Session ----------------

    [Fact]
    public async Task Login_StartsSession_ThatProtectedEndpointsRecognise()
    {
        var client = factory.NewClient();
        var profile = await client.RegisterAsync("Participant");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode); // before login
        await client.LoginAsync(profile.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);            // after login
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var user = await factory.NewUserAsync("Participant");

        var logout = await user.Client.PostAsync("/api/auth/logout", null);
        var afterwards = await user.Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task Logout_WhenNotLoggedIn_Returns401()
    {
        var response = await factory.NewClient().PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Sessions_AreSeparatePerClient()
    {
        var organiser = await factory.NewUserAsync("Organiser");
        var participant = await factory.NewUserAsync("Participant");

        var organiserMe = await (await organiser.Client.GetAsync("/api/users/me")).ReadAsync<UserProfileDto>();
        var participantMe = await (await participant.Client.GetAsync("/api/users/me")).ReadAsync<UserProfileDto>();

        Assert.Equal("Organiser", organiserMe.Role);
        Assert.Equal("Participant", participantMe.Role);
        Assert.NotEqual(organiserMe.UserId, participantMe.UserId);
    }
}
