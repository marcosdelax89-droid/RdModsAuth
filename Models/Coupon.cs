using System.ComponentModel.DataAnnotations;

namespace BelgaAuthAPI.Models
{
    public class Coupon
    {
        [Key]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        // Desconto no preço em % (ex: 10 = 10% de desconto)
        public int DiscountPercentage { get; set; } = 0;

        // Bônus em % de créditos adicionais (ex: 20 = 20% a mais de créditos)
        public int BonusPercentage { get; set; } = 0;

        public int? MaxUses { get; set; }
        public int UsesCount { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? ExpirationDate { get; set; }
    }
}
