using BelgaAuthAPI.Data;
using BelgaAuthAPI.Models;
using BelgaAuthAPI.Scripts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace BelgaAuthAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AuthDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Criar uma nova licença
        [HttpPost("license")]
        public async Task<IActionResult> CreateLicense([FromBody] DTOs.CreateLicenseRequestDto request)
        {
            try
            {
                var licenseKey = await LicenseGenerator.CreateLicense(
                    _context, 
                    request.SubscriptionName ?? "Premium", 
                    request.DaysValid,
                    resellerId: null,
                    customKey: request.CustomKey,
                    resellerPrefix: request.ResellerPrefix);

                return Ok(new { LicenseKey = licenseKey, Message = "License created successfully" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "License key already exists");
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating license");
                return StatusCode(500, new { Message = "Error creating license" });
            }
        }

        // Listar todas as licenças
        [HttpGet("licenses")]
        public async Task<IActionResult> GetLicenses()
        {
            var licenses = await _context.Licenses
                .Select(l => new
                {
                    l.Id,
                    l.Key,
                    l.SubscriptionName,
                    l.DaysValid,
                    l.IsUsed,
                    l.UsedByUserId,
                    l.CreatedDate
                })
                .ToListAsync();

            return Ok(licenses);
        }

        // Listar todos os usuários
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Include(u => u.Subscriptions)
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.PasswordPlain,
                    u.Email,
                    u.HWID,
                    u.CreatedDate,
                    u.LastLogin,
                    u.IsBanned,
                    u.BanReason,
                    Subscriptions = u.Subscriptions.Select(s => new
                    {
                        s.SubscriptionName,
                        s.Expiry,
                        s.IsPaused,
                        s.PausedAt,
                        ExpiryDate = DateTimeOffset.FromUnixTimeSeconds(s.Expiry).DateTime
                    })
                })
                .ToListAsync();

            return Ok(users);
        }

        // Criar usuário admin manualmente
        [HttpPost("user")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
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
                    IsAdmin = false // Usuários criados via API NÃO têm acesso admin por padrão
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

        // Estatísticas para o dashboard
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var totalUsers = await _context.Users.CountAsync();
                var onlineUsers = await _context.Sessions
                    .Where(s => s.IsActive)
                    .Select(s => s.UserId)
                    .Distinct()
                    .CountAsync();
                var totalLicenses = await _context.Licenses.CountAsync();
                var availableLicenses = await _context.Licenses
                    .CountAsync(l => !l.IsUsed);

                return Ok(new
                {
                    totalUsers,
                    onlineUsers,
                    totalLicenses,
                    availableLicenses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stats");
                return StatusCode(500, new { Message = "Error getting stats" });
            }
        }

        // Deletar licença (permite deletar mesmo se foi usada)
        [HttpDelete("licenses/{id}")]
        public async Task<IActionResult> DeleteLicense(int id)
        {
            try
            {
                var license = await _context.Licenses.FindAsync(id);
                if (license == null)
                {
                    return NotFound(new { Message = "License not found" });
                }

                // Permite deletar mesmo se foi usada
                _context.Licenses.Remove(license);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "License deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting license");
                return StatusCode(500, new { Message = "Error deleting license" });
            }
        }

        // Banir usuário
        [HttpPost("users/{id}/ban")]
        public async Task<IActionResult> BanUser(int id, [FromBody] BanUserRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                user.IsBanned = true;
                user.BanReason = request.Reason ?? "Banned by admin";

                // Desativa todas as sessões do usuário
                var sessions = await _context.Sessions
                    .Where(s => s.UserId == id && s.IsActive)
                    .ToListAsync();

                foreach (var session in sessions)
                {
                    session.IsActive = false;
                }

                await _context.SaveChangesAsync();

                return Ok(new { Message = "User banned successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error banning user");
                return StatusCode(500, new { Message = "Error banning user" });
            }
        }

        // Resetar HWID do usuário
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

        // Desbanir usuário
        [HttpPost("users/{id}/unban")]
        public async Task<IActionResult> UnbanUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                user.IsBanned = false;
                user.BanReason = null;

                await _context.SaveChangesAsync();

                return Ok(new { Message = "User unbanned successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unbanning user");
                return StatusCode(500, new { Message = "Error unbanning user" });
            }
        }

        // Deletar usuário
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Subscriptions)
                    .Include(u => u.Variables)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                // Remove todas as sessões do usuário
                var sessions = await _context.Sessions
                    .Where(s => s.UserId == id)
                    .ToListAsync();

                _context.Sessions.RemoveRange(sessions);

                // Remove subscriptions, variables e depois o usuário
                // (cascata deve fazer isso automaticamente, mas vamos garantir)
                _context.Subscriptions.RemoveRange(user.Subscriptions);
                _context.UserVariables.RemoveRange(user.Variables);
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();

                return Ok(new { Message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return StatusCode(500, new { Message = "Error deleting user" });
            }
        }

        // Obter detalhes de um usuário específico
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Subscriptions)
                    .Include(u => u.Variables)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                var now = DateTimeOffset.UtcNow;
                var subscriptions = user.Subscriptions.Select(s => new
                {
                    s.Id,
                    s.SubscriptionName,
                    s.Expiry,
                    s.IsPaused,
                    s.PausedAt,
                    ExpiryDate = DateTimeOffset.FromUnixTimeSeconds(s.Expiry).DateTime,
                    DaysLeft = (int)((DateTimeOffset.FromUnixTimeSeconds(s.Expiry) - now).TotalDays)
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

                var result = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    user.HWID,
                    user.CreatedDate,
                    user.LastLogin,
                    user.IsBanned,
                    user.BanReason,
                    user.IsAdmin,
                    Subscriptions = subscriptions,
                    TimeLeftUntilExpiry = timeLeftUntilExpiry
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user");
                return StatusCode(500, new { Message = "Error getting user" });
            }
        }

        // Adicionar dias a uma subscription existente
        [HttpPost("users/{id}/adddays")]
        public async Task<IActionResult> AddDaysToSubscription(int id, [FromBody] AddDaysRequest request)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Subscriptions)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                if (request.SubscriptionId.HasValue)
                {
                    // Adicionar dias a uma subscription específica
                    var subscription = user.Subscriptions.FirstOrDefault(s => s.Id == request.SubscriptionId.Value);
                    if (subscription == null)
                    {
                        return NotFound(new { Message = "Subscription not found" });
                    }

                    var currentExpiry = DateTimeOffset.FromUnixTimeSeconds(subscription.Expiry);
                    var newExpiry = currentExpiry.AddDays(request.Days);
                    subscription.Expiry = newExpiry.ToUnixTimeSeconds();
                }
                else if (!string.IsNullOrEmpty(request.SubscriptionName))
                {
                    // Adicionar dias a uma subscription por nome
                    var subscription = user.Subscriptions.FirstOrDefault(s => s.SubscriptionName == request.SubscriptionName);
                    if (subscription == null)
                    {
                        // Se não existir, cria uma nova
                        var expiry = DateTimeOffset.UtcNow.AddDays(request.Days).ToUnixTimeSeconds();
                        subscription = new Subscription
                        {
                            UserId = user.Id,
                            SubscriptionName = request.SubscriptionName,
                            Expiry = expiry
                        };
                        _context.Subscriptions.Add(subscription);
                    }
                    else
                    {
                        // Adiciona dias à existente
                        var currentExpiry = DateTimeOffset.FromUnixTimeSeconds(subscription.Expiry);
                        var newExpiry = currentExpiry > DateTimeOffset.UtcNow 
                            ? currentExpiry.AddDays(request.Days) 
                            : DateTimeOffset.UtcNow.AddDays(request.Days);
                        subscription.Expiry = newExpiry.ToUnixTimeSeconds();
                    }
                }
                else
                {
                    // Adicionar dias à primeira subscription ativa, ou criar uma nova "Premium"
                    var activeSubscription = user.Subscriptions
                        .Where(s => s.Expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                        .OrderByDescending(s => s.Expiry)
                        .FirstOrDefault();

                    if (activeSubscription != null)
                    {
                        var currentExpiry = DateTimeOffset.FromUnixTimeSeconds(activeSubscription.Expiry);
                        var newExpiry = currentExpiry.AddDays(request.Days);
                        activeSubscription.Expiry = newExpiry.ToUnixTimeSeconds();
                    }
                    else
                    {
                        // Cria nova subscription Premium
                        var expiry = DateTimeOffset.UtcNow.AddDays(request.Days).ToUnixTimeSeconds();
                        var newSubscription = new Subscription
                        {
                            UserId = user.Id,
                            SubscriptionName = "Premium",
                            Expiry = expiry
                        };
                        _context.Subscriptions.Add(newSubscription);
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { Message = $"Adicionados {request.Days} dias com sucesso!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding days");
                return StatusCode(500, new { Message = "Error adding days" });
            }
        }

        // Atualizar acesso admin do usuário
        [HttpPut("users/{id}/admin")]
        public async Task<IActionResult> UpdateUserAdminAccess(int id, [FromBody] UpdateAdminAccessRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                user.IsAdmin = request.IsAdmin;
                await _context.SaveChangesAsync();

                return Ok(new { Message = $"Acesso admin {(request.IsAdmin ? "permitido" : "revogado")} com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating admin access");
                return StatusCode(500, new { Message = "Error updating admin access" });
            }
        }

        // ========== REVENDEDORES ==========

        // Listar todos os revendedores
        [HttpGet("resellers")]
        public async Task<IActionResult> GetResellers()
        {
            var resellers = await _context.Resellers
                .Select(r => new
                {
                    r.Id,
                    r.Username,
                    r.PasswordPlain,
                    r.Name,
                    r.Email,
                    r.IsActive,
                    r.LastLogin,
                    r.CreatedDate,
                    r.TotalLicensesCreated,
                    TotalLicensesUsed = r.Licenses.Count(l => l.IsUsed),
                    r.Credits,
                    r.Notes
                })
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return Ok(resellers);
        }

        // Criar novo revendedor
        [HttpPost("reseller")]
        public async Task<IActionResult> CreateReseller([FromBody] CreateResellerRequest request)
        {
            try
            {
                // Garantir que o banco está criado
                await _context.Database.EnsureCreatedAsync();

                // Verificar se username já existe
                if (await _context.Resellers.AnyAsync(r => r.Username == request.Username))
                {
                    return BadRequest(new { Message = "Username já existe" });
                }

                var reseller = new Reseller
                {
                    Username = request.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    PasswordPlain = request.Password,
                    Name = request.Name,
                    Email = request.Email,
                    Notes = request.Notes,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                _context.Resellers.Add(reseller);
                await _context.SaveChangesAsync();

                return Ok(new { Message = "Revendedor criado com sucesso", Id = reseller.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reseller: {Error}", ex.Message);
                _logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                return StatusCode(500, new { Message = $"Error creating reseller: {ex.Message}", Error = ex.ToString() });
            }
        }

        // Obter revendedor por ID
        [HttpGet("reseller/{id}")]
        public async Task<IActionResult> GetReseller(int id)
        {
            var reseller = await _context.Resellers
                .Include(r => r.Licenses)
                .FirstOrDefaultAsync(r => r.Id == id);

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
                reseller.LastLogin,
                reseller.CreatedDate,
                reseller.TotalLicensesCreated,
                TotalLicensesUsed = reseller.Licenses.Count(l => l.IsUsed),
                reseller.Notes
            });
        }

        // Editar revendedor
        [HttpPut("reseller/{id}")]
        public async Task<IActionResult> UpdateReseller(int id, [FromBody] UpdateResellerRequest request)
        {
            try
            {
                var reseller = await _context.Resellers.FindAsync(id);
                if (reseller == null)
                {
                    return NotFound(new { Message = "Revendedor não encontrado" });
                }

                if (!string.IsNullOrEmpty(request.Name))
                    reseller.Name = request.Name;
                
                if (!string.IsNullOrEmpty(request.Email))
                    reseller.Email = request.Email;
                
                if (!string.IsNullOrEmpty(request.Password))
                {
                    reseller.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    reseller.PasswordPlain = request.Password;
                }
                
                if (request.IsActive.HasValue)
                    reseller.IsActive = request.IsActive.Value;
                
                if (request.Notes != null)
                    reseller.Notes = request.Notes;

                await _context.SaveChangesAsync();

                return Ok(new { Message = "Revendedor atualizado com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating reseller");
                return StatusCode(500, new { Message = "Error updating reseller" });
            }
        }

        // Bloquear/Desbloquear revendedor
        [HttpPost("resellers/{id}/toggle-status")]
        public async Task<IActionResult> ToggleResellerStatus(int id)
        {
            try
            {
                var reseller = await _context.Resellers.FindAsync(id);
                if (reseller == null)
                {
                    return NotFound(new { Message = "Revendedor não encontrado" });
                }

                reseller.IsActive = !reseller.IsActive;
                await _context.SaveChangesAsync();

                return Ok(new { Message = $"Revendedor {(reseller.IsActive ? "desbloqueado" : "bloqueado")} com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling reseller status");
                return StatusCode(500, new { Message = "Error toggling reseller status" });
            }
        }

        // Deletar revendedor
        [HttpDelete("resellers/{id}")]
        public async Task<IActionResult> DeleteReseller(int id)
        {
            try
            {
                var reseller = await _context.Resellers
                    .Include(r => r.Licenses)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (reseller == null)
                {
                    return NotFound(new { Message = "Revendedor não encontrado" });
                }

                // Deletar todas as licenças criadas pelo revendedor
                _context.Licenses.RemoveRange(reseller.Licenses);

                // Deletar o revendedor
                _context.Resellers.Remove(reseller);

                await _context.SaveChangesAsync();

                return Ok(new { Message = "Revendedor deletado com sucesso" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting reseller");
                return StatusCode(500, new { Message = "Error deleting reseller" });
            }
        }

        // Adicionar/Remover créditos para um revendedor
        [HttpPost("resellers/{id}/credits")]
        public async Task<IActionResult> ManageResellerCredits(int id, [FromBody] ManageCreditsRequest request)
        {
            try
            {
                var reseller = await _context.Resellers.FindAsync(id);
                if (reseller == null)
                {
                    return NotFound(new { Message = "Revendedor não encontrado" });
                }

                reseller.Credits += request.Credits;
                if (reseller.Credits < 0) reseller.Credits = 0; // Garantir que não fica negativo

                await _context.SaveChangesAsync();

                // Registrar transação
                _context.CreditTransactions.Add(new Models.CreditTransaction
                {
                    ResellerId = reseller.Id,
                    Amount = request.Credits,
                    Type = request.Credits >= 0 ? "purchase" : "usage",
                    Description = request.Credits >= 0 
                        ? $"Créditos adicionados pelo Admin" 
                        : $"Créditos removidos pelo Admin",
                    CreatedDate = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();

                return Ok(new { Message = $"Créditos alterados com sucesso! Novo saldo: {reseller.Credits}", NewBalance = reseller.Credits });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error managing reseller credits");
                return StatusCode(500, new { Message = "Error managing reseller credits" });
            }
        }

        // Congelar/Descongelar assinatura do usuário (Admin)
        [HttpPost("users/{id}/toggle-freeze")]
        public async Task<IActionResult> ToggleFreezeUser(int id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Subscriptions)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return NotFound(new { Message = "Usuário não encontrado" });
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

        // Alterar senha de um usuário
        [HttpPost("users/{id}/password")]
        public async Task<IActionResult> ChangeUserPassword(int id, [FromBody] ChangeUserPasswordRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { Message = "User not found" });
                }

                if (string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { Message = "A senha não pode ser vazia" });
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                user.PasswordPlain = request.Password;

                await _context.SaveChangesAsync();
                return Ok(new { Message = "Senha do usuário atualizada com sucesso!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing user password");
                return StatusCode(500, new { Message = "Erro ao alterar a senha do usuário" });
            }
        }
    }

    public class ChangeUserPasswordRequest
    {
        public string Password { get; set; } = string.Empty;
    }

    public class ManageCreditsRequest
    {
        public int Credits { get; set; }
    }

    public class AddDaysRequest
    {
        public int Days { get; set; }
        public int? SubscriptionId { get; set; }
        public string? SubscriptionName { get; set; }
    }

    public class BanUserRequest
    {
        public string? Reason { get; set; }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? SubscriptionName { get; set; }
        public int DaysValid { get; set; } = 0;
    }

    public class CreateResellerRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateResellerRequest
    {
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public bool? IsActive { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdateAdminAccessRequest
    {
        public bool IsAdmin { get; set; }
    }
}

