namespace ManagementSystem2.Models
{
    public class HealthResponse
    {
        public bool IsEnabled { get; set; }
        public int NumberOfActiveClients { get; set; }
        public DateTime ExpirationTime { get; set; }
    }
}