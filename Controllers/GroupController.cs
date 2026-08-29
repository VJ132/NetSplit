using Microsoft.AspNetCore.Mvc;
using NetSplit.Data;
using NetSplit.Helpers;
using NetSplit.Models;

namespace NetSplit.Controllers
{
    public class GroupController : Controller
    {
        private readonly Database _database;

        public GroupController(Database database)
        {
            _database = database;
        }

        private int? GetCurrentUserId() => HttpContext.Session.GetInt32("UserId");

        [HttpGet]
        public IActionResult Create()
        {
            if (GetCurrentUserId() == null) return RedirectToAction("Login", "Account");
            return View(new CreateGroupViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateGroupViewModel model)
        {
            int? userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid) return View(model);

            using var connection = _database.GetConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Create Group
                var groupCmd = connection.CreateCommand();
                groupCmd.Transaction = transaction;
                groupCmd.CommandText = @"
                    INSERT INTO Groups (Name, Description, Currency, CreatedByUserId, CreatedAt)
                    VALUES (@Name, @Description, @Currency, @CreatedByUserId, @CreatedAt);
                    SELECT last_insert_rowid();
                ";
                groupCmd.Parameters.AddWithValue("@Name", model.Name.Trim());
                groupCmd.Parameters.AddWithValue("@Description", model.Description?.Trim() ?? "");
                groupCmd.Parameters.AddWithValue("@Currency", model.Currency);
                groupCmd.Parameters.AddWithValue("@CreatedByUserId", userId.Value);
                groupCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                long groupId = (long)(groupCmd.ExecuteScalar() ?? 0);

                // Add creator as member
                var memberCmd = connection.CreateCommand();
                memberCmd.Transaction = transaction;
                memberCmd.CommandText = @"
                    INSERT INTO GroupMembers (GroupId, UserId, JoinedAt)
                    VALUES (@GroupId, @UserId, @JoinedAt);
                ";
                memberCmd.Parameters.AddWithValue("@GroupId", groupId);
                memberCmd.Parameters.AddWithValue("@UserId", userId.Value);
                memberCmd.Parameters.AddWithValue("@JoinedAt", DateTime.UtcNow);
                memberCmd.ExecuteNonQuery();

                transaction.Commit();

                TempData["SuccessMessage"] = $"Group '{model.Name}' created successfully!";
                return RedirectToAction("Details", new { id = groupId });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ModelState.AddModelError(string.Empty, "An error occurred while creating group: " + ex.Message);
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            int? userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            using var connection = _database.GetConnection();
            connection.Open();

            // Authorization check & load group
            var groupCmd = connection.CreateCommand();
            groupCmd.CommandText = @"
                SELECT g.Id, g.Name, g.Description, g.Currency, g.CreatedByUserId, g.CreatedAt
                FROM Groups g
                INNER JOIN GroupMembers gm ON g.Id = gm.GroupId
                WHERE g.Id = @GroupId AND gm.UserId = @UserId;
            ";
            groupCmd.Parameters.AddWithValue("@GroupId", id);
            groupCmd.Parameters.AddWithValue("@UserId", userId.Value);

            Group? group = null;
            using (var reader = groupCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    group = new Group
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                        Currency = reader.GetString(3),
                        CreatedByUserId = reader.GetInt32(4),
                        CreatedAt = reader.GetDateTime(5)
                    };
                }
            }

            if (group == null)
            {
                TempData["ErrorMessage"] = "Group not found or you are not a member of this group.";
                return RedirectToAction("Index", "Home");
            }

            // Load Members
            var members = new List<GroupMember>();
            var memberCmd = connection.CreateCommand();
            memberCmd.CommandText = @"
                SELECT gm.Id, gm.GroupId, gm.UserId, gm.JoinedAt, u.FullName, u.Email
                FROM GroupMembers gm
                INNER JOIN Users u ON gm.UserId = u.Id
                WHERE gm.GroupId = @GroupId
                ORDER BY u.FullName;
            ";
            memberCmd.Parameters.AddWithValue("@GroupId", id);
            using (var reader = memberCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    members.Add(new GroupMember
                    {
                        Id = reader.GetInt32(0),
                        GroupId = reader.GetInt32(1),
                        UserId = reader.GetInt32(2),
                        JoinedAt = reader.GetDateTime(3),
                        FullName = reader.GetString(4),
                        Email = reader.GetString(5)
                    });
                }
            }

            // Load Expenses with Shares
            var expenses = new List<Expense>();
            var expCmd = connection.CreateCommand();
            expCmd.CommandText = @"
                SELECT e.Id, e.GroupId, e.Title, e.Amount, e.Currency, e.PaidByUserId, u.FullName as PaidByName, e.SplitType, e.CreatedByUserId, e.CreatedAt
                FROM Expenses e
                INNER JOIN Users u ON e.PaidByUserId = u.Id
                WHERE e.GroupId = @GroupId
                ORDER BY e.CreatedAt DESC;
            ";
            expCmd.Parameters.AddWithValue("@GroupId", id);
            using (var reader = expCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    expenses.Add(new Expense
                    {
                        Id = reader.GetInt32(0),
                        GroupId = reader.GetInt32(1),
                        Title = reader.GetString(2),
                        Amount = reader.GetDecimal(3),
                        Currency = reader.GetString(4),
                        PaidByUserId = reader.GetInt32(5),
                        PaidByName = reader.GetString(6),
                        SplitType = (SplitType)reader.GetInt32(7),
                        CreatedByUserId = reader.GetInt32(8),
                        CreatedAt = reader.GetDateTime(9)
                    });
                }
            }

            // Load Expense Shares
            foreach (var exp in expenses)
            {
                var shareCmd = connection.CreateCommand();
                shareCmd.CommandText = @"
                    SELECT es.Id, es.ExpenseId, es.UserId, es.AmountOwed, u.FullName
                    FROM ExpenseShares es
                    INNER JOIN Users u ON es.UserId = u.Id
                    WHERE es.ExpenseId = @ExpenseId;
                ";
                shareCmd.Parameters.AddWithValue("@ExpenseId", exp.Id);
                using var reader = shareCmd.ExecuteReader();
                while (reader.Read())
                {
                    exp.Shares.Add(new ExpenseShare
                    {
                        Id = reader.GetInt32(0),
                        ExpenseId = reader.GetInt32(1),
                        UserId = reader.GetInt32(2),
                        AmountOwed = reader.GetDecimal(3),
                        UserName = reader.GetString(4)
                    });
                }
            }

            // Load Settlements
            var settlements = new List<Settlement>();
            var setCmd = connection.CreateCommand();
            setCmd.CommandText = @"
                SELECT s.Id, s.GroupId, s.PayerUserId, u1.FullName, s.ReceiverUserId, u2.FullName, s.Amount, s.Currency, s.Status, s.CreatedAt, s.PaidAt
                FROM Settlements s
                INNER JOIN Users u1 ON s.PayerUserId = u1.Id
                INNER JOIN Users u2 ON s.ReceiverUserId = u2.Id
                WHERE s.GroupId = @GroupId
                ORDER BY s.CreatedAt DESC;
            ";
            setCmd.Parameters.AddWithValue("@GroupId", id);
            using (var reader = setCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    settlements.Add(new Settlement
                    {
                        Id = reader.GetInt32(0),
                        GroupId = reader.GetInt32(1),
                        PayerUserId = reader.GetInt32(2),
                        PayerName = reader.GetString(3),
                        ReceiverUserId = reader.GetInt32(4),
                        ReceiverName = reader.GetString(5),
                        Amount = reader.GetDecimal(6),
                        Currency = reader.GetString(7),
                        Status = (SettlementStatus)reader.GetInt32(8),
                        CreatedAt = reader.GetDateTime(9),
                        PaidAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10)
                    });
                }
            }

            group.Members = members;
            group.TotalExpenses = expenses.Sum(e => e.Amount);

            // Compute balances and simplified debts
            var userBalances = DebtSimplifier.CalculateUserBalances(members, expenses, settlements);
            var simplifiedDebts = DebtSimplifier.SimplifyDebts(userBalances, group.Currency);

            var viewModel = new GroupDashboardViewModel
            {
                Group = group,
                Members = members,
                Expenses = expenses,
                UserBalances = userBalances,
                SimplifiedDebts = simplifiedDebts,
                Settlements = settlements,
                CurrentUserId = userId.Value
            };

            return View(viewModel);
        }
    }
}
