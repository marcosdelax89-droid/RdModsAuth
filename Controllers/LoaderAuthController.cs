using BelgaAuthAPI.DTOs;
using BelgaAuthAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace BelgaAuthAPI.Controllers
{
    [ApiController]
    [Route("auth")]
    public class LoaderAuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogger<LoaderAuthController> _logger;

        public LoaderAuthController(AuthService authService, ILogger<LoaderAuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginWithLicense([FromBody] LoaderLicenseLoginDto body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Key))
                return BadRequest(new { message = "Key invalida" });

            var request = new AuthRequest
            {
                Type = "license",
                Key = body.Key.Trim(),
                HWID = body.Hwid
            };

            var response = await _authService.ProcessRequest(request, GetClientIp());
            return BuildLoaderResult(response);
        }

        [HttpPost("loginuser")]
        public async Task<IActionResult> LoginWithCredentials([FromBody] LoaderUserLoginDto body)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.Username) || string.IsNullOrWhiteSpace(body.Password))
                return BadRequest(new { message = "Preencha usuario e senha!" });

            var request = new AuthRequest
            {
                Type = "login",
                Username = body.Username.Trim(),
                Pass = body.Password,
                HWID = body.Hwid
            };

            var response = await _authService.ProcessRequest(request, GetClientIp());
            return BuildLoaderResult(response);
        }

        private string? GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();

        private IActionResult BuildLoaderResult(AuthResponse response)
        {
            if (!response.Success)
            {
                var status = response.Message.Contains("HWID", StringComparison.OrdinalIgnoreCase) ? 403 : 401;
                return StatusCode(status, new { message = TranslateMessage(response.Message) });
            }

            var keyInfo = BuildKeyInfo(response);
            return Ok(new { keyInfo, sessionToken = keyInfo.SessionToken });
        }

        private static LoaderKeyInfo BuildKeyInfo(AuthResponse response)
        {
            var keyInfo = new LoaderKeyInfo
            {
                Authenticated = true,
                Blocked = false,
                SessionToken = response.SessionId ?? Guid.NewGuid().ToString("N")
            };

            var subs = response.Info?.Subscriptions;
            if (subs == null || subs.Count == 0)
                return keyInfo;

            var sub = subs[0];
            keyInfo.VendorName = sub.SubscriptionName ?? "";

            if (long.TryParse(sub.Expiry, out var expiryUnix))
            {
                var remaining = expiryUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (remaining > 86400)
                {
                    keyInfo.TimeType = "days";
                    keyInfo.TimeValue = (int)Math.Ceiling(remaining / 86400.0);
                }
                else if (remaining > 3600)
                {
                    keyInfo.TimeType = "hours";
                    keyInfo.TimeValue = (int)Math.Ceiling(remaining / 3600.0);
                }
                else if (remaining > 0)
                {
                    keyInfo.TimeType = "hours";
                    keyInfo.TimeValue = 1;
                }
            }
            else if (!string.IsNullOrWhiteSpace(sub.Timeleft))
            {
                var parts = sub.Timeleft.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[0], out var n))
                {
                    keyInfo.TimeValue = n;
                    keyInfo.TimeType = parts[1].StartsWith("day", StringComparison.OrdinalIgnoreCase) ? "days" : "hours";
                }
            }

            if (keyInfo.TimeType == "days")
                keyInfo.DaysRemaining = keyInfo.TimeValue;

            return keyInfo;
        }

        private static string TranslateMessage(string message) => message switch
        {
            "Invalid credentials" => "Usuario ou senha incorretos.",
            "Username and password required" => "Preencha usuario e senha!",
            "License not found" => "Licenca nao encontrada.",
            "License already used" => "Licenca ja utilizada.",
            "License expired" => "Licenca expirada.",
            "License key is required" => "Informe a licenca.",
            "HWID mismatch" => "HWID nao confere. Reset pelo painel do cliente.",
            "Subscription expired. Please renew your subscription." => "Assinatura expirada. Renove sua licenca.",
            _ => message
        };
    }

    public class LoaderLicenseLoginDto
    {
        public string Key { get; set; } = string.Empty;
        public string Hwid { get; set; } = string.Empty;
    }

    public class LoaderUserLoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Hwid { get; set; } = string.Empty;
    }

    public class LoaderKeyInfo
    {
        public bool Authenticated { get; set; }
        public bool Blocked { get; set; }
        public string TimeType { get; set; } = "days";
        public int TimeValue { get; set; }
        public int DaysRemaining { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
    }
}
