namespace BelgaAuthAPI.DTOs
{
    public class AuthRequest
    {
        public string Type { get; set; } = string.Empty;
        public string? Username { get; set; }
        public string? Pass { get; set; }
        public string? Key { get; set; }
        public string? Email { get; set; }
        public string? HWID { get; set; }
        public string? SessionId { get; set; }
        public string? Name { get; set; }
        public string? OwnerId { get; set; }
        public string? Ver { get; set; }
        public string? Hash { get; set; }
        public string? Enckey { get; set; }
        public string? Var { get; set; }
        public string? Data { get; set; }
        public string? VarId { get; set; }
        public string? Reason { get; set; }
        public string? NewUsername { get; set; }
        public string? Token { get; set; }

        // Aliases para compatibilidade entre o Form Web e o SDK C++ / KeyAuth
        public string? Password 
        { 
            get => Pass; 
            set => Pass = value; 
        }
        
        public string? License 
        { 
            get => Key; 
            set => Key = value; 
        }
    }
}

