using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BelgaAuthAPI.Pages
{
    [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
    public class ResellerCreditsModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
