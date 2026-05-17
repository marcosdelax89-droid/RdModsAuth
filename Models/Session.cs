using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BelgaAuthAPI.Models
{
    public class Session
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;
        
        public int? UserId { get; set; }
        
        [ForeignKey("UserId")]
        public User? User { get; set; }
        
        [Required]
        public int ApplicationId { get; set; }
        
        [ForeignKey("ApplicationId")]
        public Application Application { get; set; } = null!;
        
        public string? IP { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
    }
}

