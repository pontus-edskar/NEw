using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;
using Project.Models;
using Microsoft.AspNetCore.SignalR;

namespace Project.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ProjectMethods _projectMethods;
        private readonly IHubContext<ReadyStatusHub> _hubContext; // Injecting the HubContext      

        private string connectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=Project;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False";
        private const string GameBoardSessionKey = "GameBoard";

        // Inject UserMethods via dependency injection
        public HomeController(ILogger<HomeController> logger, ProjectMethods projectMethods, IHubContext<ReadyStatusHub> hubContext)
        {
            _logger = logger;
            _projectMethods = projectMethods;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        /*****
         * Logga In View
        *****/

        public IActionResult LogIn()
        {
            return View();
        }

        [HttpPost]
        public IActionResult LogIn(string Name, string Password)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string query = "SELECT Id, Name, Password FROM [dbo].[User] WHERE Name = @Name AND Password = @Password";

                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Name", Name);
                    command.Parameters.AddWithValue("@Password", Password);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int userId = reader.GetInt32(reader.GetOrdinal("Id"));

                            // Close the reader before running another command
                            reader.Close();

                            // Set the LoggedIn flag to true and Ready to false
                            var updateQuery = "UPDATE [dbo].[User] SET LoggedIn = 1, Ready = 0 WHERE Id = @Id";
                            using (var updateCommand = new SqlCommand(updateQuery, connection))
                            {
                                updateCommand.Parameters.AddWithValue("@Id", userId);
                                updateCommand.ExecuteNonQuery();
                            }

                            // Save the logged-in user's info in the session
                            HttpContext.Session.SetString("UserName", Name);
                            HttpContext.Session.SetInt32("UserId", userId); // Storing UserId

                            return RedirectToAction("Ready");
                        }
                    }
                }
            }

            // Invalid login
            ViewBag.Message = "Invalid username or password.";
            return View();
        }
        /*****
         * Ready View
        *****/
        public IActionResult Ready()
        {
            var gameSessions = _projectMethods.GetAllGameSessions();
            return View("Ready", gameSessions); // Pass the list of game sessions to the view
        }

        [HttpPost]
        public IActionResult JoinGame(int gameId)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
            {
                return RedirectToAction("LogIn");
            }

            // Try to join the game session
            string errorMsg;
            bool success = _projectMethods.JoinGameSession(gameId, userId.Value, out errorMsg);

            if (success)
            {
                // Redirect to the game board after joining
                return RedirectToAction("GameBoard", new { gameId });
            }
            else
            {
                // Show an error if joining failed
                ViewBag.ErrorMessage = errorMsg;
                var gameSessions = _projectMethods.GetAllGameSessions();
                return View("Ready", gameSessions);
            }
        }

        public IActionResult GameBoard(int gameId)
        {
            var gameBoard = _projectMethods.InitializeOrGetGameBoard(gameId);

            // Save the game ID in the session for the user
            HttpContext.Session.SetInt32("GameId", gameId);

            return View("GameBoard", gameBoard);
        }



        public IActionResult MemoryGame()
        {
            var animals = _projectMethods.GetAnimalNames(); // Fetch unique cards
            if (animals == null || animals.Count == 0)
            {
                return View(new List<GameStatusModel>());
            }

            int? userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue)
            {
                return RedirectToAction("LogIn");
            }

            // Shuffle the cards
            Random rng = new Random();
            animals = animals.OrderBy(a => rng.Next()).ToList();

            // Initialize game state (cards) - all cards are initially unclicked and unmatched
            var gameBoard = animals.Select(a => new GameStatusModel
            {
                AnimalId = a.AnimalId,      // Unique ID
                AnimalName = a.AnimalName,  // Animal name
                Clicked = false,            // Initially hidden
                Matched = false,            // Initially unmatched
                UserId = userId
            }).ToList();

            // Fetch the player count for the current game
            var gameId = 1; // Replace with your logic to fetch the current game ID
            var playerCount = _projectMethods.GetPlayerCountOnGameBoard(gameId);

            // Save the initial game state in Session
            HttpContext.Session.SetString(GameBoardSessionKey, JsonConvert.SerializeObject(gameBoard));


            ViewBag.PlayerCount = playerCount;

            return View("GameBoard", gameBoard);
        }

        [HttpPost]
        public IActionResult FlipCard(int id)
        {
            // Retrieve the game board from session
            var gameBoardJson = HttpContext.Session.GetString(GameBoardSessionKey);
            if (gameBoardJson == null)
            {                
                return BadRequest("Game board not found.");
            }

            var gameBoard = JsonConvert.DeserializeObject<List<GameStatusModel>>(gameBoardJson);

            // Find the selected card
            var selectedCard = gameBoard.FirstOrDefault(c => c.AnimalId == id);
            if (selectedCard == null || selectedCard.Matched || selectedCard.Clicked)
            {
                return BadRequest("Invalid card selection.");
            }

            // Flip the selected card
            selectedCard.Clicked = true;

            // Save the updated game board back to session
            HttpContext.Session.SetString(GameBoardSessionKey, JsonConvert.SerializeObject(gameBoard));

            // Return the updated game board as a partial view
            return PartialView("PartialGameBoard", gameBoard);
        }

        [HttpPost]
        public IActionResult CompareCards(int firstCardId, int secondCardId)
        {
            var gameBoardJson = HttpContext.Session.GetString(GameBoardSessionKey);
            if (gameBoardJson == null)
            {
                return BadRequest("Game board not found.");
            }

            var gameBoard = JsonConvert.DeserializeObject<List<GameStatusModel>>(gameBoardJson);

            var firstCard = gameBoard.FirstOrDefault(c => c.AnimalId == firstCardId);
            var secondCard = gameBoard.FirstOrDefault(c => c.AnimalId == secondCardId);

            if (firstCard == null || secondCard == null)
            {
                return BadRequest("Invalid card selection.");
            }

            // Check if the two flipped cards match by comparing their names
            if (firstCard.AnimalName == secondCard.AnimalName)
            {
                firstCard.Matched = true; // Mark as matched
                secondCard.Matched = true;
            }
            else
            {
                firstCard.Clicked = false; // Flip back
                secondCard.Clicked = false;
            }

            // Save the updated game board back to session
            HttpContext.Session.SetString(GameBoardSessionKey, JsonConvert.SerializeObject(gameBoard));

            // Return the updated game board as a partial view
            return PartialView("PartialGameBoard", gameBoard);
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}