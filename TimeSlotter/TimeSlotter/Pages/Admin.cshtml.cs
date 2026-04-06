using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TimeSlotter.Data;
using TimeSlotter.Models;
using TimeSlot = TimeSlotter.Models.Slot;

namespace TimeSlotter.Pages;

[Authorize]
public class AdminModel : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly UserManager<Provider> _userManager;
    private readonly AppDbContext _context;

    public AdminModel(UserManager<Provider> userManager, AppDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    public Provider Provider { get; set; } = null!;

    /// <summary>Persisted slots for <see cref="ScheduleInitialDate"/> (same source as initial JSON).</summary>
    public List<TimeSlot> ExistingSlots { get; set; } = new();

    /// <summary>yyyy-MM-dd for the schedule query and target date input.</summary>
    public string ScheduleInitialDate { get; set; } = "";

    /// <summary>Serialized payload: { "slots": [ ... ] } for first paint.</summary>
    public string InitialSlotsJson { get; set; } = "{\"slots\":[]}";

    [BindProperty]
    public string? FinalSlotsJson { get; set; }

    /// <summary>Relative URL from TempData["GeneratedLink"] after save (one-shot).</summary>
    public string? PromoBookingPath { get; set; }

    /// <summary>Slug or user name for <c>?slug=</c> on the public booking page.</summary>
    public string BookingLinkSlug { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(string? date)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        Provider = user;
        BookingLinkSlug = ResolveBookingLinkSlug(user);
        var day = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsed))
        {
            day = parsed;
        }

        ScheduleInitialDate = day.ToString("yyyy-MM-dd");
        ExistingSlots = await LoadSlotsForDayAsync(user.Id, day);
        var list = ToSnapshotList(ExistingSlots, day);
        InitialSlotsJson = JsonSerializer.Serialize(new { slots = list }, JsonWriteOptions);

        if (TempData.TryGetValue("GeneratedLink", out var genObj) && genObj is string genLink && !string.IsNullOrWhiteSpace(genLink))
        {
            PromoBookingPath = genLink.Trim();
        }

        return Page();
    }

    /// <summary>JSON snapshot of persisted slots for a date (polling / refresh).</summary>
    public async Task<IActionResult> OnGetSlotSnapshotsAsync(string date)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        if (!DateOnly.TryParse(date, out var day))
        {
            return new JsonResult(new { error = "Invalid date." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var entities = await LoadSlotsForDayAsync(user.Id, day);
        var list = ToSnapshotList(entities, day);
        return new JsonResult(new { slots = list }, JsonWriteOptions);
    }

    /// <summary>Toggles persisted slot between Available (0) and ReservedByAdmin (3).</summary>
    public async Task<JsonResult> OnPostUpdateStatusAsync(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "Unauthorized" }, JsonWriteOptions) { StatusCode = 401 };
        }

        var slot = await _context.Slots.FirstOrDefaultAsync(s => s.Id == id && s.ProviderId == user.Id);
        if (slot == null)
        {
            return new JsonResult(new { success = false, error = "Slot not found." }, JsonWriteOptions) { StatusCode = 404 };
        }

        if (slot.Status == SlotStatus.BookedByClient)
        {
            return new JsonResult(new { success = false, error = "Cannot change a client-booked slot." }, JsonWriteOptions) { StatusCode = 400 };
        }

        if (slot.Status == SlotStatus.Available)
        {
            slot.Status = SlotStatus.ReservedByAdmin;
        }
        else if (slot.Status == SlotStatus.ReservedByAdmin)
        {
            slot.Status = SlotStatus.Available;
        }
        else
        {
            return new JsonResult(new { success = false, error = "Slot status cannot be toggled." }, JsonWriteOptions) { StatusCode = 400 };
        }

        await _context.SaveChangesAsync();
        var code = (int)slot.Status;
        return new JsonResult(new { success = true, newStatus = code }, JsonWriteOptions);
    }

    public async Task<JsonResult> OnPostDeleteSlotAsync(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "Unauthorized" }, JsonWriteOptions) { StatusCode = 401 };
        }

        var slot = await _context.Slots.FirstOrDefaultAsync(s => s.Id == id && s.ProviderId == user.Id);
        if (slot == null)
        {
            return new JsonResult(new { success = false, error = "Slot not found." }, JsonWriteOptions) { StatusCode = 404 };
        }

        _context.Slots.Remove(slot);
        await _context.SaveChangesAsync();
        return new JsonResult(new { success = true }, JsonWriteOptions);
    }

    /// <summary>Remove all persisted slots for the given calendar day (provider-scoped).</summary>
    public async Task<JsonResult> OnPostClearDayAsync(DateTime date)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "Unauthorized" }, JsonWriteOptions) { StatusCode = 401 };
        }

        var dayStart = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        var dayEndExclusive = dayStart.AddDays(1);

        var toRemove = await _context.Slots
            .Where(s => s.ProviderId == user.Id && s.StartTime >= dayStart && s.StartTime < dayEndExclusive)
            .ToListAsync();

        if (toRemove.Count > 0)
        {
            _context.Slots.RemoveRange(toRemove);
            await _context.SaveChangesAsync();
        }

        return new JsonResult(new { success = true, removed = toRemove.Count }, JsonWriteOptions);
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(FinalSlotsJson))
        {
            return RedirectToPage();
        }

        List<SlotDraftJson>? draft;
        try
        {
            draft = JsonSerializer.Deserialize<List<SlotDraftJson>>(FinalSlotsJson, JsonOptions);
        }
        catch (JsonException)
        {
            ModelState.AddModelError(string.Empty, "Invalid slot data.");
            Provider = user;
            BookingLinkSlug = ResolveBookingLinkSlug(user);
            return Page();
        }

        if (draft is null || draft.Count == 0)
        {
            return RedirectToPage();
        }

        const int maxSlots = 96;
        if (draft.Count > maxSlots)
        {
            draft = draft.Take(maxSlots).ToList();
        }

        var slots = new List<TimeSlot>();
        foreach (var item in draft)
        {
            if (!DateOnly.TryParse(item.Date, out var date))
                continue;
            if (!TryParseHm(item.Start, out var startMin) || !TryParseHm(item.End, out var endMin))
                continue;

            var start = new DateTime(date.Year, date.Month, date.Day, startMin / 60, startMin % 60, 0, DateTimeKind.Unspecified);
            var end = new DateTime(date.Year, date.Month, date.Day, endMin / 60, endMin % 60, 0, DateTimeKind.Unspecified);
            if (end <= start)
                continue;

            slots.Add(new TimeSlot
            {
                ProviderId = user.Id,
                StartTime = start,
                EndTime = end,
                Status = SlotStatus.Available,
                ResourceContext = string.IsNullOrWhiteSpace(item.ResourceContext) ? null : item.ResourceContext.Trim(),
            });
        }

        if (slots.Count == 0)
        {
            return RedirectToPage();
        }

        foreach (var newSlot in slots)
        {
            var rcKey = newSlot.ResourceContext ?? string.Empty;
            var overlapsPersisted = await _context.Slots.AnyAsync(s =>
                s.ProviderId == user.Id
                && s.StartTime < newSlot.EndTime
                && newSlot.StartTime < s.EndTime
                && (s.ResourceContext ?? string.Empty) == rcKey);
            if (overlapsPersisted)
            {
                return BadRequest("Time overlap detected");
            }
        }

        for (var i = 0; i < slots.Count; i++)
        {
            for (var j = i + 1; j < slots.Count; j++)
            {
                var a = slots[i];
                var b = slots[j];
                if ((a.ResourceContext ?? string.Empty) != (b.ResourceContext ?? string.Empty))
                {
                    continue;
                }

                if (a.StartTime < b.EndTime && b.StartTime < a.EndTime)
                {
                    return BadRequest("Time overlap detected");
                }
            }
        }

        _context.Slots.AddRange(slots);
        await _context.SaveChangesAsync();

        var targetDateStr = DateOnly.FromDateTime(slots[0].StartTime).ToString("yyyy-MM-dd");
        var slugQ = string.IsNullOrWhiteSpace(user.Slug)
            ? Uri.EscapeDataString(user.UserName ?? user.Email ?? "")
            : Uri.EscapeDataString(user.Slug.Trim());
        TempData["GeneratedLink"] = $"/Booking?slug={slugQ}&date={targetDateStr}";

        return RedirectToPage();
    }

    private static string ResolveBookingLinkSlug(Provider user) =>
        !string.IsNullOrWhiteSpace(user.Slug)
            ? user.Slug.Trim()
            : (user.UserName ?? user.Email ?? "").Trim();

    private async Task<List<TimeSlot>> LoadSlotsForDayAsync(int providerId, DateOnly day)
    {
        var start = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var end = day.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Unspecified);

        return await _context.Slots
            .AsNoTracking()
            .Include(s => s.Bookings)
            .Where(s => s.ProviderId == providerId && s.StartTime >= start && s.StartTime <= end)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    private static List<SlotSnapshotDto> ToSnapshotList(IEnumerable<TimeSlot> slots, DateOnly day)
    {
        var dateStr = day.ToString("yyyy-MM-dd");
        return slots.Select(s => ToSnapshotDto(s, dateStr)).ToList();
    }

    private static SlotSnapshotDto ToSnapshotDto(TimeSlot s, string dateStr)
    {
        var b = s.Bookings.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        return new SlotSnapshotDto(
            s.Id,
            s.StartTime.ToString("HH:mm"),
            s.EndTime.ToString("HH:mm"),
            dateStr,
            s.ResourceContext,
            (int)s.Status,
            s.Status.ToString(),
            b?.CustomerName,
            b?.CustomerPhone);
    }

    private static bool TryParseHm(string value, out int totalMinutes)
    {
        totalMinutes = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        if (!int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m))
            return false;

        if (h is < 0 or > 23 || m is < 0 or > 59)
            return false;

        totalMinutes = h * 60 + m;
        return true;
    }

    public sealed record SlotSnapshotDto(
        int Id,
        string Start,
        string End,
        string Date,
        string? ResourceContext,
        int StatusCode,
        string Status,
        string? ClientName,
        string? ClientPhone);

    private sealed class SlotDraftJson
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string? ResourceContext { get; set; }
    }
}
