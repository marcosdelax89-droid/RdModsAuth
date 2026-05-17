using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using BelgaAuthAPI.Data;
using BelgaAuthAPI.Models;

namespace BelgaAuthAPI.Pages
{
    [Authorize(Policy = "CustomerOnly", AuthenticationSchemes = "CustomerCookie")]
    public class UserDashboardModel : PageModel
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<UserDashboardModel> _logger;

        public UserDashboardModel(AuthDbContext context, ILogger<UserDashboardModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public new User? User { get; set; }
        public List<Models.Subscription> Subscriptions { get; set; } = new();
        public Models.Subscription? ActiveSubscription { get; set; }
        public string TimeRemaining { get; set; } = string.Empty;
        public int OnlineUsersCount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.User.Identity?.IsAuthenticated != true || !HttpContext.User.HasClaim("IsCustomer", "true"))
            {
                return RedirectToPage("/Login");
            }

            var username = HttpContext.User.Identity.Name;

            User = await _context.Users
                .Include(u => u.Subscriptions)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (User == null)
            {
                await HttpContext.SignOutAsync("CustomerCookie");
                return RedirectToPage("/Login");
            }

            Subscriptions = User.Subscriptions.ToList();

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Assinatura ativa é uma que não expirou OR que está pausada
            ActiveSubscription = Subscriptions.FirstOrDefault(s => s.Expiry > now || s.IsPaused);

            if (ActiveSubscription != null)
            {
                if (ActiveSubscription.IsPaused && ActiveSubscription.PausedAt.HasValue)
                {
                    var expiryDate = DateTimeOffset.FromUnixTimeSeconds(ActiveSubscription.Expiry);
                    var pausedAtDate = DateTimeOffset.FromUnixTimeSeconds(ActiveSubscription.PausedAt.Value);
                    var timeLeft = expiryDate - pausedAtDate;

                    if (timeLeft.TotalDays >= 1)
                        TimeRemaining = $"{(int)timeLeft.TotalDays} dias (Congelado)";
                    else if (timeLeft.TotalHours >= 1)
                        TimeRemaining = $"{(int)timeLeft.TotalHours} horas (Congelado)";
                    else
                        TimeRemaining = $"{(int)timeLeft.TotalMinutes} minutos (Congelado)";
                }
                else
                {
                    var expiryDate = DateTimeOffset.FromUnixTimeSeconds(ActiveSubscription.Expiry);
                    var nowOffset = DateTimeOffset.UtcNow;
                    var timeLeft = expiryDate - nowOffset;

                    if (timeLeft.TotalDays >= 1)
                        TimeRemaining = $"{(int)timeLeft.TotalDays} dias";
                    else if (timeLeft.TotalHours >= 1)
                        TimeRemaining = $"{(int)timeLeft.TotalHours} horas";
                    else
                        TimeRemaining = $"{(int)timeLeft.TotalMinutes} minutos";
                }
            }
            else
            {
                TimeRemaining = "Expirada";
            }

            // Conta usuários online (atividade nos últimos 10 minutos)
            OnlineUsersCount = await _context.Sessions
                .Where(s => s.IsActive && s.LastActivity >= DateTime.UtcNow.AddMinutes(-10))
                .Select(s => s.UserId)
                .Distinct()
                .CountAsync();

            // Garante pelo menos 1 usuário online (ele mesmo)
            if (OnlineUsersCount <= 0)
            {
                OnlineUsersCount = 1;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostResetHWIDAsync()
        {
            if (HttpContext.User.Identity?.IsAuthenticated != true || !HttpContext.User.HasClaim("IsCustomer", "true"))
            {
                return RedirectToPage("/Login");
            }

            var username = HttpContext.User.Identity.Name;
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            // Verificar se o reset foi feito nas últimas 24 horas
            if (user.LastHWIDReset.HasValue && (DateTime.UtcNow - user.LastHWIDReset.Value).TotalHours < 24)
            {
                TempData["ErrorMessage"] = "Você só pode resetar seu HWID 1 vez a cada 24 horas.";
                return RedirectToPage();
            }

            // Resetar o HWID
            user.HWID = null;
            user.LastHWIDReset = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "HWID resetado com sucesso!";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostPauseSubscriptionAsync()
        {
            if (HttpContext.User.Identity?.IsAuthenticated != true || !HttpContext.User.HasClaim("IsCustomer", "true"))
            {
                return RedirectToPage("/Login");
            }

            var username = HttpContext.User.Identity.Name;
            var user = await _context.Users
                .Include(u => u.Subscriptions)
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            // Verifica se o usuário já pausou a conta anteriormente
            if (user.HasPaused)
            {
                TempData["ErrorMessage"] = "Você já pausou seu login 1 vez. O pause é limitado a apenas 1 vez por conta.";
                return RedirectToPage();
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var activeSub = user.Subscriptions.FirstOrDefault(s => s.Expiry > now && !s.IsPaused);

            if (activeSub == null)
            {
                TempData["ErrorMessage"] = "Você não possui nenhuma assinatura ativa e não pausada para pausar.";
                return RedirectToPage();
            }

            // Pausa a assinatura
            activeSub.IsPaused = true;
            activeSub.PausedAt = now;
            user.HasPaused = true; // Marca que já usou o pause

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Login pausado com sucesso! Seus dias foram congelados. Para despausar, basta efetuar o login novamente no seu injetor/painel.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLogoutAsync()
        {
            await HttpContext.SignOutAsync("CustomerCookie");
            return RedirectToPage("/Login");
        }
    }
}
