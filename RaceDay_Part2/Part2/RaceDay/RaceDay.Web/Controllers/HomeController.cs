using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public class HomeController : Controller
{
    private readonly IApiClient _api;

    public HomeController(IApiClient api) => _api = api;

    public async Task<IActionResult> Index([FromQuery] EventFilter filter)
    {
        var user = HttpContext.Session.GetUser();
        if (user?.Role == "Organiser") return RedirectToAction("Dashboard", "Organiser");

        var result = await _api.GetAsync<List<EventDto>>("api/events");
        if (!result.Success) TempData["Error"] = result.Error;

        var today = DateOnly.FromDateTime(DateTime.Today);
        IEnumerable<EventDto> q = (result.Data ?? new())
            .Where(e => e.EventDate >= today && e.Status == "Published");

        if (!string.IsNullOrWhiteSpace(filter.Search))
            q = q.Where(e => e.Name.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.EventType))
            q = q.Where(e => e.EventType.Equals(filter.EventType, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(filter.Location))
            q = q.Where(e => e.Location.Contains(filter.Location, StringComparison.OrdinalIgnoreCase));
        if (filter.MaxDistance.HasValue)
            q = q.Where(e => e.DistanceKm <= filter.MaxDistance.Value);

        return View(new HomeViewModel { Events = q.OrderBy(e => e.EventDate).ToList(), Filter = filter });
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = HttpContext.Session.GetUser();
        var ev = await _api.GetAsync<EventDto>($"api/events/{id}");
        if (!ev.Success || ev.Data == null)
        {
            TempData["Error"] = ev.Error ?? "Event not found.";
            return RedirectToAction(nameof(Index));
        }

        var cats = await _api.GetAsync<List<CategoryDto>>($"api/events/{id}/categories");
        ev.Data.Categories = cats.Data ?? new();

        var already = false;
        if (user?.Role == "Participant")
        {
            var mine = await _api.GetAsync<List<EnrolmentDto>>("api/users/me/enrolments");
            already = mine.Data?.Any(e => e.EventId == id && e.Status != "Cancelled") ?? false;
        }

        return View(new EventDetailsViewModel
        {
            Event = ev.Data,
            CanEnrol = user?.Role == "Participant" && ev.Data.Status == "Published"
                       && ev.Data.EventDate >= DateOnly.FromDateTime(DateTime.Today),
            AlreadyEnrolled = already
        });
    }

    [HttpPost, ValidateAntiForgeryToken, RoleAuthorize("Participant")]
    public async Task<IActionResult> Enrol(EnrolFormModel model)
    {
        if (model.CategoryId <= 0)
        {
            TempData["Error"] = "Please select a category before enrolling.";
            return RedirectToAction(nameof(Details), new { id = model.EventId });
        }

        var result = await _api.PostAsync<EnrolmentDto>($"api/categories/{model.CategoryId}/enrolments", null);
        if (result.Success)
        {
            TempData["Success"] = "You're enrolled! Your bib number and status are under 'My Enrolments'.";
            return RedirectToAction("MyEnrolments", "Participant");
        }

        TempData["Error"] = result.Error ?? "We couldn't complete your enrolment.";
        return RedirectToAction(nameof(Details), new { id = model.EventId });
    }

    public IActionResult Error(int? code = null)
    {
        ViewBag.Code = code;
        return View();
    }
}
