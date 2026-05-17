using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BelgaAuthAPI.Pages
{
    [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
    public class ResellerDashboardModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Verificar se está autenticado como revendedor
            if (!User.Identity?.IsAuthenticated == true || !User.HasClaim("IsReseller", "true"))
            {
                return RedirectToPage("/Login");
            }
            return Page();
        }
    }
}

