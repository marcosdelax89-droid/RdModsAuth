using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BelgaAuthAPI.Pages
{
    public class ResellerLogoutModel : PageModel
    {
        public async Task<IActionResult> OnGetAsync()
        {
            await HttpContext.SignOutAsync("ResellerCookie");
            return RedirectToPage("/Login");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await HttpContext.SignOutAsync("ResellerCookie");
            return RedirectToPage("/Login");
        }
    }
}




