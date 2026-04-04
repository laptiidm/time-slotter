using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly UserManager<Provider> _userManager;
    private readonly SignInManager<Provider> _signInManager;

    public IndexModel(UserManager<Provider> userManager, SignInManager<Provider> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public bool RememberMe { get; set; }

    public Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Task.FromResult<IActionResult>(RedirectToPage("/Admin"));
        }

        return Task.FromResult<IActionResult>(Page());
    }

    public async Task<IActionResult> OnPostAsync(string email, string password, bool rememberMe)
    {
        Email = email.Trim();
        Password = password;
        RememberMe = rememberMe;

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Admin");
        }

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(password))
        {
            AddLoginValidationErrors();
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Email);
        if (user is null)
        {
            AddLoginValidationErrors();
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return RedirectToPage("/Admin");
        }

        AddLoginValidationErrors();
        return Page();
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return RedirectToPage("/Index");
    }

    private void AddLoginValidationErrors()
    {
        ModelState.AddModelError(string.Empty, "Invalid login attempt");
        ModelState.AddModelError(nameof(Email), string.Empty);
        ModelState.AddModelError(nameof(Password), string.Empty);
    }
}
