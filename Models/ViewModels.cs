using System.ComponentModel.DataAnnotations;

namespace NetSplit.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, ErrorMessage = "Full Name cannot exceed 100 characters.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm Password is required.")]
        [Compare("Password", ErrorMessage = "Password and Confirm Password do not match.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }

    public class CreateGroupViewModel
    {
        [Required(ErrorMessage = "Group name is required.")]
        [StringLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Currency is required.")]
        public string Currency { get; set; } = "INR";
    }

    public class AddMemberViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Member email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid registered email address.")]
        public string Email { get; set; } = string.Empty;
    }

    public class AddExpenseViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Currency { get; set; } = "INR";

        [Required(ErrorMessage = "Expense title is required.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, 10000000.00, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Please select who paid.")]
        public int PaidByUserId { get; set; }

        public SplitType SplitType { get; set; } = SplitType.Equal;

        public List<int> SelectedParticipantIds { get; set; } = new();

        // Key: UserId, Value: Custom share amount
        public Dictionary<int, decimal> CustomShares { get; set; } = new();

        public List<GroupMember> AvailableMembers { get; set; } = new();
    }

    public class UserBalance
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal TotalPaid { get; set; }
        public decimal TotalOwed { get; set; }
        public decimal TotalSettlementsPaid { get; set; }
        public decimal TotalSettlementsReceived { get; set; }
        public decimal NetBalance { get; set; } // + means should receive, - means owes
    }

    public class SimplifiedDebt
    {
        public int DebtId { get; set; }
        public int DebtorId { get; set; }
        public string DebtorName { get; set; } = string.Empty;
        public int CreditorId { get; set; }
        public string CreditorName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public List<string> ExplanationSteps { get; set; } = new();
    }

    public class GroupDashboardViewModel
    {
        public Group Group { get; set; } = new();
        public List<GroupMember> Members { get; set; } = new();
        public List<Expense> Expenses { get; set; } = new();
        public List<UserBalance> UserBalances { get; set; } = new();
        public List<SimplifiedDebt> SimplifiedDebts { get; set; } = new();
        public List<Settlement> Settlements { get; set; } = new();
        public int CurrentUserId { get; set; }
    }
}
