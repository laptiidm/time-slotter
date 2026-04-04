using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TimeSlotter.Pages;

[AllowAnonymous]
public class UserModel : PageModel
{
    public void OnGet()
    {
    }
}
