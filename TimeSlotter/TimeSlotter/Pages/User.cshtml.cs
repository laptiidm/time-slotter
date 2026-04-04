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
    public Provider Provider { get; set; } = null!;
    public List<Slot> AvailableSlots { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string slug, DateTime? date, string? resource)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return NotFound();
        }

        var provider = await _context.Users.FirstOrDefaultAsync(u => u.Slug == slug);
        if (provider == null)
        {
            return NotFound();
        }

        Provider = provider;
        SelectedDate = date?.Date ?? DateTime.Today;
        SelectedResource = resource;

        var query = _context.Slots
            .Include(s => s.Provider)
            .Where(s => s.Provider.Slug == slug && s.StartTime.Date == SelectedDate.Date);

        if (!string.IsNullOrEmpty(resource))
        {
            query = query.Where(s => s.ResourceContext == resource);
        }

        AvailableSlots = await query.OrderBy(s => s.StartTime).ToListAsync();
        return Page();
    }
}
