using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BelgaAuthAPI.Pages
{
    [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
    public class ResellerCreateUserModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (!User.Identity?.IsAuthenticated == true || !User.HasClaim("IsReseller", "true"))
            {
                return RedirectToPage("/Login");
            }
            return Page();
        }
    }
}

