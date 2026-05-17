using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BelgaAuthAPI.Pages
{
    public class AdminLogoutModel : PageModel
    {
        public async Task<IActionResult> OnGetAsync()
        {
            await HttpContext.SignOutAsync("AdminCookie");
            return RedirectToPage("/Login", new { type = "admin" });
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await HttpContext.SignOutAsync("AdminCookie");
            return RedirectToPage("/Login", new { type = "admin" });
        }
    }
}




