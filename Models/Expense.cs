namespace NetSplit.Models
{
    public enum SplitType
    {
        Equal = 1,
        Custom = 2
    }

    public class Expense
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public int PaidByUserId { get; set; }
        public string PaidByName { get; set; } = string.Empty;
        public SplitType SplitType { get; set; } = SplitType.Equal;
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<ExpenseShare> Shares { get; set; } = new();
    }

    public class ExpenseShare
    {
        public int Id { get; set; }
        public int ExpenseId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal AmountOwed { get; set; }
    }
}
