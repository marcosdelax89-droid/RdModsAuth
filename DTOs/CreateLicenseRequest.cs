namespace BelgaAuthAPI.DTOs
{
    public class CreateLicenseRequestDto
    {
        public string? SubscriptionName { get; set; }
        public int DaysValid { get; set; } = 30;
        public string? CustomKey { get; set; }
        public string? ResellerPrefix { get; set; }
    }
}




