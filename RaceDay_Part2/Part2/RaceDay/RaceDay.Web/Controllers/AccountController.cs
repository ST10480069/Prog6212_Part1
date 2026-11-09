using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class AccountController : Controller
{
    private readonly IApiClient _api;

    public AccountController(IApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (HttpContext.Session.GetUser() != null) return RedirectToHome();
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(model);

        // The API starts its own session and returns its cookie; ApiClient stores that cookie in our session.
        var result = await _api.PostAsync<LoginResponseDto>("api/auth/login", new { model.Email, model.Password });
        if (!result.Success || result.Data?.User == null)
        {
            ModelState.AddModelError("", result.Error ?? "Login failed. Please try again.");
            return View(model);
        }

        var u = result.Data.User;
        HttpContext.Session.SetUser(new SessionUser { UserId = u.UserId, FullName = u.FullName, Email = u.Email, Role = u.Role });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToHome();
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (HttpContext.Session.GetUser() != null) return RedirectToHome();
        return View(new RegisterViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (model.Role is not ("Organiser" or "Participant"))
            ModelState.AddModelError(nameof(model.Role), "Choose a valid role");
        if (model.Role == "Organiser" && string.IsNullOrWhiteSpace(model.OrganiserCode))
            ModelState.AddModelError(nameof(model.OrganiserCode), "Organisers need an organiser code");

        if (!ModelState.IsValid) return View(model);

        var result = await _api.PostAsync<UserProfileDto>("api/auth/register", new
        {
            model.FullName,
            model.Email,
            model.Password,
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber,
            model.Role,
            OrganiserCode = model.Role == "Organiser" ? model.OrganiserCode : null
        });

        if (!result.Success)
        {
            ModelState.AddModelError("", result.Error ?? "Registration failed. Please try again.");
            return View(model);
        }

        TempData["Success"] = "Account created! You can now log in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _api.PostAsync<object>("api/auth/logout", null);   // end the API session too
        HttpContext.Session.Clear();
        TempData["Success"] = "You have been logged out.";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();

    private IActionResult RedirectToHome()
    {
        var user = HttpContext.Session.GetUser();
        return user?.Role == "Organiser"
            ? RedirectToAction("Dashboard", "Organiser")
            : RedirectToAction("Index", "Home");
    }
}
