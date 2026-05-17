using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using BelgaAuthAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace BelgaAuthAPI.Pages
{
    public class ResellerLoginModel : PageModel
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<ResellerLoginModel> _logger;

        public ResellerLoginModel(AuthDbContext context, ILogger<ResellerLoginModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public bool RememberMe { get; set; }

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            return RedirectToPage("/Login");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Username e senha são obrigatórios";
                return Page();
            }

            try
            {
                // Verificar se o revendedor existe
                var reseller = await _context.Resellers
                    .FirstOrDefaultAsync(r => r.Username == Username);

                if (reseller == null)
                {
                    _logger.LogWarning("Tentativa de login com username não encontrado: {Username}", Username);
                    ErrorMessage = $"Revendedor '{Username}' não encontrado. Verifique se o username está correto ou se o revendedor foi criado.";
                    return Page();
                }

                // Verificar se está ativo
                if (!reseller.IsActive)
                {
                    _logger.LogWarning("Tentativa de login com conta desativada: {Username}", Username);
                    ErrorMessage = "Sua conta de revendedor está desativada. Entre em contato com o administrador.";
                    return Page();
                }

                // Verificar senha
                if (!BCrypt.Net.BCrypt.Verify(Password, reseller.PasswordHash))
                {
                    _logger.LogWarning("Senha incorreta para revendedor: {Username}", Username);
                    ErrorMessage = "Senha incorreta. Verifique se digitou a senha correta.";
                    return Page();
                }

            // Atualizar último login
            reseller.LastLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Criar claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, reseller.Username),
                new Claim(ClaimTypes.NameIdentifier, reseller.Id.ToString()),
                new Claim("IsReseller", "true"),
                new Claim("ResellerId", reseller.Id.ToString()),
                new Claim("ResellerUsername", reseller.Username)
            };

            var claimsIdentity = new ClaimsIdentity(claims, "ResellerCookie");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(24)
            };

            await HttpContext.SignInAsync(
                "ResellerCookie",
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

                _logger.LogInformation("Reseller {Username} logged in", Username);

                return RedirectToPage("/ResellerDashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer login de revendedor: {Username}", Username);
                ErrorMessage = $"Erro ao processar login: {ex.Message}";
                return Page();
            }
        }
    }
}

