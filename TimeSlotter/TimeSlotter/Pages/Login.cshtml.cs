using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

public class LoginModel : PageModel
{
    private readonly UserManager<Provider> _userManager;
    private readonly SignInManager<Provider> _signInManager;

    public LoginModel(UserManager<Provider> userManager, SignInManager<Provider> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        // Allow signing in by either email or username (UserName is stored without leading @).
        var key = Input.EmailOrUsername.Trim();
        var user = await _userManager.FindByEmailAsync(key);
        user ??= await _userManager.FindByNameAsync(key.TrimStart('@'));

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email/username or password.");
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(user, Input.Password, isPersistent: false, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return RedirectToPage("/Admin");
        }

        ModelState.AddModelError(string.Empty, "Invalid email/username or password.");
        return Page();
    }

    public class LoginInput
    {
        [Required]
        public string EmailOrUsername { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
