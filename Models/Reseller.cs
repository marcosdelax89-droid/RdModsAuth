using System.ComponentModel.DataAnnotations;

namespace BelgaAuthAPI.Models
{
    public class Reseller
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        
        public string? PasswordPlain { get; set; }
        
        [MaxLength(100)]
        public string? Email { get; set; }
        
        [MaxLength(100)]
        public string? Name { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastLogin { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        public string? Notes { get; set; }
        
        // Estatísticas do revendedor
        public int TotalLicensesCreated { get; set; } = 0;
        
        public int TotalLicensesUsed { get; set; } = 0;
        
        // Sistema de créditos
        public int Credits { get; set; } = 0;
        public int TotalCreditsEver { get; set; } = 0;
        
        // Licenças criadas por este revendedor
        public List<License> Licenses { get; set; } = new();
    }
}




