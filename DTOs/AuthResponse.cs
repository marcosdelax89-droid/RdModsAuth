namespace BelgaAuthAPI.DTOs
{
    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public bool NewSession { get; set; }
        public string? Contents { get; set; }
        public string? Response { get; set; }
        public string? Download { get; set; }
        public UserInfo? Info { get; set; }
        public AppInfo? AppInfo { get; set; }
        public List<Message>? Messages { get; set; }
        public List<UserCredential>? Users { get; set; }
    }

    public class UserInfo
    {
        public string Username { get; set; } = string.Empty;
        public string? IP { get; set; }
        public string? HWID { get; set; }
        public string Createdate { get; set; } = string.Empty;
        public string Lastlogin { get; set; } = string.Empty;
        public List<Subscription>? Subscriptions { get; set; }
    }

    public class Subscription
    {
        public string SubscriptionName { get; set; } = string.Empty;
        public string Expiry { get; set; } = string.Empty;
        public string Timeleft { get; set; } = string.Empty;
    }

    public class AppInfo
    {
        public string NumUsers { get; set; } = "0";
        public string NumOnlineUsers { get; set; } = "0";
        public string NumKeys { get; set; } = "0";
        public string Version { get; set; } = string.Empty;
        public string? CustomerPanelLink { get; set; }
        public string? DownloadLink { get; set; }
    }

    public class Message
    {
        public string Content { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
    }

    public class UserCredential
    {
        public string Credential { get; set; } = string.Empty;
    }
}

