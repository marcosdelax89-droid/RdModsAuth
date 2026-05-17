using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BelgaAuthAPI.Pages
{
    [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "AdminCookie")]
    public class LicensesModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
