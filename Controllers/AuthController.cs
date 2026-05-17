using BelgaAuthAPI.DTOs;
using BelgaAuthAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace BelgaAuthAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessAuth([FromForm] AuthRequest request)
        {
            try
            {
                var clientIP = HttpContext.Connection.RemoteIpAddress?.ToString();
                var response = await _authService.ProcessRequest(request, clientIP);

                // Adiciona signature header (simulado para compatibilidade)
                var signature = GenerateSignature(response);
                Response.Headers.Add("signature", signature);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auth controller");
                return StatusCode(500, new AuthResponse
                {
                    Success = false,
                    Message = "Internal server error"
                });
            }
        }

        private string GenerateSignature(AuthResponse response)
        {
            // Implementação simplificada de signature
            // Em produção, use HMAC como no KeyAuth original
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var json = System.Text.Json.JsonSerializer.Serialize(response);
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}

