using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

[Authorize]
public class AdminModel : PageModel
{
    private readonly UserManager<Provider> _userManager;

    public AdminModel(UserManager<Provider> userManager)
    {
        _userManager = userManager;
    }

    public Provider Provider { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        Provider = user;
        return Page();
    }
}
