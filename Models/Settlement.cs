namespace NetSplit.Models
{
    public enum SettlementStatus
    {
        Pending = 1,
        Paid = 2
    }

    public class Settlement
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int PayerUserId { get; set; }
        public string PayerName { get; set; } = string.Empty;
        public int ReceiverUserId { get; set; }
        public string ReceiverName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public SettlementStatus Status { get; set; } = SettlementStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }
    }
}
