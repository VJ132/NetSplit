using Microsoft.AspNetCore.Mvc;
using NetSplit.Data;
using NetSplit.Models;

namespace NetSplit.Controllers
{
    public class ExpenseController : Controller
    {
        private readonly Database _database;

        public ExpenseController(Database database)
        {
            _database = database;
        }

        private int? GetCurrentUserId() => HttpContext.Session.GetInt32("UserId");

        [HttpGet]
        public IActionResult Create(int groupId)
        {
            int? userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            using var connection = _database.GetConnection();
            connection.Open();

            // Fetch Group Info & Members
            var groupCmd = connection.CreateCommand();
            groupCmd.CommandText = "SELECT Name, Currency FROM Groups WHERE Id = @GroupId";
            groupCmd.Parameters.AddWithValue("@GroupId", groupId);

            string groupName = "";
            string currency = "INR";
            using (var reader = groupCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    groupName = reader.GetString(0);
                    currency = reader.GetString(1);
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            var members = new List<GroupMember>();
            var memberCmd = connection.CreateCommand();
            memberCmd.CommandText = @"
                SELECT gm.UserId, u.FullName, u.Email
                FROM GroupMembers gm
                INNER JOIN Users u ON gm.UserId = u.Id
                WHERE gm.GroupId = @GroupId
                ORDER BY u.FullName;
            ";
            memberCmd.Parameters.AddWithValue("@GroupId", groupId);
            using (var reader = memberCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    members.Add(new GroupMember
                    {
                        UserId = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Email = reader.GetString(2)
                    });
                }
            }

            var viewModel = new AddExpenseViewModel
            {
                GroupId = groupId,
                GroupName = groupName,
                Currency = currency,
                PaidByUserId = userId.Value,
                AvailableMembers = members,
                SelectedParticipantIds = members.Select(m => m.UserId).ToList() // default select all
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(AddExpenseViewModel model)
        {
            int? userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            using var connection = _database.GetConnection();
            connection.Open();

            // Re-fetch members for validation redisplay
            var members = new List<GroupMember>();
            var memberCmd = connection.CreateCommand();
            memberCmd.CommandText = @"
                SELECT gm.UserId, u.FullName, u.Email
                FROM GroupMembers gm
                INNER JOIN Users u ON gm.UserId = u.Id
                WHERE gm.GroupId = @GroupId
                ORDER BY u.FullName;
            ";
            memberCmd.Parameters.AddWithValue("@GroupId", model.GroupId);
            using (var reader = memberCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    members.Add(new GroupMember
                    {
                        UserId = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Email = reader.GetString(2)
                    });
                }
            }
            model.AvailableMembers = members;

            if (model.Amount <= 0)
            {
                ModelState.AddModelError("Amount", "Expense amount must be greater than zero.");
            }

            if (model.SelectedParticipantIds == null || !model.SelectedParticipantIds.Any())
            {
                ModelState.AddModelError("SelectedParticipantIds", "Select at least one participant to split the expense.");
            }

            var shares = new Dictionary<int, decimal>();

            if (model.SplitType == SplitType.Equal)
            {
                if (model.SelectedParticipantIds != null && model.SelectedParticipantIds.Any())
                {
                    int count = model.SelectedParticipantIds.Count;
                    decimal baseShare = Math.Floor((model.Amount / count) * 100m) / 100m;
                    decimal remainder = model.Amount - (baseShare * count);

                    for (int i = 0; i < count; i++)
                    {
                        int pId = model.SelectedParticipantIds[i];
                        // Add cent/penny remainder to first participant to guarantee exact total sum
                        decimal participantShare = baseShare + (i == 0 ? remainder : 0m);
                        shares[pId] = participantShare;
                    }
                }
            }
            else if (model.SplitType == SplitType.Custom)
            {
                decimal totalCustomSum = 0m;
                foreach (var pId in model.SelectedParticipantIds ?? new List<int>())
                {
                    if (model.CustomShares != null && model.CustomShares.TryGetValue(pId, out decimal shareVal))
                    {
                        if (shareVal < 0)
                        {
                            ModelState.AddModelError(string.Empty, "Share amounts cannot be negative.");
                        }
                        shares[pId] = shareVal;
                        totalCustomSum += shareVal;
                    }
                    else
                    {
                        shares[pId] = 0m;
                    }
                }

                if (Math.Abs(totalCustomSum - model.Amount) > 0.01m)
                {
                    ModelState.AddModelError(string.Empty, $"Custom split sum ({model.Currency} {totalCustomSum:N2}) does not match the total expense amount ({model.Currency} {model.Amount:N2}).");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Save Expense and Shares in ADO.NET Transaction
            using var transaction = connection.BeginTransaction();
            try
            {
                var expCmd = connection.CreateCommand();
                expCmd.Transaction = transaction;
                expCmd.CommandText = @"
                    INSERT INTO Expenses (GroupId, Title, Amount, Currency, PaidByUserId, SplitType, CreatedByUserId, CreatedAt)
                    VALUES (@GroupId, @Title, @Amount, @Currency, @PaidByUserId, @SplitType, @CreatedByUserId, @CreatedAt);
                    SELECT last_insert_rowid();
                ";
                expCmd.Parameters.AddWithValue("@GroupId", model.GroupId);
                expCmd.Parameters.AddWithValue("@Title", model.Title.Trim());
                expCmd.Parameters.AddWithValue("@Amount", model.Amount);
                expCmd.Parameters.AddWithValue("@Currency", model.Currency);
                expCmd.Parameters.AddWithValue("@PaidByUserId", model.PaidByUserId);
                expCmd.Parameters.AddWithValue("@SplitType", (int)model.SplitType);
                expCmd.Parameters.AddWithValue("@CreatedByUserId", userId.Value);
                expCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

                long expenseId = (long)(expCmd.ExecuteScalar() ?? 0);

                foreach (var kvp in shares)
                {
                    var shareCmd = connection.CreateCommand();
                    shareCmd.Transaction = transaction;
                    shareCmd.CommandText = @"
                        INSERT INTO ExpenseShares (ExpenseId, UserId, AmountOwed)
                        VALUES (@ExpenseId, @UserId, @AmountOwed);
                    ";
                    shareCmd.Parameters.AddWithValue("@ExpenseId", expenseId);
                    shareCmd.Parameters.AddWithValue("@UserId", kvp.Key);
                    shareCmd.Parameters.AddWithValue("@AmountOwed", kvp.Value);
                    shareCmd.ExecuteNonQuery();
                }

                transaction.Commit();
                TempData["SuccessMessage"] = $"Expense '{model.Title}' added successfully!";
                return RedirectToAction("Details", "Group", new { id = model.GroupId });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                ModelState.AddModelError(string.Empty, "Error saving expense: " + ex.Message);
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

            var expCmd = connection.CreateCommand();
            expCmd.CommandText = @"
                SELECT e.Id, e.GroupId, e.Title, e.Amount, e.Currency, e.PaidByUserId, u.FullName as PaidByName, e.SplitType, e.CreatedAt, g.Name as GroupName
                FROM Expenses e
                INNER JOIN Users u ON e.PaidByUserId = u.Id
                INNER JOIN Groups g ON e.GroupId = g.Id
                WHERE e.Id = @ExpenseId;
            ";
            expCmd.Parameters.AddWithValue("@ExpenseId", id);

            Expense? expense = null;
            string groupName = "";
            using (var reader = expCmd.ExecuteReader())
            {
                if (reader.Read())
                {
                    expense = new Expense
                    {
                        Id = reader.GetInt32(0),
                        GroupId = reader.GetInt32(1),
                        Title = reader.GetString(2),
                        Amount = reader.GetDecimal(3),
                        Currency = reader.GetString(4),
                        PaidByUserId = reader.GetInt32(5),
                        PaidByName = reader.GetString(6),
                        SplitType = (SplitType)reader.GetInt32(7),
                        CreatedAt = reader.GetDateTime(8)
                    };
                    groupName = reader.GetString(9);
                }
            }

            if (expense == null) return RedirectToAction("Index", "Home");

            // Load Shares
            var shareCmd = connection.CreateCommand();
            shareCmd.CommandText = @"
                SELECT es.Id, es.ExpenseId, es.UserId, es.AmountOwed, u.FullName
                FROM ExpenseShares es
                INNER JOIN Users u ON es.UserId = u.Id
                WHERE es.ExpenseId = @ExpenseId;
            ";
            shareCmd.Parameters.AddWithValue("@ExpenseId", id);
            using (var reader = shareCmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    expense.Shares.Add(new ExpenseShare
                    {
                        Id = reader.GetInt32(0),
                        ExpenseId = reader.GetInt32(1),
                        UserId = reader.GetInt32(2),
                        AmountOwed = reader.GetDecimal(3),
                        UserName = reader.GetString(4)
                    });
                }
            }

            ViewBag.GroupName = groupName;
            return View(expense);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RecordSettlement(int groupId, int debtorId, int creditorId, decimal amount, string currency)
        {
            int? userId = GetCurrentUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            using var connection = _database.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Settlements (GroupId, PayerUserId, ReceiverUserId, Amount, Currency, Status, CreatedAt, PaidAt)
                VALUES (@GroupId, @PayerUserId, @ReceiverUserId, @Amount, @Currency, @Status, @CreatedAt, @PaidAt);
            ";
            cmd.Parameters.AddWithValue("@GroupId", groupId);
            cmd.Parameters.AddWithValue("@PayerUserId", debtorId);
            cmd.Parameters.AddWithValue("@ReceiverUserId", creditorId);
            cmd.Parameters.AddWithValue("@Amount", amount);
            cmd.Parameters.AddWithValue("@Currency", currency);
            cmd.Parameters.AddWithValue("@Status", (int)SettlementStatus.Paid);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@PaidAt", DateTime.UtcNow);

            cmd.ExecuteNonQuery();

            TempData["SuccessMessage"] = "Payment settlement marked as PAID successfully!";
            return RedirectToAction("Details", "Group", new { id = groupId });
        }
    }
}
