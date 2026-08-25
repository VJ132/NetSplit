using Microsoft.Data.Sqlite;
using NetSplit.Models;

namespace NetSplit.Data
{
    public class Database
    {
        private readonly string _connectionString;

        public Database(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=netsplit.db";
        }

        public SqliteConnection GetConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        public void InitializeDatabase()
        {
            using var connection = GetConnection();
            connection.Open();

            var tableCmd = connection.CreateCommand();
            tableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FullName TEXT NOT NULL,
                    Email TEXT NOT NULL UNIQUE,
                    PasswordHash TEXT NOT NULL,
                    CreatedAt DATETIME NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Groups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT NULL,
                    Currency TEXT NOT NULL DEFAULT 'INR',
                    CreatedByUserId INTEGER NOT NULL,
                    CreatedAt DATETIME NOT NULL,
                    FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id)
                );

                CREATE TABLE IF NOT EXISTS GroupMembers (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    JoinedAt DATETIME NOT NULL,
                    FOREIGN KEY (GroupId) REFERENCES Groups(Id),
                    FOREIGN KEY (UserId) REFERENCES Users(Id),
                    UNIQUE(GroupId, UserId)
                );

                CREATE TABLE IF NOT EXISTS Expenses (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    Title TEXT NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    Currency TEXT NOT NULL DEFAULT 'INR',
                    PaidByUserId INTEGER NOT NULL,
                    SplitType INTEGER NOT NULL,
                    CreatedByUserId INTEGER NOT NULL,
                    CreatedAt DATETIME NOT NULL,
                    FOREIGN KEY (GroupId) REFERENCES Groups(Id),
                    FOREIGN KEY (PaidByUserId) REFERENCES Users(Id)
                );

                CREATE TABLE IF NOT EXISTS ExpenseShares (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExpenseId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    AmountOwed DECIMAL(18,2) NOT NULL,
                    FOREIGN KEY (ExpenseId) REFERENCES Expenses(Id) ON DELETE CASCADE,
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                );

                CREATE TABLE IF NOT EXISTS Settlements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    PayerUserId INTEGER NOT NULL,
                    ReceiverUserId INTEGER NOT NULL,
                    Amount DECIMAL(18,2) NOT NULL,
                    Currency TEXT NOT NULL DEFAULT 'INR',
                    Status INTEGER NOT NULL DEFAULT 1,
                    CreatedAt DATETIME NOT NULL,
                    PaidAt DATETIME NULL,
                    FOREIGN KEY (GroupId) REFERENCES Groups(Id),
                    FOREIGN KEY (PayerUserId) REFERENCES Users(Id),
                    FOREIGN KEY (ReceiverUserId) REFERENCES Users(Id)
                );
            ";
            tableCmd.ExecuteNonQuery();
        }
    }
}
