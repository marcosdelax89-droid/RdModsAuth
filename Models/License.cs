using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelgaAuthAPI.Models
{
    public class License
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(50)]
        public string SubscriptionName { get; set; } = string.Empty;
        
        public int DaysValid { get; set; } = 30;
        
        public bool IsUsed { get; set; } = false;
        
        public int? UsedByUserId { get; set; }
        
        public DateTime? UsedDate { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        public DateTime? ExpiryDate { get; set; }
        
        // Revendedor que criou esta licença
        public int? ResellerId { get; set; }
        
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("ResellerId")]
        public Reseller? Reseller { get; set; }
    }
}

