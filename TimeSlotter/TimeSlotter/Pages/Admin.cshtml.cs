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
    [BindProperty(SupportsGet = true)]
    public string ScheduleInitialDate { get; set; } = "";

    /// <summary>Serialized payload: { "slots": [ ... ] } for first paint.</summary>
    public string InitialSlotsJson { get; set; } = "{\"slots\":[]}";

    [BindProperty]
    public string? FinalSlotsJson { get; set; }

    /// <summary>Slug or user name for <c>?slug=</c> on the public booking page.</summary>
    public string BookingLinkSlug { get; set; } = "";

    public IReadOnlyList<CustomerPickVm> CustomersForAssign { get; set; } = Array.Empty<CustomerPickVm>();

    public IReadOnlyList<AvailableSlotAssignVm> AvailableSlotsForAssign { get; set; } = Array.Empty<AvailableSlotAssignVm>();

    /// <summary>Clamped minutes used when splitting merged slots (UI + server).</summary>
    public int DefaultSlotIntervalMinutes { get; set; } = 30;

    [BindProperty]
    public int AssignSlotId { get; set; }

    [BindProperty]
    public string AssignCustomerSelection { get; set; } = "walkin";

    [BindProperty]
    public string? AssignWalkInName { get; set; }

    [BindProperty]
    public string? AssignWalkInPhone { get; set; }

    public async Task<IActionResult> OnGetAsync(string? date)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var day = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsed))
        {
            day = parsed;
        }

        await PrepareSchedulePageAsync(user, day);

        return Page();
    }

    /// <summary>AJAX: persist provider default split interval (minutes).</summary>
    public async Task<IActionResult> OnPostDefaultSlotIntervalAsync([FromForm] int minutes)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "Unauthorized" }, JsonWriteOptions) { StatusCode = 401 };
        }

        var clamped = Math.Clamp(minutes, 5, 480);
        user.DefaultSlotIntervalMinutes = clamped;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return new JsonResult(new { success = false, error = "Could not update settings." }, JsonWriteOptions) { StatusCode = 500 };
        }

        return new JsonResult(new { success = true, defaultSlotIntervalMinutes = clamped }, JsonWriteOptions);
    }

    /// <summary>Full-page assign booking form (walk-in or linked customer).</summary>
    public async Task<IActionResult> OnPostAssignBookingFormAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        var slot = await _context.Slots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == AssignSlotId && s.ProviderId == user.Id);
        var dayFallback = DateOnly.FromDateTime(DateTime.Today);
        if (!DateOnly.TryParse(ScheduleInitialDate, out var dayFromField))
        {
            dayFromField = dayFallback;
        }

        var day = slot != null ? DateOnly.FromDateTime(slot.StartTime) : dayFromField;

        if (AssignSlotId <= 0 || slot == null)
        {
            ModelState.AddModelError(string.Empty, "Select a valid slot.");
        }
        else if (slot.Status != SlotStatus.Available)
        {
            ModelState.AddModelError(string.Empty, "That slot is no longer available.");
        }

        string customerName = "";
        string customerPhone = "";
        int? customerId = null;

        var sel = (AssignCustomerSelection ?? "walkin").Trim();
        if (sel == "walkin" || string.IsNullOrEmpty(sel))
        {
            customerName = (AssignWalkInName ?? "").Trim();
            customerPhone = (AssignWalkInPhone ?? "").Trim();
            customerId = null;
            if (string.IsNullOrEmpty(customerName) || string.IsNullOrEmpty(customerPhone))
            {
                ModelState.AddModelError(string.Empty, "Walk-in bookings need name and phone.");
            }
        }
        else if (!int.TryParse(sel, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var cid))
        {
            ModelState.AddModelError(string.Empty, "Invalid customer selection.");
        }
        else
        {
            customerId = cid;
            var cust = await _userManager.FindByIdAsync(cid.ToString());
            if (cust == null)
            {
                ModelState.AddModelError(string.Empty, "Customer not found.");
            }
            else
            {
                customerName = (cust.Name ?? "").Trim();
                customerPhone = (cust.PhoneNumber ?? "").Trim();
                if (string.IsNullOrEmpty(customerName) || string.IsNullOrEmpty(customerPhone))
                {
                    ModelState.AddModelError(string.Empty, "Selected account must have name and phone, or choose walk-in.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await PrepareSchedulePageAsync(user, day);
            return Page();
        }

        await using var tx = await _context.Database.BeginTransactionAsync();
        var updatedRows = await _context.Slots
            .Where(s => s.Id == AssignSlotId && s.ProviderId == user.Id && s.Status == SlotStatus.Available)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, SlotStatus.BookedByClient));
        if (updatedRows == 0)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Slot was just taken; refresh and try again.");
            await PrepareSchedulePageAsync(user, day);
            return Page();
        }

        _context.Bookings.Add(new Booking
        {
            SlotId = AssignSlotId,
            CustomerId = customerId,
            AssignedByProviderId = user.Id,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return RedirectToPage(new { date = day.ToString("yyyy-MM-dd") });
    }

    /// <summary>AJAX: book an available slot for a walk-in / phone customer from the timeline (no full reload).</summary>
    public async Task<IActionResult> OnPostAssignBookingAsync(int slotId, string? customerName, string? customerPhone)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "Unauthorized" }, JsonWriteOptions) { StatusCode = 401 };
        }

        customerName = (customerName ?? string.Empty).Trim();
        customerPhone = (customerPhone ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(customerName))
        {
            return new JsonResult(new { success = false, error = "Ім'я обов'язкове." }, JsonWriteOptions) { StatusCode = 400 };
        }

        if (string.IsNullOrEmpty(customerPhone))
        {
            return new JsonResult(new { success = false, error = "Вкажіть телефон." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var slotEntity = await _context.Slots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == slotId && s.ProviderId == user.Id);
        if (slotEntity == null)
        {
            return new JsonResult(new { success = false, error = "Слот не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        if (slotEntity.Status != SlotStatus.Available)
        {
            return new JsonResult(new { success = false, error = "Слот уже зайнято або заблоковано." }, JsonWriteOptions) { StatusCode = 409 };
        }

        await using var tx = await _context.Database.BeginTransactionAsync();

        var updatedRows = await _context.Slots
            .Where(s => s.Id == slotId && s.ProviderId == user.Id && s.Status == SlotStatus.Available)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, SlotStatus.BookedByClient));
        if (updatedRows == 0)
        {
            await tx.RollbackAsync();
            return new JsonResult(new { success = false, error = "Слот щойно зайнято. Оновіть список." }, JsonWriteOptions) { StatusCode = 409 };
        }

        _context.Bookings.Add(new Booking
        {
            SlotId = slotId,
            CustomerId = null,
            AssignedByProviderId = user.Id,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        var day = DateOnly.FromDateTime(slotEntity.StartTime);
        var refreshed = await LoadSlotsForDayAsync(user.Id, day);
        var entity = refreshed.FirstOrDefault(s => s.Id == slotId);
        if (entity == null)
        {
            return new JsonResult(new { success = true }, JsonWriteOptions);
        }

        var snap = ToSnapshotDto(entity, day.ToString("yyyy-MM-dd"));
        return new JsonResult(new { success = true, slot = snap }, JsonWriteOptions);
    }

    private async Task PrepareSchedulePageAsync(Provider user, DateOnly day)
    {
        Provider = user;
        DefaultSlotIntervalMinutes = Math.Clamp(user.DefaultSlotIntervalMinutes <= 0 ? 30 : user.DefaultSlotIntervalMinutes, 5, 480);
        BookingLinkSlug = ResolveBookingLinkSlug(user);
        ScheduleInitialDate = day.ToString("yyyy-MM-dd");
        ExistingSlots = await LoadSlotsForDayAsync(user.Id, day);
        var list = ToSnapshotList(ExistingSlots, day);
        InitialSlotsJson = JsonSerializer.Serialize(new { slots = list }, JsonWriteOptions);

        CustomersForAssign = await _userManager.Users.AsNoTracking()
            .Where(u => u.Id != user.Id)
            .OrderBy(u => u.Name)
            .Select(u => new CustomerPickVm(u.Id, $"{u.Name} · {u.Email ?? u.UserName ?? ""}"))
            .ToListAsync();

        AvailableSlotsForAssign = ExistingSlots
            .Where(s => s.Status == SlotStatus.Available)
            .Select(s => new AvailableSlotAssignVm(
                s.Id,
                $"{s.StartTime:HH:mm} – {s.EndTime:HH:mm}{(string.IsNullOrEmpty(s.ResourceContext) ? "" : $" · {s.ResourceContext}")}"))
            .ToList();
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

        if (slot.Status is SlotStatus.BookedByClient or SlotStatus.Pending)
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

    /// <summary>AJAX: set <see cref="Slot.RequiresApproval"/> for an available slot (immediate persist).</summary>
    public async Task<JsonResult> OnPostToggleSlotApprovalAsync(int id, [FromForm] bool requiresApproval)
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

        if (slot.Status != SlotStatus.Available)
        {
            return new JsonResult(new { success = false, error = "Можна змінювати лише для вільного слота." }, JsonWriteOptions) { StatusCode = 400 };
        }

        slot.RequiresApproval = requiresApproval;
        await _context.SaveChangesAsync();

        var day = DateOnly.FromDateTime(slot.StartTime);
        var snap = ToSnapshotDto(slot, day.ToString("yyyy-MM-dd"));
        return new JsonResult(new { success = true, slot = snap }, JsonWriteOptions);
    }

    /// <summary>AJAX: confirm a <see cref="SlotStatus.Pending"/> public request as booked.</summary>
    public async Task<JsonResult> OnPostApprovePendingSlotAsync(int slotId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "Unauthorized" }, JsonWriteOptions) { StatusCode = 401 };
        }

        var slot = await _context.Slots
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == slotId && s.ProviderId == user.Id);
        if (slot == null)
        {
            return new JsonResult(new { success = false, error = "Слот не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        if (slot.Status != SlotStatus.Pending || slot.Bookings.Count == 0)
        {
            return new JsonResult(new { success = false, error = "Слот не в статусі очікування." }, JsonWriteOptions) { StatusCode = 400 };
        }

        slot.Status = SlotStatus.BookedByClient;
        await _context.SaveChangesAsync();

        var day = DateOnly.FromDateTime(slot.StartTime);
        var refreshed = await LoadSlotsForDayAsync(user.Id, day);
        var entity = refreshed.FirstOrDefault(s => s.Id == slotId);
        var snap = entity != null ? ToSnapshotDto(entity, day.ToString("yyyy-MM-dd")) : ToSnapshotDto(slot, day.ToString("yyyy-MM-dd"));
        return new JsonResult(new { success = true, slot = snap }, JsonWriteOptions);
    }

    /// <summary>AJAX: decline a pending request — slot becomes available and booking rows are removed.</summary>
    public async Task<JsonResult> OnPostRejectPendingSlotAsync(int slotId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "Unauthorized" }, JsonWriteOptions) { StatusCode = 401 };
        }

        var slot = await _context.Slots
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == slotId && s.ProviderId == user.Id);
        if (slot == null)
        {
            return new JsonResult(new { success = false, error = "Слот не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        if (slot.Status != SlotStatus.Pending)
        {
            return new JsonResult(new { success = false, error = "Слот не в статусі очікування." }, JsonWriteOptions) { StatusCode = 400 };
        }

        _context.Bookings.RemoveRange(slot.Bookings);
        slot.Status = SlotStatus.Available;
        await _context.SaveChangesAsync();

        var day = DateOnly.FromDateTime(slot.StartTime);
        var refreshed = await LoadSlotsForDayAsync(user.Id, day);
        var entity = refreshed.FirstOrDefault(s => s.Id == slotId);
        var snap = entity != null ? ToSnapshotDto(entity, day.ToString("yyyy-MM-dd")) : ToSnapshotDto(slot, day.ToString("yyyy-MM-dd"));
        return new JsonResult(new { success = true, slot = snap }, JsonWriteOptions);
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

    /// <summary>Merge consecutive available slots into one block (same day, same resource).</summary>
    public async Task<IActionResult> OnPostMergeSlotsAsync([FromForm] List<int>? slotIds)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "Unauthorized" }, JsonWriteOptions) { StatusCode = 401 };
        }

        if (slotIds is null || slotIds.Count < 2)
        {
            return new JsonResult(new { success = false, error = "Оберіть щонайменше два слоти." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var distinct = slotIds.Distinct().ToList();
        var entities = await _context.Slots
            .Include(s => s.Bookings)
            .Where(s => distinct.Contains(s.Id) && s.ProviderId == user.Id)
            .ToListAsync();

        if (entities.Count != distinct.Count)
        {
            return new JsonResult(new { success = false, error = "Один або кілька слотів не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        if (entities.Any(s => s.Status != SlotStatus.Available || s.Bookings.Count > 0))
        {
            return new JsonResult(new { success = false, error = "Можна об'єднувати лише вільні слоти без бронювань." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var day = DateOnly.FromDateTime(entities[0].StartTime);
        if (entities.Any(s => DateOnly.FromDateTime(s.StartTime) != day))
        {
            return new JsonResult(new { success = false, error = "Усі слоти мають бути в один день." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var rcKey = entities[0].ResourceContext ?? string.Empty;
        if (entities.Any(s => (s.ResourceContext ?? string.Empty) != rcKey))
        {
            return new JsonResult(new { success = false, error = "Слоти мають мати однаковий ресурс (контекст)." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var ordered = entities.OrderBy(s => s.StartTime).ToList();
        for (var i = 0; i < ordered.Count - 1; i++)
        {
            if (ordered[i].EndTime != ordered[i + 1].StartTime)
            {
                return new JsonResult(new { success = false, error = "Слоти мають йти підряд без проміжків." }, JsonWriteOptions) { StatusCode = 400 };
            }
        }

        var mergedStart = ordered[0].StartTime;
        var mergedEnd = ordered[^1].EndTime;
        var inheritedRc = string.IsNullOrWhiteSpace(ordered[0].ResourceContext)
            ? null
            : ordered[0].ResourceContext!.Trim();

        await using var tx = await _context.Database.BeginTransactionAsync();
        _context.Slots.RemoveRange(ordered);
        await _context.SaveChangesAsync();

        var newSlot = new TimeSlot
        {
            ProviderId = user.Id,
            StartTime = mergedStart,
            EndTime = mergedEnd,
            Status = SlotStatus.Available,
            ResourceContext = inheritedRc,
            IsGrouped = true,
            RequiresApproval = ordered.Any(s => s.RequiresApproval),
        };
        _context.Slots.Add(newSlot);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        var refreshed = await LoadSlotsForDayAsync(user.Id, day);
        var list = ToSnapshotList(refreshed, day);
        return new JsonResult(new { success = true, slots = list }, JsonWriteOptions);
    }

    /// <summary>Split one merged (<see cref="Slot.IsGrouped"/>) available slot into segments; optional <paramref name="minutes"/> overrides the provider default for this request.</summary>
    public async Task<IActionResult> OnPostSplitSlotAsync(int slotId, [FromForm] int? minutes = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new JsonResult(new { success = false, error = "Unauthorized" }, JsonWriteOptions) { StatusCode = 401 };
        }

        var fallback = Math.Clamp(user.DefaultSlotIntervalMinutes <= 0 ? 30 : user.DefaultSlotIntervalMinutes, 5, 480);
        var chunkMinutes = minutes.HasValue ? Math.Clamp(minutes.Value, 5, 480) : fallback;
        const int minDurationMinutes = 5;

        var slot = await _context.Slots
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(s => s.Id == slotId && s.ProviderId == user.Id);

        if (slot == null)
        {
            return new JsonResult(new { success = false, error = "Слот не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        if (!slot.IsGrouped)
        {
            return new JsonResult(new { success = false, error = "Розбити можна лише згрупований слот (після об’єднання)." }, JsonWriteOptions) { StatusCode = 400 };
        }

        if (slot.Status != SlotStatus.Available || slot.Bookings.Count > 0)
        {
            return new JsonResult(new { success = false, error = "Можна розбивати лише вільні слоти." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var totalMinutes = (int)(slot.EndTime - slot.StartTime).TotalMinutes;
        if (totalMinutes < minDurationMinutes)
        {
            return new JsonResult(new { success = false, error = "Слот занадто короткий." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var day = DateOnly.FromDateTime(slot.StartTime);
        var newPieces = new List<TimeSlot>();
        var cur = slot.StartTime;
        var end = slot.EndTime;

        while (cur < end)
        {
            var next = cur.AddMinutes(chunkMinutes);
            if (next > end)
            {
                next = end;
            }

            var len = (int)(next - cur).TotalMinutes;
            if (len < minDurationMinutes && newPieces.Count > 0)
            {
                newPieces[^1].EndTime = end;
                break;
            }

            if (len >= minDurationMinutes)
            {
                newPieces.Add(new TimeSlot
                {
                    ProviderId = user.Id,
                    StartTime = cur,
                    EndTime = next,
                    Status = SlotStatus.Available,
                    ResourceContext = string.IsNullOrWhiteSpace(slot.ResourceContext) ? null : slot.ResourceContext.Trim(),
                    IsGrouped = false,
                    RequiresApproval = slot.RequiresApproval,
                });
                cur = next;
            }
            else
            {
                newPieces.Add(new TimeSlot
                {
                    ProviderId = user.Id,
                    StartTime = cur,
                    EndTime = end,
                    Status = SlotStatus.Available,
                    ResourceContext = string.IsNullOrWhiteSpace(slot.ResourceContext) ? null : slot.ResourceContext.Trim(),
                    IsGrouped = false,
                    RequiresApproval = slot.RequiresApproval,
                });
                break;
            }
        }

        if (newPieces.Count == 0)
        {
            return new JsonResult(new { success = false, error = "Не вдалося розбити слот." }, JsonWriteOptions) { StatusCode = 400 };
        }

        await using var tx = await _context.Database.BeginTransactionAsync();
        _context.Slots.Remove(slot);
        await _context.SaveChangesAsync();
        _context.Slots.AddRange(newPieces);
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        var refreshed = await LoadSlotsForDayAsync(user.Id, day);
        var list = ToSnapshotList(refreshed, day);
        return new JsonResult(new { success = true, slots = list }, JsonWriteOptions);
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
            var day = DateOnly.FromDateTime(DateTime.Today);
            if (DateOnly.TryParse(ScheduleInitialDate, out var pd))
            {
                day = pd;
            }

            await PrepareSchedulePageAsync(user, day);
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
                IsGrouped = false,
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

        TempData["AdminSlotsSaved"] = "1";
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
            b?.CustomerPhone,
            s.IsGrouped,
            s.RequiresApproval);
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
        string? ClientPhone,
        bool IsGrouped,
        bool RequiresApproval);

    private sealed class SlotDraftJson
    {
        public string Start { get; set; } = string.Empty;
        public string End { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string? ResourceContext { get; set; }
    }

    public sealed record CustomerPickVm(int Id, string Display);

    public sealed record AvailableSlotAssignVm(int SlotId, string Label);
}
