using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Filters;
using RaceDay.Web.Models;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[RoleAuthorize("Organiser")]
public class OrganiserController : Controller
{
    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private readonly IApiClient _api;

    public OrganiserController(IApiClient api) => _api = api;

    // ---------- Dashboard ----------
    public async Task<IActionResult> Dashboard()
    {
        var events = await MyEventsAsync();
        foreach (var e in events)
            e.EnrolmentCount = (await EnrolmentsForEventAsync(e.EventId)).Count(x => x.Status != "Cancelled");
        return View(new DashboardViewModel { Events = events });
    }

    // ---------- Events ----------
    public async Task<IActionResult> Events() => View(await MyEventsAsync());

    [HttpGet]
    public IActionResult CreateEvent() => View("EventForm", new EventFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        model.Status = "Published";
        var created = await _api.PostAsync<EventDto>("api/events", ToPayload(model));
        if (!created.Success || created.Data == null)
        {
            ModelState.AddModelError("", created.Error ?? "We couldn't create the event.");
            return View("EventForm", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var up = await _api.UploadAsync<UploadResultDto>($"api/events/{created.Data.EventId}/banner", model.Banner);
            if (!up.Success) TempData["Error"] = "Event created, but the banner upload failed: " + up.Error;
        }

        if (TempData["Error"] == null) TempData["Success"] = "Event created. Now add categories for it.";
        return RedirectToAction(nameof(Categories), new { eventId = created.Data.EventId });
    }

    [HttpGet]
    public async Task<IActionResult> EditEvent(int id)
    {
        var r = await _api.GetAsync<EventDto>($"api/events/{id}");
        if (!r.Success || r.Data == null)
        {
            TempData["Error"] = r.Error ?? "Event not found.";
            return RedirectToAction(nameof(Events));
        }

        var e = r.Data;
        return View("EventForm", new EventFormViewModel
        {
            Id = e.EventId, Name = e.Name, Description = e.Description,
            EventDate = e.EventDate.ToDateTime(TimeOnly.MinValue),
            Location = e.Location, DistanceKm = e.DistanceKm, EventType = e.EventType,
            Status = e.Status == "Draft" ? "Published" : e.Status,
            ExistingBannerUrl = e.BannerUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.Id = id;
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        var updated = await _api.PutAsync<EventDto>($"api/events/{id}", ToPayload(model));
        if (!updated.Success)
        {
            ModelState.AddModelError("", updated.Error ?? "We couldn't update the event.");
            return View("EventForm", model);
        }

        if (model.Banner is { Length: > 0 })
        {
            var up = await _api.UploadAsync<UploadResultDto>($"api/events/{id}/banner", model.Banner);
            if (!up.Success) TempData["Error"] = "Event saved, but the banner upload failed: " + up.Error;
        }

        if (TempData["Error"] == null) TempData["Success"] = "Event updated.";
        return RedirectToAction(nameof(Events));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var r = await _api.DeleteAsync($"api/events/{id}");
        if (r.Success) TempData["Success"] = "Event deleted.";
        else TempData["Error"] = r.Error ?? "We couldn't delete the event.";
        return RedirectToAction(nameof(Events));
    }

    // ---------- Categories ----------
    public async Task<IActionResult> Categories(int eventId)
    {
        var vm = await BuildCategoriesVm(eventId);
        return vm == null ? RedirectToAction(nameof(Events)) : View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(CategoryFormViewModel form)
    {
        if (form.CategoryType == "Distance" && !form.DistanceKm.HasValue)
            ModelState.AddModelError(nameof(form.DistanceKm), "Enter the distance for a distance category");
        if (form.CategoryType == "Age")
        {
            if (!form.MinAge.HasValue)
                ModelState.AddModelError(nameof(form.MinAge), "Enter the minimum age for an age category");
            if (form.MinAge.HasValue && form.MaxAge.HasValue && form.MaxAge < form.MinAge)
                ModelState.AddModelError(nameof(form.MaxAge), "Maximum age cannot be lower than the minimum age");
        }

        if (!ModelState.IsValid)
        {
            var vm = await BuildCategoriesVm(form.EventId);
            if (vm == null) return RedirectToAction(nameof(Events));
            vm.Form = form;
            return View("Categories", vm);
        }

        var isDistance = form.CategoryType == "Distance";
        var r = await _api.PostAsync<CategoryDto>($"api/events/{form.EventId}/categories", new
        {
            form.Name,
            form.CategoryType,
            DistanceKm = isDistance ? form.DistanceKm : null,
            MinAge = isDistance ? null : form.MinAge,
            MaxAge = isDistance ? null : form.MaxAge,
            form.MaxParticipants,
            form.Price
        });

        if (r.Success) TempData["Success"] = "Category added.";
        else TempData["Error"] = r.Error ?? "We couldn't add the category.";
        return RedirectToAction(nameof(Categories), new { eventId = form.EventId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int eventId, int categoryId)
    {
        var r = await _api.DeleteAsync($"api/categories/{categoryId}");
        if (r.Success) TempData["Success"] = "Category removed.";
        else TempData["Error"] = r.Error ?? "We couldn't remove the category.";
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    // ---------- Enrolments ----------
    public async Task<IActionResult> Enrolments(int eventId)
    {
        var ev = await _api.GetAsync<EventDto>($"api/events/{eventId}");
        if (!ev.Success || ev.Data == null)
        {
            TempData["Error"] = ev.Error ?? "Event not found.";
            return RedirectToAction(nameof(Events));
        }

        var enrolments = await EnrolmentsForEventAsync(eventId);
        return View(new EnrolmentsPageViewModel { Event = ev.Data, Enrolments = enrolments });
    }

    // ---------- Results ----------
    [HttpGet]
    public async Task<IActionResult> Results(int eventId)
    {
        var vm = await BuildResultsVm(eventId);
        return vm == null ? RedirectToAction(nameof(Events)) : View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Results(ResultsEntryViewModel model)
    {
        var toSend = new List<(ResultEntryRow Row, TimeOnly? Time)>();

        for (var i = 0; i < model.Rows.Count; i++)
        {
            var row = model.Rows[i];
            var finished = row.Status == "Finished";
            var hasTime = !string.IsNullOrWhiteSpace(row.FinishTime);

            // nothing entered and nothing saved yet: skip this runner
            if (finished && !hasTime && !row.Position.HasValue && !row.ResultId.HasValue) continue;

            TimeOnly? time = null;
            if (finished)
            {
                if (!hasTime || !row.Position.HasValue)
                {
                    ModelState.AddModelError("", $"Enter both a finish time and a position for {row.ParticipantName}.");
                    continue;
                }
                if (!TimeOnly.TryParse(row.FinishTime, out var parsed))
                {
                    ModelState.AddModelError("", $"Finish time for {row.ParticipantName} must look like 01:23:45.");
                    continue;
                }
                if (row.Position < 1)
                {
                    ModelState.AddModelError("", $"Position for {row.ParticipantName} must be 1 or higher.");
                    continue;
                }
                time = parsed;
            }
            toSend.Add((row, time));
        }

        // the same position cannot be used twice in one category
        var dup = toSend.Where(x => x.Row.Status == "Finished")
            .GroupBy(x => (x.Row.CategoryName, x.Row.Position)).FirstOrDefault(g => g.Count() > 1);
        if (dup != null)
            ModelState.AddModelError("", $"Position {dup.Key.Position} is used more than once in {dup.Key.CategoryName}.");

        if (!ModelState.IsValid) return View(model);

        var failures = new List<string>();
        var saved = 0;
        foreach (var (row, time) in toSend)
        {
            var finished = row.Status == "Finished";
            var body = new
            {
                FinishTime = finished ? time : null,
                Position = finished ? row.Position : null,
                row.Status
            };

            var res = row.ResultId.HasValue
                ? await _api.PutAsync<ResultDto>($"api/results/{row.ResultId}", body)
                : await _api.PostAsync<ResultDto>($"api/enrolments/{row.EnrolmentId}/results", body);

            if (res.Success) saved++;
            else failures.Add($"{row.ParticipantName}: {res.Error}");
        }

        if (saved > 0) TempData["Success"] = $"{saved} result(s) saved.";
        if (failures.Count > 0) TempData["Error"] = string.Join(" | ", failures);
        return RedirectToAction(nameof(Results), new { eventId = model.EventId });
    }

    // ---------- helpers ----------
    private async Task<List<EventDto>> MyEventsAsync()
    {
        var user = HttpContext.Session.GetUser()!;
        var r = await _api.GetAsync<List<EventDto>>("api/events");
        if (!r.Success) TempData["Error"] = r.Error;
        return (r.Data ?? new()).Where(e => e.OrganiserId == user.UserId).OrderBy(e => e.EventDate).ToList();
    }

    // The API lists enrolments per category, so gather them for every category of the event.
    private async Task<List<EnrolmentDto>> EnrolmentsForEventAsync(int eventId)
    {
        var cats = await _api.GetAsync<List<CategoryDto>>($"api/events/{eventId}/categories");
        var all = new List<EnrolmentDto>();
        foreach (var c in cats.Data ?? new())
        {
            var r = await _api.GetAsync<List<EnrolmentDto>>($"api/categories/{c.CategoryId}/enrolments");
            if (r.Success && r.Data != null) all.AddRange(r.Data);
            else if (!r.Success) TempData["Error"] = r.Error;
        }
        return all;
    }

    private void ValidateBanner(EventFormViewModel model)
    {
        if (model.Banner is not { Length: > 0 }) return;
        if (!AllowedImageTypes.Contains(model.Banner.ContentType))
            ModelState.AddModelError(nameof(model.Banner), "Banner must be a JPG, PNG or WEBP image.");
        else if (model.Banner.Length > MaxImageBytes)
            ModelState.AddModelError(nameof(model.Banner), "Banner must be 5 MB or smaller.");
    }

    private static object ToPayload(EventFormViewModel m) => new
    {
        m.Name,
        EventDate = DateOnly.FromDateTime(m.EventDate),
        m.Location,
        m.Description,
        m.DistanceKm,
        m.EventType,
        m.Status
    };

    private async Task<CategoriesPageViewModel?> BuildCategoriesVm(int eventId)
    {
        var ev = await _api.GetAsync<EventDto>($"api/events/{eventId}");
        if (!ev.Success || ev.Data == null)
        {
            TempData["Error"] = ev.Error ?? "Event not found.";
            return null;
        }
        var cats = await _api.GetAsync<List<CategoryDto>>($"api/events/{eventId}/categories");
        return new CategoriesPageViewModel
        {
            Event = ev.Data,
            Categories = cats.Data ?? new(),
            Form = new CategoryFormViewModel { EventId = eventId }
        };
    }

    private async Task<ResultsEntryViewModel?> BuildResultsVm(int eventId)
    {
        var ev = await _api.GetAsync<EventDto>($"api/events/{eventId}");
        if (!ev.Success || ev.Data == null)
        {
            TempData["Error"] = ev.Error ?? "Event not found.";
            return null;
        }

        var enrolments = await EnrolmentsForEventAsync(eventId);
        var existing = await _api.GetAsync<List<ResultDto>>($"api/events/{eventId}/results");
        var byEnrolment = (existing.Data ?? new()).ToDictionary(r => r.EnrolmentId);

        var rows = enrolments
            .Where(e => e.Status == "Confirmed")
            .OrderBy(e => e.CategoryName).ThenBy(e => e.BibNumber)
            .Select(e =>
            {
                byEnrolment.TryGetValue(e.EnrolmentId, out var r);
                return new ResultEntryRow
                {
                    EnrolmentId = e.EnrolmentId,
                    ResultId = r?.ResultId,
                    ParticipantName = e.ParticipantName,
                    BibNumber = e.BibNumber,
                    CategoryName = e.CategoryName,
                    Status = r?.Status ?? "Finished",
                    FinishTime = r?.FinishTime?.ToString("HH:mm:ss"),
                    Position = r?.Position
                };
            }).ToList();

        return new ResultsEntryViewModel
        {
            EventId = eventId,
            EventName = ev.Data.Name,
            EventDate = ev.Data.EventDate.ToDateTime(TimeOnly.MinValue),
            Rows = rows
        };
    }
}
