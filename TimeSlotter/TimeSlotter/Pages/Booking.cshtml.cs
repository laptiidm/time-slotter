using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TimeSlotter.Data;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

[AllowAnonymous]
public class BookingModel : PageModel
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AppDbContext _context;
    private readonly UserManager<Provider> _userManager;

    public BookingModel(AppDbContext context, UserManager<Provider> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public Provider? Provider { get; set; }
    public List<Slot> DaySlots { get; set; } = new();
    public DateTime SelectedDate { get; set; }
    public string DateYmd { get; set; } = "";
    /// <summary>Selected day for display (e.g. «14 квітня 2026 р.»).</summary>
    public string TitleDateUk { get; set; } = "";
    /// <summary>Raw slug from route (for building URLs).</summary>
    public string SlugRoute { get; set; } = "";

    /// <summary>True when the signed-in user is in the <c>Admin</c> role (site-wide).</summary>
    public bool IsSiteAdmin { get; set; }

    /// <summary>Slots on this day that are <see cref="SlotStatus.Available"/> (public bookable count).</summary>
    public int PublicAvailableSlotCount { get; set; }

    /// <summary>When signed in, booking can link to this account (<see cref="OnPostBookAsync"/> sets <see cref="Booking.CustomerId"/>).</summary>
    public bool IsAuthenticatedBooker { get; set; }

    public string? PrefillCustomerName { get; set; }
    public string? PrefillCustomerPhone { get; set; }

    public async Task<IActionResult> OnGetAsync(string? slug, DateTime? date)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = Request.Query["slug"].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            return Page();
        }

        var normalizedSlug = slug.TrimStart('@');
        normalizedSlug = string.IsNullOrEmpty(normalizedSlug) ? slug : "@" + normalizedSlug;

        var provider = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == normalizedSlug || u.Slug == slug);
        if (provider == null)
        {
            return NotFound();
        }

        Provider = provider;
        SlugRoute = slug.Trim();
        SelectedDate = date?.Date ?? DateTime.Today;
        DateYmd = SelectedDate.ToString("yyyy-MM-dd");
        var uk = new CultureInfo("uk-UA");
        TitleDateUk = SelectedDate.ToString("d MMMM yyyy", uk) + " р.";

        var dayStart = DateTime.SpecifyKind(SelectedDate.Date, DateTimeKind.Unspecified);
        var dayEndExclusive = dayStart.AddDays(1);

        DaySlots = await _context.Slots
            .AsNoTracking()
            .Where(s => s.ProviderId == provider.Id && s.StartTime >= dayStart && s.StartTime < dayEndExclusive)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        IsSiteAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
        PublicAvailableSlotCount = DaySlots.Count(s => s.Status == SlotStatus.Available);

        if (User.Identity?.IsAuthenticated == true)
        {
            var me = await _userManager.GetUserAsync(User);
            if (me != null)
            {
                IsAuthenticatedBooker = true;
                PrefillCustomerName = me.Name;
                PrefillCustomerPhone = me.PhoneNumber ?? string.Empty;
            }
        }

        return Page();
    }

    public async Task<IActionResult> OnGetSlotStatusesAsync(string? slug, string? date)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = Request.Query["slug"].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            return new JsonResult(new { error = "Некоректне посилання провайдера." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var normalizedSlug = slug.TrimStart('@');
        normalizedSlug = string.IsNullOrEmpty(normalizedSlug) ? slug : "@" + normalizedSlug;

        var provider = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == normalizedSlug || u.Slug == slug);
        if (provider == null)
        {
            return new JsonResult(new { error = "Провайдера не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        if (string.IsNullOrWhiteSpace(date))
        {
            date = Request.Query["date"].FirstOrDefault();
        }

        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dayParsed))
        {
            dayParsed = DateTime.Today;
        }

        var dayStart = DateTime.SpecifyKind(dayParsed.Date, DateTimeKind.Unspecified);
        var dayEndExclusive = dayStart.AddDays(1);

        var slots = await _context.Slots
            .AsNoTracking()
            .Where(s => s.ProviderId == provider.Id && s.StartTime >= dayStart && s.StartTime < dayEndExclusive)
            .OrderBy(s => s.StartTime)
            .Select(s => new
            {
                id = s.Id,
                status = SlotStatusApiToken(s.Status),
                requiresApproval = s.Status == SlotStatus.Available && s.RequiresApproval,
            })
            .ToListAsync();

        return new JsonResult(new { slots }, JsonWriteOptions);
    }

    /// <summary>JSON day schedule for client-side date changes (no full page reload).</summary>
    public async Task<IActionResult> OnGetDayScheduleAsync(string? slug, string? date)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = Request.Query["slug"].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            return new JsonResult(new { error = "Некоректне посилання провайдера." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var normalizedSlug = slug.TrimStart('@');
        normalizedSlug = string.IsNullOrEmpty(normalizedSlug) ? slug : "@" + normalizedSlug;

        var provider = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == normalizedSlug || u.Slug == slug);
        if (provider == null)
        {
            return new JsonResult(new { error = "Провайдера не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        if (string.IsNullOrWhiteSpace(date))
        {
            date = Request.Query["date"].FirstOrDefault();
        }

        if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dayParsed))
        {
            dayParsed = DateTime.Today;
        }

        var dayStart = DateTime.SpecifyKind(dayParsed.Date, DateTimeKind.Unspecified);
        var dayEndExclusive = dayStart.AddDays(1);

        var rawSlots = await _context.Slots
            .AsNoTracking()
            .Where(s => s.ProviderId == provider.Id && s.StartTime >= dayStart && s.StartTime < dayEndExclusive)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        var uk = new CultureInfo("uk-UA");
        var dateYmd = dayParsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var titleDateUk = dayParsed.ToString("d MMMM yyyy", uk) + " р.";
        var publicAvailableSlotCount = rawSlots.Count(s => s.Status == SlotStatus.Available);

        var slots = rawSlots.Select(s => new
        {
            id = s.Id,
            start = s.StartTime.ToString("HH:mm"),
            end = s.EndTime.ToString("HH:mm"),
            resourceContext = s.ResourceContext,
            status = SlotStatusApiToken(s.Status),
            requiresApproval = s.Status == SlotStatus.Available && s.RequiresApproval,
        }).ToList();

        return new JsonResult(new
        {
            dateYmd,
            titleDateUk,
            publicAvailableSlotCount,
            totalCount = slots.Count,
            slots,
        }, JsonWriteOptions);
    }

    public async Task<IActionResult> OnPostBookAsync(string? slug, int slotId, string? name, string? phone)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return new JsonResult(new { success = false, error = "Некоректне посилання провайдера." }, JsonWriteOptions) { StatusCode = 400 };
        }

        name = (name ?? string.Empty).Trim();
        phone = (phone ?? string.Empty).Trim();

        int? customerId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var me = await _userManager.GetUserAsync(User);
            customerId = me?.Id;
            if (me != null)
            {
                if (string.IsNullOrEmpty(name))
                {
                    name = me.Name?.Trim() ?? string.Empty;
                }

                if (string.IsNullOrEmpty(phone))
                {
                    phone = me.PhoneNumber?.Trim() ?? string.Empty;
                }
            }
        }

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone))
        {
            return new JsonResult(new { success = false, error = "Вкажіть ім'я та телефон (або заповніть їх у профілі)." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var normalizedSlug = slug.TrimStart('@');
        normalizedSlug = string.IsNullOrEmpty(normalizedSlug) ? slug : "@" + normalizedSlug;

        var provider = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Slug == normalizedSlug || u.Slug == slug);
        if (provider == null)
        {
            return new JsonResult(new { success = false, error = "Провайдера не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        await using var tx = await _context.Database.BeginTransactionAsync();

        var slotExists = await _context.Slots.AnyAsync(s => s.Id == slotId && s.ProviderId == provider.Id);
        if (!slotExists)
        {
            await tx.RollbackAsync();
            return new JsonResult(new { success = false, error = "Слот не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        var updatedRows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE Slots
SET Status = CASE WHEN RequiresApproval = 1 THEN {(int)SlotStatus.Pending} ELSE {(int)SlotStatus.BookedByClient} END
WHERE Id = {slotId} AND ProviderId = {provider.Id} AND Status = {(int)SlotStatus.Available}");

        if (updatedRows == 0)
        {
            await tx.RollbackAsync();
            return new JsonResult(new
            {
                message = "SlotAlreadyBooked",
                success = false,
                error = "Цей слот щойно зайняли. Оберіть інший час.",
            }, JsonWriteOptions)
            {
                StatusCode = StatusCodes.Status409Conflict,
            };
        }

        var pendingApproval = await _context.Slots
            .AsNoTracking()
            .Where(s => s.Id == slotId)
            .Select(s => s.Status == SlotStatus.Pending)
            .FirstAsync();

        _context.Bookings.Add(new Booking
        {
            SlotId = slotId,
            CustomerId = customerId,
            AssignedByProviderId = null,
            CustomerName = name,
            CustomerPhone = phone,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return new JsonResult(new { success = true, pendingApproval }, JsonWriteOptions);
    }

    private static string SlotStatusApiToken(SlotStatus status) =>
        status switch
        {
            SlotStatus.Available => "available",
            SlotStatus.Pending => "pending",
            SlotStatus.BookedByClient => "booked",
            SlotStatus.ReservedByAdmin => "reserved",
            _ => "reserved", // future enum values / raw DB ints
        };
}
