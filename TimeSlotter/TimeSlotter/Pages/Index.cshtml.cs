using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeSlotter.Data;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

/// <summary>
/// Login landing. Site <c>Admin</c> JSON: POST <c>/Index?handler=ReserveSlot</c> / <c>ReleaseSlot</c> (see <see cref="OnPostReserveSlotAsync"/>).
/// </summary>
[AllowAnonymous]
public class IndexModel : PageModel
{
    private readonly UserManager<Provider> _userManager;
    private readonly SignInManager<Provider> _signInManager;
    private readonly AppDbContext _context;

    public IndexModel(
        UserManager<Provider> userManager,
        SignInManager<Provider> signInManager,
        AppDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    /// <summary>
    /// Admin role only (enforced in <see cref="AdminSlotRoleHandlers"/>). Razor Pages cannot use <c>[Authorize(Roles = "Admin")]</c> on this handler.
    /// </summary>
    public Task<IActionResult> OnPostReserveSlotAsync(int slotId) =>
        AdminSlotRoleHandlers.ReserveSlotAsync(User, _context, slotId);

    /// <summary>Admin role only: clear <see cref="SlotStatus.ReservedByAdmin"/>.</summary>
    public Task<IActionResult> OnPostReleaseSlotAsync(int slotId) =>
        AdminSlotRoleHandlers.ReleaseSlotAsync(User, _context, slotId);

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
