using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelgaAuthAPI.Models
{
    public class PaymentOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ResellerId { get; set; }

        [ForeignKey("ResellerId")]
        public Reseller Reseller { get; set; } = null!;

        // ID de correlação do OpenPix para rastrear o pagamento
        [MaxLength(100)]
        public string? PixCorrelationId { get; set; }

        // Valor em centavos (ex: R$10.00 = 1000)
        public int AmountCents { get; set; }

        // Quantidade de créditos que serão adicionados ao confirmar
        public int Credits { get; set; }

        // "pending", "paid", "expired", "cancelled"
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        // URL do QR Code para pagamento
        public string? QrCodeUrl { get; set; }

        // Código copia-e-cola do PIX
        public string? PixCopyPaste { get; set; }

        public string? CouponCode { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
    }
}
