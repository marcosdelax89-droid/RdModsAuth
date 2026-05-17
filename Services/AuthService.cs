using BelgaAuthAPI.Data;
using BelgaAuthAPI.Models;
using BelgaAuthAPI.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace BelgaAuthAPI.Services
{
    public class AuthService
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AuthDbContext context, ILogger<AuthService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<AuthResponse> ProcessRequest(AuthRequest request, string? clientIP)
        {
            var response = new AuthResponse();

            try
            {
                // Valida aplicação
                var app = await _context.Applications
                    .FirstOrDefaultAsync(a => a.OwnerId == request.OwnerId && a.Name == request.Name);

                if (app == null)
                {
                    // Fallback para a primeira aplicação cadastrada para requisições web
                    app = await _context.Applications.FirstOrDefaultAsync();
                }

                if (app == null)
                {
                    response.Success = false;
                    response.Message = "Application not found";
                    return response;
                }

                // Valida secret
                if (request.Type != "init" && !string.IsNullOrEmpty(request.SessionId))
                {
                    var session = await _context.Sessions
                        .Include(s => s.Application)
                        .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

                    if (session == null || session.Application.Secret != app.Secret)
                    {
                        response.Success = false;
                        response.Message = "Invalid session";
                        return response;
                    }
                }

                switch (request.Type.ToLower())
                {
                    case "init":
                        return await HandleInit(request, app, clientIP);
                    case "login":
                        return await HandleLogin(request, app, clientIP);
                    case "register":
                        return await HandleRegister(request, app, clientIP);
                    case "check":
                        return await HandleCheck(request, app);
                    case "logout":
                        return await HandleLogout(request, app);
                    case "setvar":
                        return await HandleSetVar(request, app);
                    case "getvar":
                        return await HandleGetVar(request, app);
                    case "ban":
                        return await HandleBan(request, app);
                    case "var":
                        return await HandleVar(request, app);
                    case "fetchonline":
                        return await HandleFetchOnline(request, app);
                    case "fetchstats":
                        return await HandleFetchStats(request, app);
                    case "license":
                        return await HandleLicenseLogin(request, app, clientIP);
                    default:
                        response.Success = false;
                        response.Message = "Invalid request type";
                        return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing auth request");
                response.Success = false;
                response.Message = "Internal server error";
                return response;
            }
        }

        private async Task<AuthResponse> HandleInit(AuthRequest request, Application app, string? clientIP)
        {
            var response = new AuthResponse();

            // Verifica versão
            if (!string.IsNullOrEmpty(request.Ver) && request.Ver != app.Version)
            {
                response.Success = false;
                response.Message = "Version mismatch";
                response.Download = app.DownloadLink;
                return response;
            }

            // Cria nova sessão
            var sessionId = GenerateSessionId();
            var session = new Session
            {
                SessionId = sessionId,
                ApplicationId = app.Id,
                IP = clientIP,
                CreatedDate = DateTime.UtcNow,
                LastActivity = DateTime.UtcNow,
                IsActive = true
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            response.Success = true;
            response.SessionId = sessionId;
            response.NewSession = true;
            response.Message = "Initialized successfully";

            return response;
        }

        private async Task<AuthResponse> HandleLogin(AuthRequest request, Application app, string? clientIP)
        {
            var response = new AuthResponse();

            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Pass))
            {
                response.Success = false;
                response.Message = "Username and password required";
                return response;
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Pass, user.PasswordHash))
            {
                response.Success = false;
                response.Message = "Invalid credentials";
                return response;
            }

            if (user.IsBanned)
            {
                response.Success = false;
                response.Message = $"Account banned: {user.BanReason ?? "No reason provided"}";
                return response;
            }

            // Verifica HWID se já estiver vinculado
            if (!string.IsNullOrEmpty(user.HWID) && user.HWID != request.HWID)
            {
                response.Success = false;
                response.Message = "HWID mismatch";
                return response;
            }

            // Atualiza HWID se não estiver vinculado
            if (string.IsNullOrEmpty(user.HWID) && !string.IsNullOrEmpty(request.HWID))
            {
                user.HWID = request.HWID;
            }

            // Atualiza último login
            user.LastLogin = DateTime.UtcNow;
            user.IP = clientIP;

            // Cria/atualiza sessão
            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

            if (session != null)
            {
                session.UserId = user.Id;
                session.LastActivity = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Carregar subscriptions do usuário
            var subscriptions = await _context.Subscriptions
                .Where(s => s.UserId == user.Id)
                .ToListAsync();

            // Auto-unpause se o login for efetuado
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool dbChanged = false;

            foreach (var sub in subscriptions)
            {
                if (sub.IsPaused && sub.PausedAt.HasValue)
                {
                    var pauseDuration = now - sub.PausedAt.Value;
                    if (pauseDuration > 0)
                    {
                        sub.Expiry += pauseDuration;
                    }
                    sub.IsPaused = false;
                    sub.PausedAt = null;
                    dbChanged = true;
                }
            }

            if (dbChanged)
            {
                await _context.SaveChangesAsync();
            }

            // ⚠️ VERIFICAR SE A SUBSCRIPTION EXPIROU
            var hasValidSubscription = subscriptions.Any(s => s.Expiry > now);

            if (!hasValidSubscription)
            {
                response.Success = false;
                response.Message = "Subscription expired. Please renew your subscription.";
                return response;
            }

            // Prepara resposta com dados do usuário
            response.Success = true;
            response.Message = "Login successful";
            response.Info = new UserInfo
            {
                Username = user.Username,
                IP = user.IP,
                HWID = user.HWID,
                Createdate = ((DateTimeOffset)user.CreatedDate).ToUnixTimeSeconds().ToString(),
                Lastlogin = user.LastLogin.HasValue 
                    ? ((DateTimeOffset)user.LastLogin.Value).ToUnixTimeSeconds().ToString() 
                    : "0",
                Subscriptions = subscriptions.Select(s => new DTOs.Subscription
                {
                    SubscriptionName = s.SubscriptionName,
                    Expiry = s.Expiry.ToString(),
                    Timeleft = CalculateTimeLeft(s.Expiry)
                }).ToList()
            };

            return response;
        }

        private async Task<AuthResponse> HandleRegister(AuthRequest request, Application app, string? clientIP)
        {
            var response = new AuthResponse();

            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Pass))
            {
                response.Success = false;
                response.Message = "Username and password required";
                return response;
            }

            if (string.IsNullOrEmpty(request.Key))
            {
                response.Success = false;
                response.Message = "License key is required to register";
                return response;
            }

            // 1. Verificar se a licença existe no banco
            var license = await _context.Licenses
                .Include(l => l.Reseller)
                .FirstOrDefaultAsync(l => l.Key == request.Key);

            if (license == null)
            {
                response.Success = false;
                response.Message = "License not found";
                return response;
            }

            if (license.IsUsed)
            {
                response.Success = false;
                response.Message = "License already used";
                return response;
            }

            // Verifica se usuário já existe
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                response.Success = false;
                response.Message = "Username already exists";
                return response;
            }

            // Cria usuário
            var user = new User
            {
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Pass),
                PasswordPlain = request.Pass,
                Email = request.Email,
                HWID = request.HWID,
                IP = clientIP,
                CreatedDate = DateTime.UtcNow,
                CreatedByResellerId = license.ResellerId // Vincula o revendedor que gerou a licença
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 2. Criar a assinatura correspondente à licença com o nome do revendedor
            var subName = license.Reseller?.Username ?? "admin";
            var expiryTimestamp = DateTimeOffset.UtcNow.AddDays(license.DaysValid).ToUnixTimeSeconds();
            var subscription = new Models.Subscription
            {
                UserId = user.Id,
                SubscriptionName = subName,
                Expiry = expiryTimestamp
            };
            _context.Subscriptions.Add(subscription);

            // 3. Marcar a licença como usada
            license.IsUsed = true;
            license.UsedByUserId = user.Id;
            license.UsedDate = DateTime.UtcNow;
            license.ExpiryDate = DateTime.UtcNow.AddDays(license.DaysValid);

            // Atualiza estatísticas
            app.TotalUsers++;
            await _context.SaveChangesAsync();

            response.Success = true;
            response.Message = "Registration successful";

            return response;
        }

        private async Task<AuthResponse> HandleCheck(AuthRequest request, Application app)
        {
            var response = new AuthResponse();

            var session = await _context.Sessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

            if (session == null || !session.IsActive)
            {
                response.Success = false;
                response.Message = "Invalid or expired session";
                return response;
            }

            if (session.User == null)
            {
                response.Success = true;
                response.Message = "Session valid";
                return response;
            }

            var user = session.User;

            // Verifica se usuário está banido
            if (user.IsBanned)
            {
                response.Success = false;
                response.Message = $"Account banned: {user.BanReason ?? "No reason provided"}";
                return response;
            }

            session.LastActivity = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            response.Success = true;
            response.Message = "Session valid";
            response.Info = new UserInfo
            {
                Username = user.Username,
                IP = user.IP,
                HWID = user.HWID,
                Createdate = ((DateTimeOffset)user.CreatedDate).ToUnixTimeSeconds().ToString(),
                Lastlogin = user.LastLogin.HasValue 
                    ? ((DateTimeOffset)user.LastLogin.Value).ToUnixTimeSeconds().ToString() 
                    : "0"
            };

            return response;
        }

        private async Task<AuthResponse> HandleLogout(AuthRequest request, Application app)
        {
            var response = new AuthResponse();

            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

            if (session != null)
            {
                session.IsActive = false;
                await _context.SaveChangesAsync();
            }

            response.Success = true;
            response.Message = "Logged out successfully";

            return response;
        }

        private async Task<AuthResponse> HandleSetVar(AuthRequest request, Application app)
        {
            var response = new AuthResponse();

            if (string.IsNullOrEmpty(request.Var) || string.IsNullOrEmpty(request.Data))
            {
                response.Success = false;
                response.Message = "Variable name and data required";
                return response;
            }

            var session = await _context.Sessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

            if (session?.User == null)
            {
                response.Success = false;
                response.Message = "Invalid session or user not logged in";
                return response;
            }

            var variable = await _context.UserVariables
                .FirstOrDefaultAsync(v => v.UserId == session.User.Id && v.VariableName == request.Var);

            if (variable == null)
            {
                variable = new UserVariable
                {
                    UserId = session.User.Id,
                    VariableName = request.Var,
                    Value = request.Data,
                    CreatedDate = DateTime.UtcNow
                };
                _context.UserVariables.Add(variable);
            }
            else
            {
                variable.Value = request.Data;
                variable.UpdatedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            response.Success = true;
            response.Message = "Variable set successfully";

            return response;
        }

        private async Task<AuthResponse> HandleGetVar(AuthRequest request, Application app)
        {
            var response = new AuthResponse();

            if (string.IsNullOrEmpty(request.Var))
            {
                response.Success = false;
                response.Message = "Variable name required";
                return response;
            }

            var session = await _context.Sessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

            if (session?.User == null)
            {
                response.Success = false;
                response.Message = "Invalid session or user not logged in";
                return response;
            }

            var variable = await _context.UserVariables
                .FirstOrDefaultAsync(v => v.UserId == session.User.Id && v.VariableName == request.Var);

            if (variable == null)
            {
                response.Success = false;
                response.Message = "Variable not found";
                return response;
            }

            response.Success = true;
            response.Response = variable.Value;

            return response;
        }

        private async Task<AuthResponse> HandleBan(AuthRequest request, Application app)
        {
            var response = new AuthResponse();

            var session = await _context.Sessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

            if (session?.User == null)
            {
                response.Success = false;
                response.Message = "Invalid session";
                return response;
            }

            session.User.IsBanned = true;
            session.User.BanReason = request.Reason;
            session.IsActive = false;

            await _context.SaveChangesAsync();

            response.Success = true;
            response.Message = "User banned successfully";

            return response;
        }

        private Task<AuthResponse> HandleVar(AuthRequest request, Application app)
        {
            var response = new AuthResponse();
            // Global variables - implementar conforme necessário
            response.Success = false;
            response.Message = "Global variables not implemented";
            return Task.FromResult(response);
        }

        private async Task<AuthResponse> HandleFetchOnline(AuthRequest request, Application app)
        {
            var response = new AuthResponse();

            var activeSessions = await _context.Sessions
                .Include(s => s.User)
                .Where(s => s.ApplicationId == app.Id && s.IsActive && s.User != null)
                .Select(s => new UserCredential { Credential = s.User!.Username })
                .Distinct()
                .ToListAsync();

            response.Success = true;
            response.Users = activeSessions;

            return response;
        }

        private async Task<AuthResponse> HandleFetchStats(AuthRequest request, Application app)
        {
            var response = new AuthResponse();

            var onlineUsers = await _context.Sessions
                .Where(s => s.ApplicationId == app.Id && s.IsActive)
                .Select(s => s.UserId)
                .Distinct()
                .CountAsync();

            var totalLicenses = await _context.Licenses.CountAsync();

            response.Success = true;
            response.AppInfo = new AppInfo
            {
                NumUsers = app.TotalUsers.ToString(),
                NumOnlineUsers = onlineUsers.ToString(),
                NumKeys = totalLicenses.ToString(),
                Version = app.Version,
                CustomerPanelLink = app.CustomerPanelLink,
                DownloadLink = app.DownloadLink
            };

            return response;
        }

        private string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private string CalculateTimeLeft(long expiryTimestamp)
        {
            var expiryDate = DateTimeOffset.FromUnixTimeSeconds(expiryTimestamp);
            var now = DateTimeOffset.UtcNow;
            var timeLeft = expiryDate - now;
            
            if (timeLeft.TotalDays < 0)
                return "Expired";
            
            if (timeLeft.TotalDays >= 1)
                return $"{(int)timeLeft.TotalDays} days";
            
            if (timeLeft.TotalHours >= 1)
                return $"{(int)timeLeft.TotalHours} hours";
            
            return $"{(int)timeLeft.TotalMinutes} minutes";
        }

        private async Task<AuthResponse> HandleLicenseLogin(AuthRequest request, Application app, string? clientIP)
        {
            var response = new AuthResponse();

            if (string.IsNullOrEmpty(request.Key))
            {
                response.Success = false;
                response.Message = "License key is required";
                return response;
            }

            var licenseKey = request.Key;

            // 1. Verificar se a licença existe no banco
            var license = await _context.Licenses
                .Include(l => l.Reseller)
                .FirstOrDefaultAsync(l => l.Key == licenseKey);

            if (license == null)
            {
                response.Success = false;
                response.Message = "License not found";
                return response;
            }

            User user;

            if (!license.IsUsed)
            {
                // 2. Se a licença NÃO estiver usada, vamos registrar automaticamente o usuário.
                // Mas primeiro, garantir que o username (que é a própria chave da licença) não exista no banco.
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == licenseKey);

                if (existingUser != null)
                {
                    user = existingUser;
                }
                else
                {
                    // Criar usuário com username = chave de licença
                    user = new User
                    {
                        Username = licenseKey,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(licenseKey),
                        PasswordPlain = licenseKey, // senha padrão = licença
                        HWID = request.HWID,
                        IP = clientIP,
                        CreatedDate = DateTime.UtcNow,
                        CreatedByResellerId = license.ResellerId // Vincula o revendedor que gerou a licença
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    // Criar a assinatura correspondente à licença com o nome do revendedor
                    var subName = license.Reseller?.Username ?? "admin";
                    var expiryTimestamp = DateTimeOffset.UtcNow.AddDays(license.DaysValid).ToUnixTimeSeconds();
                    var subscription = new Models.Subscription
                    {
                        UserId = user.Id,
                        SubscriptionName = subName,
                        Expiry = expiryTimestamp
                    };
                    _context.Subscriptions.Add(subscription);
                    
                    // Atualiza estatísticas da app
                    app.TotalUsers++;
                    await _context.SaveChangesAsync();
                }

                // Marcar a licença como usada
                license.IsUsed = true;
                license.UsedByUserId = user.Id;
                license.UsedDate = DateTime.UtcNow;
                license.ExpiryDate = DateTime.UtcNow.AddDays(license.DaysValid);
                
                await _context.SaveChangesAsync();
            }
            else
            {
                // 3. Se a licença já estiver usada, realizar o login direto
                var existingUser = await _context.Users
                    .Include(u => u.Subscriptions)
                    .FirstOrDefaultAsync(u => u.Id == license.UsedByUserId);

                if (existingUser == null)
                {
                    response.Success = false;
                    response.Message = "User associated with this license not found";
                    return response;
                }

                user = existingUser;

                if (user.IsBanned)
                {
                    response.Success = false;
                    response.Message = $"Account banned: {user.BanReason ?? "No reason provided"}";
                    return response;
                }

                // Verifica HWID se já houver HWID registrado
                if (!string.IsNullOrEmpty(user.HWID) && user.HWID != request.HWID)
                {
                    response.Success = false;
                    response.Message = "HWID mismatch";
                    return response;
                }

                // Vincula HWID se estiver vazio
                if (string.IsNullOrEmpty(user.HWID) && !string.IsNullOrEmpty(request.HWID))
                {
                    user.HWID = request.HWID;
                }

                // Atualizar login
                user.LastLogin = DateTime.UtcNow;
                user.IP = clientIP;
                await _context.SaveChangesAsync();
            }

            // Atualiza sessão
            var session = await _context.Sessions
                .FirstOrDefaultAsync(s => s.SessionId == request.SessionId);

            if (session != null)
            {
                session.UserId = user.Id;
                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // Carregar as assinaturas do usuário
            var subscriptions = await _context.Subscriptions
                .Where(s => s.UserId == user.Id)
                .ToListAsync();

            // Auto-unpause se o login for efetuado
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool dbChanged = false;

            foreach (var sub in subscriptions)
            {
                if (sub.IsPaused && sub.PausedAt.HasValue)
                {
                    var pauseDuration = now - sub.PausedAt.Value;
                    if (pauseDuration > 0)
                    {
                        sub.Expiry += pauseDuration;
                    }
                    sub.IsPaused = false;
                    sub.PausedAt = null;
                    dbChanged = true;
                }
            }

            if (dbChanged)
            {
                await _context.SaveChangesAsync();
            }

            var validSubscription = subscriptions.FirstOrDefault(s => s.Expiry > now);

            if (validSubscription == null)
            {
                response.Success = false;
                response.Message = "License expired";
                return response;
            }

            // Resposta de sucesso
            response.Success = true;
            response.Message = "Logged in successfully";
            response.Info = new UserInfo
            {
                Username = user.Username,
                IP = user.IP,
                HWID = user.HWID,
                Createdate = ((DateTimeOffset)user.CreatedDate).ToUnixTimeSeconds().ToString(),
                Lastlogin = user.LastLogin.HasValue 
                    ? ((DateTimeOffset)user.LastLogin.Value).ToUnixTimeSeconds().ToString() 
                    : "0",
                Subscriptions = subscriptions.Select(s => new DTOs.Subscription
                {
                    SubscriptionName = s.SubscriptionName,
                    Expiry = s.Expiry.ToString(),
                    Timeleft = CalculateTimeLeft(s.Expiry)
                }).ToList()
            };

            return response;
        }
    }
}

