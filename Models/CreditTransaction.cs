using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelgaAuthAPI.Models
{
    public class CreditTransaction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ResellerId { get; set; }

        [ForeignKey("ResellerId")]
        public Reseller Reseller { get; set; } = null!;

        // Positivo = créditos adicionados, Negativo = créditos usados
        public int Amount { get; set; }

        // "purchase", "usage", "admin_add", "admin_remove"
        [MaxLength(30)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Description { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
