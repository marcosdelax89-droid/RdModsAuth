using BelgaAuthAPI.Data;
using BelgaAuthAPI.Models;
using BelgaAuthAPI.Scripts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BelgaAuthAPI.Controllers
{
    [ApiController]
    [Route("api/reseller")]
    [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
    public class ResellerController : ControllerBase
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<ResellerController> _logger;

        public ResellerController(AuthDbContext context, ILogger<ResellerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private int GetResellerId()
        {
            var resellerIdClaim = User.FindFirst("ResellerId")?.Value;
            return int.TryParse(resellerIdClaim, out var id) ? id : 0;
        }

        // Obter perfil do revendedor
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var resellerId = GetResellerId();
            
            if (resellerId == 0)
            {
                return Unauthorized(new { Message = "Revendedor não identificado" });
            }

            var reseller = await _context.Resellers.FindAsync(resellerId);
            if (reseller == null)
            {
                return NotFound(new { Message = "Revendedor não encontrado" });
            }

            return Ok(new
            {
                reseller.Id,
                reseller.Username,
                reseller.Name,
                reseller.Email,
                reseller.IsActive,
                reseller.CreatedDate
            });
        }

        // Estatísticas do revendedor
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var resellerId = GetResellerId();
            
            var totalLicenses = await _context.Licenses
                .CountAsync(l => l.ResellerId == resellerId);
            
            var usedLicenses = await _context.Licenses
                .CountAsync(l => l.ResellerId == resellerId && l.IsUsed);
            
            var availableLicenses = totalLicenses - usedLicenses;

            var reseller = await _context.Resellers.FindAsync(resellerId);

            return Ok(new
            {
                totalLicenses,
                usedLicenses,
                availableLicenses,
                credits = reseller?.Credits ?? 0
            });
        }

        // Criar licença
        [HttpPost("license")]
        public async Task<IActionResult> CreateLicense([FromBody] DTOs.CreateLicenseRequestDto request)
        {
            try
            {
                var resellerId = GetResellerId();
                
                if (resellerId == 0)
                {
                    return Unauthorized(new { Message = "Revendedor não identificado" });
                }

                // Se não tem customKey, usa o username do revendedor como prefixo
                var reseller = await _context.Resellers.FindAsync(resellerId);
                string? prefix = request.ResellerPrefix;
                
                if (string.IsNullOrEmpty(request.CustomKey) && string.IsNullOrEmpty(prefix) && reseller != null)
                {
                    prefix = reseller.Username;
                }

                // O nome da subscription é SEMPRE o username do revendedor
                var subscriptionName = reseller?.Username ?? request.SubscriptionName ?? "Premium";

                // 💰 Verificar créditos antes de gerar licença
                var creditCost = PaymentController.LicenseCosts.ContainsKey(request.DaysValid)
                    ? PaymentController.LicenseCosts[request.DaysValid]
                    : request.DaysValid; // Fallback: 1 crédito por dia

                if (reseller == null || reseller.Credits < creditCost)
                {
                    return BadRequest(new { Message = $"Saldo insuficiente. Custo: {creditCost} créditos. Seu saldo: {reseller?.Credits ?? 0} créditos." });
                }

                // Gerar licença
                var licenseKey = await LicenseGenerator.CreateLicense(
                    _context, 
                    subscriptionName,
                    request.DaysValid,
                    resellerId,
                    customKey: request.CustomKey,
                    resellerPrefix: prefix);

                // Deduzir créditos e registrar transação
                reseller.Credits -= creditCost;
                reseller.TotalLicensesCreated++;

                _context.CreditTransactions.Add(new Models.CreditTransaction
                {
                    ResellerId = resellerId,
                    Amount = -creditCost,
                    Type = "usage",
                    Description = $"Licença gerada ({request.DaysValid} dias) - {licenseKey}",
                    CreatedDate = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return Ok(new { LicenseKey = licenseKey, Message = "License created successfully", CreditsUsed = creditCost, CreditsRemaining = reseller.Credits });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "License key already exists");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating license for reseller");
                return StatusCode(500, new { Message = "Error creating license" });
            }
        }

        // Listar licenças do revendedor
        [HttpGet("licenses")]
        public async Task<IActionResult> GetLicenses([FromQuery] int limit = 50)
        {
            var resellerId = GetResellerId();
            
            var licenses = await _context.Licenses
                .Where(l => l.ResellerId == resellerId)
                .OrderByDescending(l => l.CreatedDate)
                .Take(limit)
                .Select(l => new
                {
                    l.Id,
                    l.Key,
                    l.SubscriptionName,
                    l.DaysValid,
                    l.IsUsed,
                    l.UsedByUserId,
                    l.UsedDate,
                    l.CreatedDate,
                    l.ExpiryDate
                })
                .ToListAsync();

            return Ok(licenses);
        }

        // Listar apenas os usuários criados por este revendedor
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var resellerId = GetResellerId();
            
            if (resellerId == 0)
            {
                return Unauthorized(new { Message = "Revendedor não identificado" });
            }

            var users = await _context.Users
                .Where(u => u.CreatedByResellerId == resellerId) // Filtrar apenas usuários criados por este revendedor
                .Include(u => u.Subscriptions)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            var result = users.Select(u => {
                var subscriptions = u.Subscriptions.Select(s => new
                {
                    s.SubscriptionName,
                    s.Expiry,
                    s.IsPaused,
                    s.PausedAt,
                    ExpiryDate = DateTimeOffset.FromUnixTimeSeconds(s.Expiry).DateTime
                }).ToList();

                // Calcular tempo restante da subscription mais próxima de expirar
                var activeSubscriptions = subscriptions
                    .Where(s => s.Expiry > now.ToUnixTimeSeconds() || s.IsPaused)
                    .OrderBy(s => s.Expiry)
                    .ToList();

                string? timeLeftUntilExpiry = null;
                if (activeSubscriptions.Any())
                {
                    var firstSub = activeSubscriptions.First();
                    TimeSpan timeLeft;
                    string freezeSuffix = "";
                    
                    if (firstSub.IsPaused && firstSub.PausedAt.HasValue)
                    {
                        var expiryDate = DateTimeOffset.FromUnixTimeSeconds(firstSub.Expiry);
                        var pausedAtDate = DateTimeOffset.FromUnixTimeSeconds(firstSub.PausedAt.Value);
                        timeLeft = expiryDate - pausedAtDate;
                        freezeSuffix = " [Congelado]";
                    }
                    else
                    {
                        var nearestExpiry = DateTimeOffset.FromUnixTimeSeconds(firstSub.Expiry);
                        timeLeft = nearestExpiry - now;
                    }
                    
                    if (timeLeft.TotalSeconds <= 0)
                    {
                        timeLeftUntilExpiry = "Expirado";
                    }
                    else if (timeLeft.TotalDays >= 1)
                    {
                        timeLeftUntilExpiry = $"{(int)timeLeft.TotalDays} dia(s) e {timeLeft.Hours} hora(s){freezeSuffix}";
                    }
                    else if (timeLeft.TotalHours >= 1)
                    {
                        timeLeftUntilExpiry = $"{(int)timeLeft.TotalHours} hora(s) e {timeLeft.Minutes} minuto(s){freezeSuffix}";
                    }
                    else
                    {
                        timeLeftUntilExpiry = $"{(int)timeLeft.TotalMinutes} minuto(s){freezeSuffix}";
                    }
                }
                else
                {
                    timeLeftUntilExpiry = "Expirado";
                }

                return new
                {
                    u.Id,
                    u.Username,
                    u.Email,
                    u.HWID,
                    u.CreatedDate,
                    u.LastLogin,
                    u.IsBanned,
                    Subscriptions = subscriptions,
                    TimeLeftUntilExpiry = timeLeftUntilExpiry
                };
            }).ToList();

            return Ok(result);
        }

        // Criar usuário (para revendedor)
        [HttpPost("user")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var resellerId = GetResellerId();
                
                if (resellerId == 0)
                {
                    return Unauthorized(new { Message = "Revendedor não identificado" });
                }

                if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                {
                    return BadRequest(new { Message = "Username already exists" });
                }

                var user = new User
                {
                    Username = request.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    PasswordPlain = request.Password,
                    Email = request.Email,
                    CreatedDate = DateTime.UtcNow,
                    IsBanned = false,
                    IsAdmin = false,
                    CreatedByResellerId = resellerId // Salvar qual revendedor criou este usuário
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Adiciona subscription se fornecida
                if (request.SubscriptionName != null && request.DaysValid > 0)
                {
                    var expiry = DateTimeOffset.UtcNow.AddDays(request.DaysValid).ToUnixTimeSeconds();
                    var subscription = new Subscription
                    {
                        UserId = user.Id,
                        SubscriptionName = request.SubscriptionName,
                        Expiry = expiry
                    };

                    _context.Subscriptions.Add(subscription);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { Message = "User created successfully", UserId = user.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return StatusCode(500, new { Message = "Error creating user" });
            }
        }

        // Resetar HWID do usuário (para revendedor)
        [HttpPost("users/{id}/resethwid")]
        public async Task<IActionResult> ResetHWID(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                user.HWID = null;

                // Desativa todas as sessões do usuário para forçar novo login
                var sessions = await _context.Sessions
                    .Where(s => s.UserId == id && s.IsActive)
                    .ToListAsync();

                foreach (var session in sessions)
                {
                    session.IsActive = false;
                }

                await _context.SaveChangesAsync();

                return Ok(new { Message = "HWID reset successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting HWID");
                return StatusCode(500, new { Message = "Error resetting HWID" });
            }
        }

        // Congelar/Descongelar assinatura do usuário (Reseller)
        [HttpPost("users/{id}/toggle-freeze")]
        public async Task<IActionResult> ToggleFreezeUser(int id)
        {
            try
            {
                var resellerId = GetResellerId();
                if (resellerId == 0)
                {
                    return Unauthorized(new { Message = "Revendedor não identificado" });
                }

                var user = await _context.Users
                    .Include(u => u.Subscriptions)
                    .FirstOrDefaultAsync(u => u.Id == id && u.CreatedByResellerId == resellerId);

                if (user == null)
                {
                    return NotFound(new { Message = "Usuário não encontrado ou não pertence a você" });
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                
                // Encontrar assinatura ativa ou pausada
                var sub = user.Subscriptions
                    .FirstOrDefault(s => s.Expiry > now || s.IsPaused);

                if (sub == null)
                {
                    return BadRequest(new { Message = "O usuário não possui nenhuma assinatura ativa ou pausada." });
                }

                if (sub.IsPaused)
                {
                    // Descongelar
                    if (sub.PausedAt.HasValue)
                    {
                        var pauseDuration = now - sub.PausedAt.Value;
                        if (pauseDuration > 0)
                        {
                            sub.Expiry += pauseDuration;
                        }
                    }
                    sub.IsPaused = false;
                    sub.PausedAt = null;
                }
                else
                {
                    // Congelar
                    sub.IsPaused = true;
                    sub.PausedAt = now;
                }

                await _context.SaveChangesAsync();

                return Ok(new { Message = $"Assinatura {(sub.IsPaused ? "congelada" : "descongelada")} com sucesso!", IsPaused = sub.IsPaused });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling freeze state for user");
                return StatusCode(500, new { Message = "Erro ao congelar/descongelar assinatura" });
            }
        }

        public class CreateUserRequest
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? SubscriptionName { get; set; }
            public int DaysValid { get; set; } = 0;
        }
    }
}

