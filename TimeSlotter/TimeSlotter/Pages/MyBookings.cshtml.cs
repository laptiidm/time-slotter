using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TimeSlotter.Data;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

[Authorize]
public class MyBookingsModel : PageModel
{
    private readonly AppDbContext _context;
    private readonly UserManager<Provider> _userManager;

    public MyBookingsModel(AppDbContext context, UserManager<Provider> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IReadOnlyList<MyBookingRowVm> Bookings { get; private set; } = Array.Empty<MyBookingRowVm>();

    public static string SlotStatusUk(SlotStatus status) =>
        status switch
        {
            SlotStatus.Available => "Доступно",
            SlotStatus.Pending => "Очікує підтвердження",
            SlotStatus.BookedByClient => "Зайнято",
            SlotStatus.ReservedByAdmin => "Заблоковано",
            _ => status.ToString(),
        };

    public async Task<IActionResult> OnGetAsync()
    {
        var me = await _userManager.GetUserAsync(User);
        if (me == null)
        {
            return Unauthorized();
        }

        Bookings = await _context.Bookings
            .AsNoTracking()
            .Where(b => b.CustomerId == me.Id)
            .OrderByDescending(b => b.Slot.StartTime)
            .Select(b => new MyBookingRowVm(
                b.Id,
                b.Slot.StartTime,
                b.Slot.EndTime,
                b.Slot.Provider.Name,
                b.Slot.Provider.Slug,
                b.CustomerName,
                b.CustomerPhone,
                b.Slot.Status))
            .ToListAsync();

        return Page();
    }

    public sealed record MyBookingRowVm(
        int BookingId,
        DateTime Start,
        DateTime End,
        string ProviderName,
        string ProviderSlug,
        string CustomerName,
        string CustomerPhone,
        SlotStatus SlotStatus);
}
