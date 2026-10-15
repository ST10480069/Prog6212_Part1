using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>GET/PUT /api/users/me for both roles.</summary>
public class ProfileTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    [Fact]
    public async Task GetMe_WithoutLogin_Returns401()
    {
        var response = await factory.NewClient().GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task GetMe_LoggedIn_ReturnsOwnProfile_ForBothRoles(string role)
    {
        var user = await factory.NewUserAsync(role);

        var response = await user.Client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.ReadAsync<UserProfileDto>();
        Assert.Equal(user.Profile.UserId, profile.UserId);
        Assert.Equal(role, profile.Role);
        Assert.Equal(user.Email, profile.Email);
    }

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task UpdateMe_ChangesNameAndPhone_ForBothRoles(string role)
    {
        var user = await factory.NewUserAsync(role);

        var response = await user.Client.PutAsJsonAsync("/api/users/me", new { fullName = "New Name", phoneNumber = "0831112222" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadAsync<UserProfileDto>();
        Assert.Equal("New Name", updated.FullName);
        Assert.Equal("0831112222", updated.PhoneNumber);

        var reread = await (await user.Client.GetAsync("/api/users/me")).ReadAsync<UserProfileDto>();
        Assert.Equal("New Name", reread.FullName);
        Assert.Equal(user.Email, reread.Email); // email and role are not changed by this endpoint
        Assert.Equal(role, reread.Role);
    }

    [Fact]
    public async Task UpdateMe_WithoutLogin_Returns401()
    {
        var response = await factory.NewClient().PutAsJsonAsync("/api/users/me", new { fullName = "Hacker" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_EmptyName_Returns400()
    {
        var user = await factory.NewUserAsync("Participant");

        var response = await user.Client.PutAsJsonAsync("/api/users/me", new { fullName = "", phoneNumber = "0831112222" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_OnlyChangesTheLoggedInUser()
    {
        var alice = await factory.NewUserAsync("Participant");
        var bob = await factory.NewUserAsync("Participant");

        await alice.Client.PutAsJsonAsync("/api/users/me", new { fullName = "Alice Changed" });

        var bobProfile = await (await bob.Client.GetAsync("/api/users/me")).ReadAsync<UserProfileDto>();
        Assert.NotEqual("Alice Changed", bobProfile.FullName);
    }
}
