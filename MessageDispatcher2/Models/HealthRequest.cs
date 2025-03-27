namespace MessageDispatcher2.Models
{
    public class HealthRequest
    {
        public string? Id { get; set; }
        public DateTime SystemTime { get; set; }
        public int NumberOfConnectedClients { get; set; }
    }
}