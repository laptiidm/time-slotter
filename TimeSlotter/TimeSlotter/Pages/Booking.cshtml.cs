using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
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

    public BookingModel(AppDbContext context)
    {
        _context = context;
    }

    public Provider? Provider { get; set; }
    public List<Slot> DaySlots { get; set; } = new();
    public DateTime SelectedDate { get; set; }
    public string DateYmd { get; set; } = "";
    /// <summary>Raw slug from route (for building URLs).</summary>
    public string SlugRoute { get; set; } = "";

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

        var dayStart = DateTime.SpecifyKind(SelectedDate.Date, DateTimeKind.Unspecified);
        var dayEndExclusive = dayStart.AddDays(1);

        DaySlots = await _context.Slots
            .AsNoTracking()
            .Where(s => s.ProviderId == provider.Id && s.StartTime >= dayStart && s.StartTime < dayEndExclusive)
            .OrderBy(s => s.StartTime)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostBookAsync(string? slug, int slotId, string? name, string? phone)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return new JsonResult(new { success = false, error = "Invalid provider." }, JsonWriteOptions) { StatusCode = 400 };
        }

        name = (name ?? string.Empty).Trim();
        phone = (phone ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone))
        {
            return new JsonResult(new { success = false, error = "Вкажіть ім'я та телефон." }, JsonWriteOptions) { StatusCode = 400 };
        }

        var normalizedSlug = slug.TrimStart('@');
        normalizedSlug = string.IsNullOrEmpty(normalizedSlug) ? slug : "@" + normalizedSlug;

        var provider = await _context.Users.FirstOrDefaultAsync(u => u.Slug == normalizedSlug || u.Slug == slug);
        if (provider == null)
        {
            return new JsonResult(new { success = false, error = "Provider not found." }, JsonWriteOptions) { StatusCode = 404 };
        }

        await using var tx = await _context.Database.BeginTransactionAsync();
        var slot = await _context.Slots.FirstOrDefaultAsync(s => s.Id == slotId && s.ProviderId == provider.Id);
        if (slot == null)
        {
            await tx.RollbackAsync();
            return new JsonResult(new { success = false, error = "Слот не знайдено." }, JsonWriteOptions) { StatusCode = 404 };
        }

        if (slot.Status != SlotStatus.Available)
        {
            await tx.RollbackAsync();
            return new JsonResult(new { success = false, error = "Цей слот уже зайнято." }, JsonWriteOptions) { StatusCode = 409 };
        }

        slot.Status = SlotStatus.BookedByClient;
        _context.Bookings.Add(new Booking
        {
            SlotId = slot.Id,
            CustomerName = name,
            CustomerPhone = phone,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return new JsonResult(new { success = true }, JsonWriteOptions);
    }
}
