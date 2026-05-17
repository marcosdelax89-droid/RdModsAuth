using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BelgaAuthAPI.Pages
{
    [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
    public class ResellerLicensesModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
