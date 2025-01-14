using Microsoft.Data.SqlClient;
using Project.Models;
using System.Data;

namespace Project.Models
{
    public class ProjectMethods
    {
        public ProjectMethods() { }

        // Connection string for the database
        private string connectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=Project;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False";

        /**** 
         * Logga In
        ****/
        public UserModel AuthenticateUser(string username, string password, out string errorMsg)
        {
            errorMsg = string.Empty;
            UserModel user = null;

            string query = "SELECT Id, Name, Points, Password FROM [User] WHERE Name = @UserName AND Password = @Password";

            using (SqlConnection sqlConnection = new SqlConnection(connectionString))
            {
                SqlCommand sqlCommand = new SqlCommand(query, sqlConnection);
                sqlCommand.Parameters.AddWithValue("@UserName", username);
                sqlCommand.Parameters.AddWithValue("@Password", password);

                try
                {
                    sqlConnection.Open();
                    SqlDataReader reader = sqlCommand.ExecuteReader();

                    if (reader.Read())
                    {
                        user = new UserModel
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Points = reader.GetInt32(2),
                            Password = reader.GetString(3)
                        };
                    }
                }
                catch (Exception ex)
                {
                    errorMsg = ex.Message;
                }
            }

            return user;
        }
        /**** 
         * Spelbräda
        ****/

        public List<GameStatusModel> GetAnimalNames()
        {
            List<GameStatusModel> animalNames = new List<GameStatusModel>();

            string query = "SELECT AnimalId, Name FROM [Animals]";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    animalNames.Add(new GameStatusModel
                    {
                        AnimalId = reader.GetInt32(0),   // Unique ID for each card
                        AnimalName = reader.GetString(1)
                    });
                }
            }

            Console.WriteLine($"Fetched {animalNames.Count} animal names from the database."); // Debugging line
            return animalNames;
        }

        public int GetPlayerCountOnGameBoard(int gameId)
        {
            int playerCount = 0;

            string query = "SELECT COUNT(*) FROM [User] WHERE GameId = @GameId AND LoggedIn = 1";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@GameId", gameId);

                try
                {
                    connection.Open();

                    var result = command.ExecuteScalar();
                    playerCount = result != null ? Convert.ToInt32(result) : 0;
                }
                catch (Exception ex)
                {
                    // Log the exception or handle it as necessary
                    Console.WriteLine($"Error counting players: {ex.Message}");
                }
            }

            return playerCount;
        }

        public List<GameStatusModel> InitializeOrGetGameBoard(int gameId)
        {
            List<GameSessionModel> gameBoard = new List<GameSessionModel>();

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                // Check if the game board already exists for this game ID
                string checkQuery = "SELECT COUNT(*) FROM [GameSession] WHERE GameId = @GameId";
                using (var checkCommand = new SqlCommand(checkQuery, connection))
                {
                    checkCommand.Parameters.AddWithValue("@GameId", gameId);
                    int count = Convert.ToInt32(checkCommand.ExecuteScalar());

                    if (count > 0)
                    {
                        // Fetch the existing game board
                        string fetchQuery = @"
                    SELECT gs.GameId, a.CreatedOn, gs.Player1Id, gs.Player2Id
                    FROM [GameSession] gs
                    INNER JOIN [Animals] a ON gs.AnimalId = a.AnimalId
                    WHERE gs.GameId = @GameId";

                        using (var fetchCommand = new SqlCommand(fetchQuery, connection))
                        {
                            fetchCommand.Parameters.AddWithValue("@GameId", gameId);
                            using (var reader = fetchCommand.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    gameBoard.Add(new GameSessionModel
                                    {
                                        GameId = gameId,
                                        CreatedOn = reader.GetDateTime(0),
                                        Player1Id = reader.GetInt32(1),
                                        Player2Id = reader.GetInt32(2)                                        
                                    });
                                }
                            }
                        }
                    }
                    else
                    {
                        // Initialize a new game board
                        var animals = GetAnimalNames();
                        Random rng = new Random();
                        animals = animals.OrderBy(a => rng.Next()).ToList(); // Shuffle animals

                        foreach (var animal in animals)
                        {
                            gameBoard.Add(new GameSessionModel
                            {
                                GameId = gameId,
                                AnimalId = animal.AnimalId,
                                AnimalName = animal.AnimalName,
                                Clicked = false,
                                Matched = false
                            });
                        }

                        // Save the game board to the database
                        string insertQuery = @"
                    INSERT INTO [GameStatus] (GameId, AnimalId, Clicked, Matched)
                    VALUES (@GameId, @AnimalId, 0, 0)";

                        using (var insertCommand = new SqlCommand(insertQuery, connection))
                        {
                            foreach (var card in gameBoard)
                            {
                                insertCommand.Parameters.Clear();
                                insertCommand.Parameters.AddWithValue("@GameId", gameId);
                                insertCommand.Parameters.AddWithValue("@AnimalId", card.AnimalId);
                                insertCommand.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }

            return gameBoard;
        }


        // Helper method to get the lowest UserId (replace with your logic to fetch the correct UserId)
        private int GetLowestUserId(SqlConnection connection)
        {
            string getLowestUserIdQuery = "SELECT MIN(Id) FROM [User]"; // Assuming User table has Id column
            using (var command = new SqlCommand(getLowestUserIdQuery, connection))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        // Fetch all available game sessions
        public List<GameSessionModel> GetAllGameSessions()
        {
            List<GameSessionModel> gameSessions = new List<GameSessionModel>();

            string query = "SELECT GameId, CreatedOn FROM [GameSession] WHERE Player1Id IS NULL OR Player2Id IS NULL";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                connection.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    gameSessions.Add(new GameSessionModel
                    {
                        GameId = reader.GetInt32(0),
                        CreatedOn = reader.GetDateTime(1)
                    });
                }
            }

            return gameSessions;
        }

        // Assign a user to a game session
        public bool JoinGameSession(int gameId, int userId, out string errorMsg)
        {
            errorMsg = string.Empty;

            string query = @"
            UPDATE [GameSession]
            SET Player1Id = CASE WHEN Player1Id IS NULL THEN @UserId ELSE Player1Id END,
                Player2Id = CASE WHEN Player1Id IS NOT NULL AND Player2Id IS NULL THEN @UserId ELSE Player2Id END
            WHERE GameId = @GameId";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@GameId", gameId);
                command.Parameters.AddWithValue("@UserId", userId);

                try
                {
                    connection.Open();
                    int rowsAffected = command.ExecuteNonQuery();

                    // Check if the update was successful
                    if (rowsAffected == 0)
                    {
                        errorMsg = "Game session is full or does not exist.";
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    errorMsg = ex.Message;
                    return false;
                }
            }

            return true;
        }
    }


    /**** 
     * Redo
    ****/

}
