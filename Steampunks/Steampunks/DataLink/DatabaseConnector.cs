using System;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;
using Steampunks.Domain.Entities;

namespace Steampunks.DataLink
{
    public class DatabaseConnector
    {
        private readonly string connectionString;
        private SqlConnection? connection;

        public DatabaseConnector()
        {
            // Local MSSQL connection string
            connectionString = @"Server=localhost;Database=SteampunksDB;Trusted_Connection=True;TrustServerCertificate=True;";
        }

        public SqlConnection GetConnection()
        {
            if (connection == null)
            {
                connection = new SqlConnection(connectionString);
            }
            return connection;
        }

        public void OpenConnection()
        {
            if (connection?.State != System.Data.ConnectionState.Open)
            {
                connection?.Open();
            }
        }

        public void CloseConnection()
        {
            if (connection?.State != System.Data.ConnectionState.Closed)
            {
                connection?.Close();
            }
        }

        public List<Game> GetAllGames()
        {
            var games = new List<Game>();
            using (var command = new SqlCommand("SELECT GameId, Title, Price, Genre, Description, Status FROM Games", GetConnection()))
            {
                try
                {
                    OpenConnection();
                    using (var reader = command.ExecuteReader())
                    {
                        bool hasGames = false;
                        while (reader.Read())
                        {
                            hasGames = true;
                            var game = new Game(
                                reader.GetString(reader.GetOrdinal("Title")),
                                (float)reader.GetDouble(reader.GetOrdinal("Price")),
                                reader.GetString(reader.GetOrdinal("Genre")),
                                reader.GetString(reader.GetOrdinal("Description"))
                            );
                            game.SetGameId(reader.GetInt32(reader.GetOrdinal("GameId")));
                            games.Add(game);
                        }

                        if (!hasGames)
                        {
                            // If no games exist, create test games
                            CloseConnection();
                            InsertTestGames();
                            return GetAllGames(); // Recursive call to get the newly inserted games
                        }
                    }
                }
                finally
                {
                    CloseConnection();
                }
            }
            return games;
        }

        private void InsertTestGames()
        {
            var testGames = new List<(string title, float price, string genre, string description)>
            {
                ("Counter-Strike 2", 0.0f, "FPS", "The next evolution of Counter-Strike"),
                ("Dota 2", 0.0f, "MOBA", "A complex game of strategy and teamwork"),
                ("Red Dead Redemption 2", 59.99f, "Action Adventure", "Epic tale of life in America's unforgiving heartland"),
                ("The Witcher 3", 39.99f, "RPG", "An epic role-playing game set in a vast open world"),
                ("Cyberpunk 2077", 59.99f, "RPG", "An open-world action-adventure story set in Night City")
            };

            using (var command = new SqlCommand(@"
                INSERT INTO Games (Title, Price, Genre, Description, Status)
                VALUES (@Title, @Price, @Genre, @Description, 'Available')", GetConnection()))
            {
                try
                {
                    OpenConnection();
                    foreach (var game in testGames)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@Title", game.title);
                        command.Parameters.AddWithValue("@Price", game.price);
                        command.Parameters.AddWithValue("@Genre", game.genre);
                        command.Parameters.AddWithValue("@Description", game.description);
                        command.ExecuteNonQuery();
                    }
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        public User GetCurrentUser()
        {
            using (var command = new SqlCommand("SELECT TOP 1 UserId, Username FROM Users", GetConnection()))
            {
                try
                {
                    OpenConnection();
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var user = new User(reader.GetString(reader.GetOrdinal("Username")));
                            user.SetUserId(reader.GetInt32(reader.GetOrdinal("UserId")));
                            return user;
                        }
                        
                        // If no users exist, create test users
                        CloseConnection();
                        InsertTestUsers();
                        return GetCurrentUser(); // Recursive call to get the newly inserted user
                    }
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        private void InsertTestUsers()
        {
            var testUsers = new List<string>
            {
                "TestUser1",
                "TestUser2",
                "TestUser3"
            };

            using (var command = new SqlCommand(@"
                INSERT INTO Users (Username, WalletBalance, PointBalance, IsDeveloper)
                VALUES (@Username, 1000, 100, 0)", GetConnection()))
            {
                try
                {
                    OpenConnection();
                    foreach (var username in testUsers)
                    {
                        command.Parameters.Clear();
                        command.Parameters.AddWithValue("@Username", username);
                        command.ExecuteNonQuery();
                    }
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        public User? GetUserByUsername(string username)
        {
            using (var command = new SqlCommand("SELECT UserId, Username FROM Users WHERE Username = @Username", GetConnection()))
            {
                command.Parameters.AddWithValue("@Username", username);
                try
                {
                    OpenConnection();
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            var user = new User(reader.GetString(reader.GetOrdinal("Username")));
                            user.SetUserId(reader.GetInt32(reader.GetOrdinal("UserId")));
                            return user;
                        }
                        return null;
                    }
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        public void CreateGameTrade(GameTrade trade)
        {
            using (var command = new SqlCommand(@"
                INSERT INTO GameTrades (SourceUserId, DestinationUserId, GameId, TradeDescription)
                VALUES (@SourceUserId, @DestinationUserId, @GameId, @TradeDescription);
                SELECT SCOPE_IDENTITY();", GetConnection()))
            {
                command.Parameters.AddWithValue("@SourceUserId", trade.GetSourceUser().UserId);
                command.Parameters.AddWithValue("@DestinationUserId", trade.GetDestinationUser().UserId);
                command.Parameters.AddWithValue("@GameId", trade.GetTradeGame().GameId);
                command.Parameters.AddWithValue("@TradeDescription", trade.TradeDescription);

                try
                {
                    OpenConnection();
                    var tradeId = Convert.ToInt32(command.ExecuteScalar());
                    trade.SetTradeId(tradeId);
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        public void ExecuteStoredProcedure(string procedureName, params SqlParameter[] parameters)
        {
            using (var command = new SqlCommand(procedureName, GetConnection()))
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;
                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                try
                {
                    OpenConnection();
                    command.ExecuteNonQuery();
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        public bool TestConnection()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    OpenConnection();
                    using (var command = new SqlCommand("SELECT 1", connection))
                    {
                        command.ExecuteScalar();
                        System.Diagnostics.Debug.WriteLine("Database query test successful!");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database query test failed: {ex.Message}");
                return false;
            }
            finally
            {
                CloseConnection();
            }
        }

        public List<GameTrade> GetActiveGameTrades()
        {
            var trades = new List<GameTrade>();
            var currentUser = GetCurrentUser();

            using (var command = new SqlCommand(@"
                SELECT 
                    t.TradeId, t.TradeDate, t.TradeDescription, t.TradeStatus,
                    t.AcceptedBySourceUser, t.AcceptedByDestinationUser,
                    su.UserId as SourceUserId, su.Username as SourceUsername,
                    du.UserId as DestUserId, du.Username as DestUsername,
                    g.GameId, g.Title, g.Price, g.Genre, g.Description
                FROM GameTrades t
                JOIN Users su ON t.SourceUserId = su.UserId
                JOIN Users du ON t.DestinationUserId = du.UserId
                JOIN Games g ON t.GameId = g.GameId
                WHERE (t.SourceUserId = @UserId OR t.DestinationUserId = @UserId)
                AND t.TradeStatus = 'Pending'
                ORDER BY t.TradeDate DESC", GetConnection()))
            {
                command.Parameters.AddWithValue("@UserId", currentUser.UserId);

                try
                {
                    OpenConnection();
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var sourceUser = new User(
                                reader.GetString(reader.GetOrdinal("SourceUsername"))
                            );
                            sourceUser.SetUserId(reader.GetInt32(reader.GetOrdinal("SourceUserId")));

                            var destUser = new User(
                                reader.GetString(reader.GetOrdinal("DestUsername"))
                            );
                            destUser.SetUserId(reader.GetInt32(reader.GetOrdinal("DestUserId")));

                            var game = new Game(
                                reader.GetString(reader.GetOrdinal("Title")),
                                (float)reader.GetDouble(reader.GetOrdinal("Price")),
                                reader.GetString(reader.GetOrdinal("Genre")),
                                reader.GetString(reader.GetOrdinal("Description"))
                            );
                            game.SetGameId(reader.GetInt32(reader.GetOrdinal("GameId")));

                            var trade = new GameTrade(sourceUser, destUser, game, reader.GetString(reader.GetOrdinal("TradeDescription")));
                            trade.SetTradeId(reader.GetInt32(reader.GetOrdinal("TradeId")));
                            trade.SetTradeStatus(reader.GetString(reader.GetOrdinal("TradeStatus")));

                            trades.Add(trade);
                        }
                    }
                }
                finally
                {
                    CloseConnection();
                }
            }
            return trades;
        }

        public void AcceptTrade(int tradeId)
        {
            var currentUser = GetCurrentUser();
            
            using (var command = new SqlCommand(@"
                UPDATE GameTrades 
                SET 
                    AcceptedBySourceUser = CASE 
                        WHEN SourceUserId = @UserId THEN 1 
                        ELSE AcceptedBySourceUser 
                    END,
                    AcceptedByDestinationUser = CASE 
                        WHEN DestinationUserId = @UserId THEN 1 
                        ELSE AcceptedByDestinationUser 
                    END,
                    TradeStatus = CASE 
                        WHEN (
                            (SourceUserId = @UserId AND DestinationUserId = @UserId) OR
                            (SourceUserId != @UserId AND AcceptedBySourceUser = 1) OR
                            (DestinationUserId != @UserId AND AcceptedByDestinationUser = 1)
                        ) THEN 'Completed'
                        ELSE 'Pending'
                    END
                WHERE TradeId = @TradeId", GetConnection()))
            {
                command.Parameters.AddWithValue("@TradeId", tradeId);
                command.Parameters.AddWithValue("@UserId", currentUser.UserId);

                try
                {
                    OpenConnection();
                    command.ExecuteNonQuery();
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        public void DeclineTrade(int tradeId)
        {
            using (var command = new SqlCommand(@"
                UPDATE GameTrades 
                SET TradeStatus = 'Declined'
                WHERE TradeId = @TradeId", GetConnection()))
            {
                command.Parameters.AddWithValue("@TradeId", tradeId);

                try
                {
                    OpenConnection();
                    command.ExecuteNonQuery();
                }
                finally
                {
                    CloseConnection();
                }
            }
        }
    }
} 