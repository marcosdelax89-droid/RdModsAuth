using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using BelgaAuthAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace BelgaAuthAPI.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(AuthDbContext context, ILogger<LoginModel> logger)
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

        [BindProperty]
        public string LoginType { get; set; } = "customer"; // customer, reseller, admin

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            // Se já estiver logado, redireciona para a página apropriada
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.HasClaim("IsAdmin", "true"))
                {
                    return RedirectToPage("/Index");
                }
                else if (User.HasClaim("IsReseller", "true"))
                {
                    return RedirectToPage("/ResellerDashboard");
                }
                else if (User.HasClaim("IsCustomer", "true"))
                {
                    return RedirectToPage("/UserDashboard");
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            _logger.LogInformation("Tentativa de login via página unificada - Username: {Username}, Tipo: {LoginType}", Username, LoginType);

            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Usuário e senha são obrigatórios";
                return Page();
            }

            try
            {
                // 1. FLUXO LOGIN ADMINISTRADOR
                if (LoginType == "admin")
                {
                    _logger.LogInformation("Verificando credenciais de Administrador para '{Username}'", Username);
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == Username);

                    if (user == null || !BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash))
                    {
                        ErrorMessage = "Usuário ou senha incorretos";
                        return Page();
                    }

                    if (!user.IsAdmin)
                    {
                        ErrorMessage = "Sua conta não possui privilégios de administrador.";
                        return Page();
                    }

                    // Criar claims de Admin
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim("IsAdmin", "true")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, "AdminCookie");
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = RememberMe,
                        ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(24)
                    };

                    await HttpContext.SignInAsync(
                        "AdminCookie",
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    _logger.LogInformation("✅ Administrador {Username} logado com sucesso, redirecionando para /Index", Username);
                    return RedirectToPage("/Index");
                }

                // 2. FLUXO LOGIN REVENDEDOR
                if (LoginType == "reseller")
                {
                    _logger.LogInformation("Verificando se '{Username}' é um revendedor...", Username);
                    var reseller = await _context.Resellers.FirstOrDefaultAsync(r => r.Username == Username);

                    if (reseller == null || !BCrypt.Net.BCrypt.Verify(Password, reseller.PasswordHash))
                    {
                        ErrorMessage = "Usuário ou senha incorretos";
                        return Page();
                    }

                    if (!reseller.IsActive)
                    {
                        ErrorMessage = "Sua conta de revendedor está desativada. Entre em contato com o administrador.";
                        return Page();
                    }

                    // Atualizar último login
                    reseller.LastLogin = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    // Criar claims de revendedor
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, reseller.Username),
                        new Claim(ClaimTypes.NameIdentifier, reseller.Id.ToString()),
                        new Claim("IsReseller", "true"),
                        new Claim("ResellerId", reseller.Id.ToString())
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

                    _logger.LogInformation("✅ Revendedor {Username} logado com sucesso, redirecionando para /ResellerDashboard", Username);
                    return RedirectToPage("/ResellerDashboard");
                }

                // 3. FLUXO LOGIN CLIENTE (PADRÃO)
                _logger.LogInformation("Verificando se '{Username}' é usuário normal...", Username);
                var clientUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == Username);

                if (clientUser == null || !BCrypt.Net.BCrypt.Verify(Password, clientUser.PasswordHash))
                {
                    ErrorMessage = "Usuário ou senha incorretos";
                    return Page();
                }

                if (clientUser.IsBanned)
                {
                    ErrorMessage = $"Sua conta está banida: {clientUser.BanReason ?? "Sem motivo especificado"}";
                    return Page();
                }

                // Verificar se o usuário tem assinaturas
                var subscriptions = await _context.Subscriptions
                    .Where(s => s.UserId == clientUser.Id)
                    .ToListAsync();

                // Permitir login mesmo se expirado ou pausado (o painel do cliente mostrará o status correto)
                if (!subscriptions.Any())
                {
                    ErrorMessage = "Sua conta não possui nenhuma assinatura vinculada.";
                    return Page();
                }

                // 🔒 IP Lock — o painel web só pode ser acessado a partir do IP registrado
                var currentIP = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                // Normalizar IPv6 loopback para IPv4
                if (currentIP == "::1") currentIP = "127.0.0.1";

                if (!string.IsNullOrEmpty(clientUser.IP))
                {
                    if (clientUser.IP != currentIP)
                    {
                        _logger.LogWarning("🔒 IP Lock bloqueou login de '{Username}': IP registrado={RegisteredIP}, IP atual={CurrentIP}", Username, clientUser.IP, currentIP);
                        ErrorMessage = $"Acesso negado. Este painel só pode ser acessado a partir do IP registrado ({clientUser.IP}). Entre em contato com o suporte.";
                        return Page();
                    }
                }
                else
                {
                    // Primeiro login: registrar o IP
                    clientUser.IP = currentIP;
                }

                // Atualizar último login
                clientUser.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Criar claims de cliente
                var clientClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, clientUser.Username),
                    new Claim(ClaimTypes.NameIdentifier, clientUser.Id.ToString()),
                    new Claim("IsCustomer", "true")
                };

                var clientIdentity = new ClaimsIdentity(clientClaims, "CustomerCookie");
                var clientAuthProperties = new AuthenticationProperties
                {
                    IsPersistent = RememberMe,
                    ExpiresUtc = RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(24)
                };

                await HttpContext.SignInAsync(
                    "CustomerCookie",
                    new ClaimsPrincipal(clientIdentity),
                    clientAuthProperties);

                _logger.LogInformation("✅ Cliente {Username} logado com sucesso, redirecionando para /UserDashboard", Username);
                return RedirectToPage("/UserDashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao fazer login unificado: {Username} - {Error}", Username, ex.Message);
                ErrorMessage = $"Erro ao processar login: {ex.Message}";
                return Page();
            }
        }
    }
}
