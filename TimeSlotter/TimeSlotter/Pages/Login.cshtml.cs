using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TimeSlotter.Pages
{
    public class LoginModel : PageModel
    {
        public void OnGet()
        {
        }

        /// <summary>
        /// UI mockup only: no server-side authentication or validation yet.
        /// HTML5 <c>required</c> runs in the browser before this runs.
        /// </summary>
        public IActionResult OnPost()
        {
            return RedirectToPage("/Admin");
        }
    }
}
