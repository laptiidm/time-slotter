using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TimeSlotter.Models;

namespace TimeSlotter.Pages;

public class RegisterModel : PageModel
{
    private readonly UserManager<Provider> _userManager;
    private readonly SignInManager<Provider> _signInManager;
    private readonly RoleManager<IdentityRole<int>> _roleManager;

    public RegisterModel(
        UserManager<Provider> userManager,
        SignInManager<Provider> signInManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
    }

    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Simple UX: we rely mostly on HTML5 + Identity password rules.
        if (!ModelState.IsValid)
            return Page();

        Input.Nickname = Slugify(Input.Nickname);
        Input.FullName = Input.FullName.Trim();

        if (string.IsNullOrWhiteSpace(Input.Nickname))
        {
            ModelState.AddModelError(string.Empty, "Nickname is required.");
            return Page();
        }

        // Identity UserName is normalized without @; public Slug is stored with a single @ prefix.
        var handle = Input.Nickname.TrimStart('@');
        var slugWithAt = "@" + handle;

        // Since we store UserName = handle, this checks uniqueness quickly.
        var existing = await _userManager.FindByNameAsync(handle);
        if (existing != null)
        {
            ModelState.AddModelError(string.Empty, "That nickname is already taken.");
            return Page();
        }

        var user = new Provider
        {
            Email = Input.Email.Trim(),
            UserName = handle,
            Name = Input.FullName,
            Slug = slugWithAt,
            Bio = string.IsNullOrWhiteSpace(Input.Description) ? null : Input.Description.Trim(),
        };

        var createResult = await _userManager.CreateAsync(user, Input.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        if (await _roleManager.RoleExistsAsync("User"))
        {
            await _userManager.AddToRoleAsync(user, "User");
        }

        // High-UX: sign them in immediately.
        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToPage("/Admin");
    }

    private static string Slugify(string value)
    {
        value = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0) return string.Empty;

        // Replace runs of non-alphanumeric with hyphens, then trim hyphens.
        value = System.Text.RegularExpressions.Regex.Replace(value, @"[^a-z0-9]+", "-");
        return value.Trim('-');
    }

    public class RegisterInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Nickname { get; set; } = string.Empty;

        public string? Description { get; set; }
    }
}

