using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RoleAuthorize("Participant")]
public class ParticipantController : Controller
{
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private readonly IApiClient _api;

    public ParticipantController(IApiClient api) => _api = api;

    public async Task<IActionResult> MyEnrolments()
    {
        var r = await _api.GetAsync<List<EnrolmentDto>>("api/users/me/enrolments");
        if (!r.Success) TempData["Error"] = r.Error;

        var dates = await EventDatesAsync();
        var list = r.Data ?? new();
        foreach (var e in list)
            if (dates.TryGetValue(e.EventId, out var d)) e.EventDate = d;

        return View(list);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelEnrolment(int id)
    {
        var r = await _api.DeleteAsync($"api/enrolments/{id}");
        if (r.Success) TempData["Success"] = "Your enrolment was cancelled.";
        else TempData["Error"] = r.Error ?? "We couldn't cancel that enrolment.";
        return RedirectToAction(nameof(MyEnrolments));
    }

    public async Task<IActionResult> MyResults()
    {
        var r = await _api.GetAsync<List<ResultDto>>("api/users/me/results");
        if (!r.Success) TempData["Error"] = r.Error;

        var list = r.Data ?? new();
        var dates = await EventDatesAsync();

        // "47th out of 312 finishers": count the finishers per category of each event
        var finishers = new Dictionary<int, int>();   // categoryId -> number of finishers
        foreach (var eventId in list.Select(x => x.EventId).Distinct())
        {
            var all = await _api.GetAsync<List<ResultDto>>($"api/events/{eventId}/results");
            foreach (var g in (all.Data ?? new()).Where(x => x.Status == "Finished" && x.Position.HasValue).GroupBy(x => x.CategoryId))
                finishers[g.Key] = g.Count();
        }

        foreach (var x in list)
        {
            if (dates.TryGetValue(x.EventId, out var d)) x.EventDate = d;
            x.TotalFinishers = finishers.GetValueOrDefault(x.CategoryId);
        }

        return View(list.OrderByDescending(x => x.EventDate).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var r = await _api.GetAsync<UserProfileDto>("api/users/me");
        if (!r.Success || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "We couldn't load your profile.";
            return RedirectToAction("Index", "Home");
        }

        return View(new ProfileViewModel
        {
            FullName = r.Data.FullName, Email = r.Data.Email,
            PhoneNumber = r.Data.PhoneNumber, ProfilePictureUrl = r.Data.ProfilePictureUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (model.Picture is { Length: > 0 })
        {
            if (!AllowedImageTypes.Contains(model.Picture.ContentType))
                ModelState.AddModelError(nameof(model.Picture), "Picture must be a JPG, PNG or WEBP image.");
            else if (model.Picture.Length > MaxImageBytes)
                ModelState.AddModelError(nameof(model.Picture), "Picture must be 5 MB or smaller.");
        }

        if (!ModelState.IsValid) return View(model);

        var update = await _api.PutAsync<UserProfileDto>("api/users/me", new { model.FullName, model.PhoneNumber });
        if (!update.Success)
        {
            ModelState.AddModelError("", update.Error ?? "We couldn't update your profile.");
            return View(model);
        }

        if (model.Picture is { Length: > 0 })
        {
            var up = await _api.UploadAsync<UploadResultDto>("api/users/me/picture", model.Picture);
            if (!up.Success) TempData["Error"] = "Details saved, but the picture upload failed: " + up.Error;
        }

        // keep the name shown in the navbar in sync
        var user = HttpContext.Session.GetUser();
        if (user != null)
        {
            user.FullName = model.FullName;
            HttpContext.Session.SetUser(user);
        }

        if (TempData["Error"] == null) TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }

    // The enrolment/result responses carry no event date, so look it up from the events list.
    private async Task<Dictionary<int, DateOnly>> EventDatesAsync()
    {
        var events = await _api.GetAsync<List<EventDto>>("api/events");
        return (events.Data ?? new()).ToDictionary(e => e.EventId, e => e.EventDate);
    }
}
