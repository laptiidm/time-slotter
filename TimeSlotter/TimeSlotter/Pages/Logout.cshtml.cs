using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

[Authorize]
public class LogoutModel : PageModel
{
    private readonly SignInManager<Provider> _signInManager;

    public LogoutModel(SignInManager<Provider> signInManager)
    {
        _signInManager = signInManager;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await _signInManager.SignOutAsync();
        return RedirectToPage("/Login");
    }
}

