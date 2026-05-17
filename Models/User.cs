using System.ComponentModel.DataAnnotations;

namespace BelgaAuthAPI.Models
{
    public class User
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
        public string? HWID { get; set; }
        
        public string? IP { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastLogin { get; set; }
        
        public bool IsBanned { get; set; } = false;
        
        public string? BanReason { get; set; }
        
        // Se o usuário pode fazer login no painel admin
        public bool IsAdmin { get; set; } = false;
        
        // ID do revendedor que criou este usuário (null se foi criado pelo admin)
        public int? CreatedByResellerId { get; set; }
        
        // Navegação para o revendedor
        public Reseller? CreatedByReseller { get; set; }
        
        // Controle de Reset de HWID
        public DateTime? LastHWIDReset { get; set; }
        
        // Controle se o usuário já pausou a conta uma vez
        public bool HasPaused { get; set; } = false;
        
        public List<Subscription> Subscriptions { get; set; } = new();
        
        public List<UserVariable> Variables { get; set; } = new();
    }
}

