using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelgaAuthAPI.Models
{
    public class Subscription
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
        
        [Required]
        [MaxLength(50)]
        public string SubscriptionName { get; set; } = string.Empty;
        
        [Required]
        public long Expiry { get; set; } // Unix timestamp
        
        public bool IsPaused { get; set; } = false;
        
        public long? PausedAt { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}

