using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TimeSlotter.Data;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

[AllowAnonymous]
public class UserModel : PageModel
{
    private readonly AppDbContext _context;

    public UserModel(AppDbContext context)
    {
        _context = context;
    }

    public DateTime SelectedDate { get; set; }
    public string? SelectedResource { get; set; }
    public string ProviderSlug { get; set; } = string.Empty;
    public Provider Provider { get; set; } = null!;
    public List<Slot> AvailableSlots { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug, DateTime? date, string? resource)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var normalizedSlug = slug.TrimStart('@');
        normalizedSlug = string.IsNullOrEmpty(normalizedSlug) ? slug : "@" + normalizedSlug;

        var provider = await _context.Users.FirstOrDefaultAsync(u => u.Slug == normalizedSlug || u.Slug == slug);
        if (provider == null)
        {
            return NotFound();
        }

        Provider = provider;
        ProviderSlug = slug;
        SelectedDate = date?.Date ?? DateTime.Today;
        SelectedResource = resource;

        var query = _context.Slots
            .Include(s => s.Provider)
            .Where(s => s.ProviderId == provider.Id
                        && s.StartTime.Date == SelectedDate.Date
                        && s.Status == SlotStatus.Available);

        if (!string.IsNullOrEmpty(resource))
        {
            query = query.Where(s => s.ResourceContext == resource);
        }

        AvailableSlots = await query.OrderBy(s => s.StartTime).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostBookAsync(string slug, int slotId, string customerName, string customerPhone, DateTime? date, string? resource)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var normalizedSlug = slug.TrimStart('@');
        normalizedSlug = string.IsNullOrEmpty(normalizedSlug) ? slug : "@" + normalizedSlug;

        var provider = await _context.Users.FirstOrDefaultAsync(u => u.Slug == normalizedSlug || u.Slug == slug);
        if (provider == null)
        {
            return NotFound();
        }

        customerName = (customerName ?? string.Empty).Trim();
        customerPhone = (customerPhone ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(customerName) || string.IsNullOrEmpty(customerPhone))
        {
            return RedirectToPage(new { slug, date = (date ?? DateTime.Today).ToString("yyyy-MM-dd"), resource });
        }

        await using var tx = await _context.Database.BeginTransactionAsync();
        var slot = await _context.Slots.FirstOrDefaultAsync(s => s.Id == slotId && s.ProviderId == provider.Id);
        if (slot == null || slot.Status != SlotStatus.Available)
        {
            await tx.RollbackAsync();
            return RedirectToPage(new { slug, date = (date ?? DateTime.Today).ToString("yyyy-MM-dd"), resource });
        }

        slot.Status = SlotStatus.BookedByClient;
        _context.Bookings.Add(new Booking
        {
            SlotId = slot.Id,
            CustomerName = customerName,
            CustomerPhone = customerPhone,
            CreatedAt = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return RedirectToPage(new { slug, date = slot.StartTime.Date.ToString("yyyy-MM-dd"), resource });
    }
}
