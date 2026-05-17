using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BelgaAuthAPI.Pages
{
    [Authorize(Policy = "AdminOnly")]
    public class EditResellerModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}




