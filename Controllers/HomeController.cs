using Microsoft.AspNetCore.Mvc;
using NetSplit.Data;
using NetSplit.Models;

namespace NetSplit.Controllers
{
    public class HomeController : Controller
    {
        private readonly Database _database;

        public HomeController(Database database)
        {
            _database = database;
        }

        public IActionResult Index()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userGroups = new List<Group>();

            using var connection = _database.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT g.Id, g.Name, g.Description, g.Currency, g.CreatedByUserId, g.CreatedAt,
                       (SELECT COUNT(*) FROM GroupMembers gm WHERE gm.GroupId = g.Id) as MemberCount,
                       (SELECT COALESCE(SUM(e.Amount), 0) FROM Expenses e WHERE e.GroupId = g.Id) as TotalExpenses
                FROM Groups g
                INNER JOIN GroupMembers gm ON g.Id = gm.GroupId
                WHERE gm.UserId = @UserId
                ORDER BY g.CreatedAt DESC;
            ";
            cmd.Parameters.AddWithValue("@UserId", userId.Value);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var group = new Group
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Currency = reader.GetString(3),
                    CreatedByUserId = reader.GetInt32(4),
                    CreatedAt = reader.GetDateTime(5),
                    TotalExpenses = reader.GetDecimal(7)
                };

                int memberCount = reader.GetInt32(6);
                for (int i = 0; i < memberCount; i++)
                {
                    group.Members.Add(new GroupMember());
                }

                userGroups.Add(group);
            }

            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            return View(userGroups);
        }
    }
}
