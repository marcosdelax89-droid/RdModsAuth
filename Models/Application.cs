using System.ComponentModel.DataAnnotations;

namespace BelgaAuthAPI.Models
{
    public class Application
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(10)]
        public string OwnerId { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(64)]
        public string Secret { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string Version { get; set; } = "1.0";
        
        [MaxLength(500)]
        public string? DownloadLink { get; set; }
        
        [MaxLength(500)]
        public string? CustomerPanelLink { get; set; }
        
        public int TotalUsers { get; set; } = 0;
        
        public int OnlineUsers { get; set; } = 0;
        
        public int TotalLicenses { get; set; } = 0;
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}

