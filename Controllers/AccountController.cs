using Microsoft.AspNetCore.Mvc;
using NetSplit.Data;
using NetSplit.Helpers;
using NetSplit.Models;

namespace NetSplit.Controllers
{
    public class AccountController : Controller
    {
        private readonly Database _database;

        public AccountController(Database database)
        {
            _database = database;
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            using var connection = _database.GetConnection();
            connection.Open();

            // Check if email already exists
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
            checkCmd.Parameters.AddWithValue("@Email", model.Email.Trim().ToLower());
            long count = (long)(checkCmd.ExecuteScalar() ?? 0);

            if (count > 0)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            // Hash password and insert user
            string passwordHash = PasswordHasher.HashPassword(model.Password);

            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO Users (FullName, Email, PasswordHash, CreatedAt)
                VALUES (@FullName, @Email, @PasswordHash, @CreatedAt);
            ";
            insertCmd.Parameters.AddWithValue("@FullName", model.FullName.Trim());
            insertCmd.Parameters.AddWithValue("@Email", model.Email.Trim().ToLower());
            insertCmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
            insertCmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow);

            insertCmd.ExecuteNonQuery();

            TempData["SuccessMessage"] = "Registration successful! Please log in with your credentials.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetInt32("UserId") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            using var connection = _database.GetConnection();
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, FullName, Email, PasswordHash FROM Users WHERE Email = @Email";
            cmd.Parameters.AddWithValue("@Email", model.Email.Trim().ToLower());

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                int userId = reader.GetInt32(0);
                string fullName = reader.GetString(1);
                string email = reader.GetString(2);
                string storedHash = reader.GetString(3);

                if (PasswordHasher.VerifyPassword(model.Password, storedHash))
                {
                    // Set session
                    HttpContext.Session.SetInt32("UserId", userId);
                    HttpContext.Session.SetString("UserName", fullName);
                    HttpContext.Session.SetString("UserEmail", email);

                    return RedirectToAction("Index", "Home");
                }
            }

            ModelState.AddModelError(string.Empty, "Invalid email address or password.");
            return View(model);
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}
