using BelgaAuthAPI.Data;
using BelgaAuthAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BelgaAuthAPI.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly AuthDbContext _context;
        private readonly ILogger<PaymentController> _logger;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        // Pacotes de créditos disponíveis (créditos => valor em centavos)
        private static readonly Dictionary<int, int> CreditPackages = new()
        {
            { 7, 700 },      // 7 créditos = R$7.00
            { 20, 2000 },    // 20 créditos = R$20.00
            { 25, 2500 },    // 25 créditos = R$25.00
            { 45, 4500 },    // 45 créditos = R$45.00
            { 200, 20000 }   // 200 créditos = R$200.00
        };

        // Custo em créditos por duração de licença
        public static readonly Dictionary<int, int> LicenseCosts = new()
        {
            { 1, 7 },
            { 5, 20 },
            { 7, 25 },
            { 30, 45 },
            { 999, 200 }   // Lifetime
        };

        public PaymentController(AuthDbContext context, ILogger<PaymentController> logger, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _config = config;
            _httpClient = httpClientFactory.CreateClient();
        }

        // Retornar pacotes disponíveis e custos
        [HttpGet("packages")]
        [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
        public IActionResult GetPackages()
        {
            var packages = CreditPackages.Select(p => new
            {
                Credits = p.Key,
                PriceCents = p.Value,
                PriceFormatted = $"R$ {p.Value / 100.0:F2}"
            }).ToList();

            return Ok(new { packages, licenseCosts = LicenseCosts });
        }

        // Criar cobrança PIX via Mercado Pago
        [HttpPost("create-pix")]
        [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
        public async Task<IActionResult> CreatePixCharge([FromBody] CreatePixRequest request)
        {
            var resellerIdClaim = User.FindFirst("ResellerId")?.Value;
            if (!int.TryParse(resellerIdClaim, out var resellerId))
                return Unauthorized(new { message = "Revendedor não identificado" });

            if (!CreditPackages.ContainsKey(request.Credits))
                return BadRequest(new { message = "Pacote de créditos inválido" });

            var baseAmountCents = CreditPackages[request.Credits];
            var amountCents = baseAmountCents;
            var finalCredits = request.Credits;

            Coupon? coupon = null;
            if (!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                coupon = await _context.Coupons.FindAsync(request.CouponCode.Trim().ToUpper());
                if (coupon != null && coupon.IsActive)
                {
                    bool valid = true;
                    if (coupon.ExpirationDate.HasValue && coupon.ExpirationDate.Value < DateTime.UtcNow) valid = false;
                    if (coupon.MaxUses.HasValue && coupon.UsesCount >= coupon.MaxUses.Value) valid = false;

                    if (valid)
                    {
                        if (coupon.DiscountPercentage > 0)
                        {
                            amountCents = (int)Math.Round(baseAmountCents * (1.0 - (coupon.DiscountPercentage / 100.0)));
                        }
                        if (coupon.BonusPercentage > 0)
                        {
                            finalCredits = (int)Math.Round(request.Credits * (1.0 + (coupon.BonusPercentage / 100.0)));
                        }

                        // Incrementar usos do cupom
                        coupon.UsesCount++;
                    }
                }
            }

            var accessToken = _config["MercadoPago:AccessToken"] ?? "";

            // Criar pedido no banco de dados
            var order = new PaymentOrder
            {
                ResellerId = resellerId,
                AmountCents = amountCents,
                Credits = finalCredits,
                Status = "pending",
                CreatedDate = DateTime.UtcNow,
                CouponCode = coupon?.Code
            };

            _context.PaymentOrders.Add(order);
            await _context.SaveChangesAsync();

            // Gerar cobrança PIX no Mercado Pago
            if (!string.IsNullOrEmpty(accessToken) && !accessToken.Contains("SEU_"))
            {
                try
                {
                    var mpPayload = new
                    {
                        transaction_amount = amountCents / 100.0,
                        description = $"BelgaAuth - {finalCredits} créditos",
                        payment_method_id = "pix",
                        payer = new
                        {
                            email = $"reseller{resellerId}@belgaauth.com"
                        }
                    };

                    var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/v1/payments");
                    httpRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
                    httpRequest.Headers.Add("X-Idempotency-Key", Guid.NewGuid().ToString());
                    httpRequest.Content = new StringContent(
                        JsonSerializer.Serialize(mpPayload),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    var response = await _httpClient.SendAsync(httpRequest);
                    var responseBody = await response.Content.ReadAsStringAsync();

                    _logger.LogInformation("Mercado Pago response: {StatusCode} - {Body}", response.StatusCode, responseBody);

                    if (response.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        var root = doc.RootElement;

                        // Pegar ID do pagamento do Mercado Pago
                        var mpPaymentId = root.GetProperty("id").GetInt64().ToString();
                        order.PixCorrelationId = mpPaymentId;

                        // Pegar dados do QR Code PIX
                        var pointOfInteraction = root.GetProperty("point_of_interaction");
                        var transactionData = pointOfInteraction.GetProperty("transaction_data");

                        // QR Code em base64
                        if (transactionData.TryGetProperty("qr_code_base64", out var qrBase64))
                        {
                            order.QrCodeUrl = $"data:image/png;base64,{qrBase64.GetString()}";
                        }

                        // Código copia e cola
                        if (transactionData.TryGetProperty("qr_code", out var qrCode))
                        {
                            order.PixCopyPaste = qrCode.GetString();
                        }

                        await _context.SaveChangesAsync();

                        return Ok(new
                        {
                            orderId = order.Id,
                            mpPaymentId,
                            qrCodeUrl = order.QrCodeUrl,
                            pixCopyPaste = order.PixCopyPaste,
                            amount = $"R$ {amountCents / 100.0:F2}",
                            credits = finalCredits
                        });
                    }
                    else
                    {
                        _logger.LogWarning("Mercado Pago erro: {StatusCode} - {Body}", response.StatusCode, responseBody);
                        return StatusCode(500, new { message = $"Erro do Mercado Pago: {response.StatusCode}" });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao criar cobrança PIX no Mercado Pago");
                    return StatusCode(500, new { message = "Erro ao conectar com Mercado Pago" });
                }
            }

            // Fallback: sem token configurado
            return Ok(new
            {
                orderId = order.Id,
                qrCodeUrl = (string?)null,
                pixCopyPaste = (string?)null,
                amount = $"R$ {amountCents / 100.0:F2}",
                credits = finalCredits,
                message = "Token do Mercado Pago não configurado. Aguardando confirmação manual do admin."
            });
        }

        // Webhook do Mercado Pago — chamado quando PIX é pago
        [HttpPost("mp-webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> MercadoPagoWebhook([FromQuery] string? type, [FromQuery(Name = "data.id")] string? dataId)
        {
            // Ler body completo
            string body;
            using (var reader = new StreamReader(Request.Body))
            {
                body = await reader.ReadToEndAsync();
            }

            _logger.LogInformation("📩 Webhook Mercado Pago recebido: type={Type}, dataId={DataId}, body={Body}", type, dataId, body);

            try
            {
                string? paymentId = dataId;

                // Tentar extrair do body se não veio na query
                if (string.IsNullOrEmpty(paymentId) && !string.IsNullOrEmpty(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var idProp))
                    {
                        paymentId = idProp.ToString();
                    }
                }

                if (string.IsNullOrEmpty(paymentId) || type != "payment")
                {
                    return Ok(); // Ignorar outros tipos de notificação
                }

                // Consultar o status do pagamento no Mercado Pago
                var accessToken = _config["MercadoPago:AccessToken"] ?? "";
                var checkRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/v1/payments/{paymentId}");
                checkRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                var checkResponse = await _httpClient.SendAsync(checkRequest);
                if (!checkResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Erro ao consultar pagamento {PaymentId} no MP", paymentId);
                    return Ok();
                }

                var checkBody = await checkResponse.Content.ReadAsStringAsync();
                using var checkDoc = JsonDocument.Parse(checkBody);
                var status = checkDoc.RootElement.GetProperty("status").GetString();

                _logger.LogInformation("Status do pagamento {PaymentId}: {Status}", paymentId, status);

                if (status != "approved")
                {
                    return Ok(); // Pagamento não aprovado ainda
                }

                // Buscar pedido pelo ID do Mercado Pago
                var order = await _context.PaymentOrders
                    .FirstOrDefaultAsync(p => p.PixCorrelationId == paymentId && p.Status == "pending");

                if (order == null)
                {
                    _logger.LogWarning("Pedido não encontrado ou já processado: MP PaymentId={PaymentId}", paymentId);
                    return Ok();
                }

                // Confirmar pagamento e adicionar créditos
                await ConfirmPayment(order);
                _logger.LogInformation("✅ Pagamento PIX confirmado via webhook MP: OrderId={OrderId}, Credits={Credits}", order.Id, order.Credits);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar webhook Mercado Pago");
                return Ok(); // Sempre retornar 200
            }
        }

        // Polling: verificar pagamento consultando direto no Mercado Pago
        [HttpPost("check-payment/{orderId}")]
        [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
        public async Task<IActionResult> CheckAndConfirmPayment(int orderId)
        {
            var resellerIdClaim = User.FindFirst("ResellerId")?.Value;
            if (!int.TryParse(resellerIdClaim, out var resellerId))
                return Unauthorized();

            var order = await _context.PaymentOrders
                .FirstOrDefaultAsync(p => p.Id == orderId && p.ResellerId == resellerId);

            if (order == null)
                return NotFound();

            if (order.Status == "paid")
            {
                var r = await _context.Resellers.FindAsync(resellerId);
                return Ok(new { status = "paid", currentCredits = r?.Credits ?? 0 });
            }

            // Se temos o ID do pagamento no MP, consultar o status
            if (!string.IsNullOrEmpty(order.PixCorrelationId))
            {
                var accessToken = _config["MercadoPago:AccessToken"] ?? "";
                var checkRequest = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/v1/payments/{order.PixCorrelationId}");
                checkRequest.Headers.Add("Authorization", $"Bearer {accessToken}");

                try
                {
                    var checkResponse = await _httpClient.SendAsync(checkRequest);
                    if (checkResponse.IsSuccessStatusCode)
                    {
                        var checkBody = await checkResponse.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(checkBody);
                        var status = doc.RootElement.GetProperty("status").GetString();

                        if (status == "approved" && order.Status == "pending")
                        {
                            await ConfirmPayment(order);
                            var reseller = await _context.Resellers.FindAsync(resellerId);
                            return Ok(new { status = "paid", currentCredits = reseller?.Credits ?? 0 });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao consultar pagamento no MP");
                }
            }

            var res = await _context.Resellers.FindAsync(resellerId);
            return Ok(new { status = order.Status, currentCredits = res?.Credits ?? 0 });
        }

        // Admin confirma pagamento manualmente
        [HttpPost("confirm/{orderId}")]
        [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "AdminCookie")]
        public async Task<IActionResult> ConfirmPaymentManual(int orderId)
        {
            var order = await _context.PaymentOrders.FindAsync(orderId);
            if (order == null)
                return NotFound(new { message = "Pedido não encontrado" });

            if (order.Status == "paid")
                return BadRequest(new { message = "Pedido já foi confirmado" });

            await ConfirmPayment(order);
            return Ok(new { message = $"Pagamento confirmado! {order.Credits} créditos adicionados." });
        }

        // Listar pedidos (admin)
        [HttpGet("pending")]
        [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "AdminCookie")]
        public async Task<IActionResult> GetPendingOrders()
        {
            var orders = await _context.PaymentOrders
                .Include(p => p.Reseller)
                .OrderByDescending(p => p.CreatedDate)
                .Take(50)
                .Select(p => new
                {
                    p.Id,
                    p.ResellerId,
                    ResellerName = p.Reseller.Username,
                    p.AmountCents,
                    AmountFormatted = $"R$ {p.AmountCents / 100.0:F2}",
                    p.Credits,
                    p.Status,
                    p.CreatedDate,
                    p.PaidAt
                })
                .ToListAsync();

            return Ok(orders);
        }

        // Admin adicionar créditos manualmente
        [HttpPost("admin-add-credits")]
        [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "AdminCookie")]
        public async Task<IActionResult> AdminAddCredits([FromBody] AdminAddCreditsRequest request)
        {
            var reseller = await _context.Resellers.FindAsync(request.ResellerId);
            if (reseller == null)
                return NotFound(new { message = "Revendedor não encontrado" });

            reseller.Credits += request.Amount;
            reseller.TotalCreditsEver += request.Amount;

            _context.CreditTransactions.Add(new CreditTransaction
            {
                ResellerId = reseller.Id,
                Amount = request.Amount,
                Type = "admin_add",
                Description = $"Admin adicionou {request.Amount} créditos",
                CreatedDate = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(new { message = $"{request.Amount} créditos adicionados para {reseller.Username}. Saldo: {reseller.Credits}" });
        }

        // Verificar status de um pedido
        [HttpGet("status/{orderId}")]
        [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
        public async Task<IActionResult> CheckOrderStatus(int orderId)
        {
            var resellerIdClaim = User.FindFirst("ResellerId")?.Value;
            if (!int.TryParse(resellerIdClaim, out var resellerId))
                return Unauthorized();

            var order = await _context.PaymentOrders
                .FirstOrDefaultAsync(p => p.Id == orderId && p.ResellerId == resellerId);

            if (order == null)
                return NotFound();

            var reseller = await _context.Resellers.FindAsync(resellerId);
            return Ok(new { order.Status, order.PaidAt, currentCredits = reseller?.Credits ?? 0 });
        }

        // Histórico de transações do revendedor
        [HttpGet("transactions")]
        [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
        public async Task<IActionResult> GetTransactions()
        {
            var resellerIdClaim = User.FindFirst("ResellerId")?.Value;
            if (!int.TryParse(resellerIdClaim, out var resellerId))
                return Unauthorized();

            var transactions = await _context.CreditTransactions
                .Where(t => t.ResellerId == resellerId)
                .OrderByDescending(t => t.CreatedDate)
                .Take(30)
                .Select(t => new { t.Amount, t.Type, t.Description, t.CreatedDate })
                .ToListAsync();

            return Ok(transactions);
        }

        // Helper para confirmar pagamento
        private async Task ConfirmPayment(PaymentOrder order)
        {
            order.Status = "paid";
            order.PaidAt = DateTime.UtcNow;

            var reseller = await _context.Resellers.FindAsync(order.ResellerId);
            if (reseller != null)
            {
                reseller.Credits += order.Credits;
                reseller.TotalCreditsEver += order.Credits;
            }

            _context.CreditTransactions.Add(new CreditTransaction
            {
                ResellerId = order.ResellerId,
                Amount = order.Credits,
                Type = "purchase",
                Description = $"Compra PIX confirmada: {order.Credits} créditos (R$ {order.AmountCents / 100.0:F2})",
                CreatedDate = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
        }

        // Validar cupom na compra de créditos
        [HttpPost("validate-coupon")]
        [Authorize(Policy = "ResellerOnly", AuthenticationSchemes = "ResellerCookie")]
        public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                return BadRequest(new { message = "Código do cupom é obrigatório" });

            if (!CreditPackages.ContainsKey(request.Credits))
                return BadRequest(new { message = "Pacote de créditos inválido" });

            var coupon = await _context.Coupons.FindAsync(request.Code.Trim().ToUpper());
            if (coupon == null || !coupon.IsActive)
                return BadRequest(new { message = "Cupom inválido ou inativo" });

            if (coupon.ExpirationDate.HasValue && coupon.ExpirationDate.Value < DateTime.UtcNow)
                return BadRequest(new { message = "Cupom expirado" });

            if (coupon.MaxUses.HasValue && coupon.UsesCount >= coupon.MaxUses.Value)
                return BadRequest(new { message = "Cupom atingiu o limite de usos" });

            var baseAmountCents = CreditPackages[request.Credits];
            
            // Desconto no valor
            var finalAmountCents = baseAmountCents;
            if (coupon.DiscountPercentage > 0)
            {
                finalAmountCents = (int)Math.Round(baseAmountCents * (1.0 - (coupon.DiscountPercentage / 100.0)));
            }

            // Bônus nos créditos
            var finalCredits = request.Credits;
            if (coupon.BonusPercentage > 0)
            {
                finalCredits = (int)Math.Round(request.Credits * (1.0 + (coupon.BonusPercentage / 100.0)));
            }

            return Ok(new
            {
                isValid = true,
                discountPercentage = coupon.DiscountPercentage,
                bonusPercentage = coupon.BonusPercentage,
                finalAmountCents,
                finalAmountFormatted = $"R$ {finalAmountCents / 100.0:F2}",
                finalCredits,
                message = "Cupom aplicado com sucesso!"
            });
        }

        // Criar cupom (Admin)
        [HttpPost("admin/coupons")]
        [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "AdminCookie")]
        public async Task<IActionResult> CreateCoupon([FromBody] Coupon coupon)
        {
            if (string.IsNullOrWhiteSpace(coupon.Code))
                return BadRequest(new { message = "O código do cupom é obrigatório" });

            coupon.Code = coupon.Code.Trim().ToUpper();

            if (await _context.Coupons.AnyAsync(c => c.Code == coupon.Code))
                return BadRequest(new { message = "Já existe um cupom com este código" });

            coupon.CreatedDate = DateTime.UtcNow;
            _context.Coupons.Add(coupon);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cupom criado com sucesso!", coupon });
        }

        // Listar cupons (Admin)
        [HttpGet("admin/coupons")]
        [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "AdminCookie")]
        public async Task<IActionResult> GetCoupons()
        {
            var coupons = await _context.Coupons
                .OrderByDescending(c => c.CreatedDate)
                .ToListAsync();
            return Ok(coupons);
        }

        // Deletar cupom (Admin)
        [HttpDelete("admin/coupons/{code}")]
        [Authorize(Policy = "AdminOnly", AuthenticationSchemes = "AdminCookie")]
        public async Task<IActionResult> DeleteCoupon(string code)
        {
            var coupon = await _context.Coupons.FindAsync(code.Trim().ToUpper());
            if (coupon == null)
                return NotFound(new { message = "Cupom não encontrado" });

            _context.Coupons.Remove(coupon);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cupom deletado com sucesso!" });
        }

        public class CreatePixRequest
        {
            public int Credits { get; set; }
            public string? CouponCode { get; set; }
        }

        public class ValidateCouponRequest
        {
            public string Code { get; set; } = string.Empty;
            public int Credits { get; set; }
        }

        public class AdminAddCreditsRequest
        {
            public int ResellerId { get; set; }
            public int Amount { get; set; }
        }
    }
}
