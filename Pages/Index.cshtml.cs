using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BelgaAuthAPI.Pages
{
    [Authorize(Policy = "AdminOnly")]
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}

