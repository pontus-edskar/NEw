namespace Project.Models
{
    public class GameSessionModel
    {
        public int GameId { get; set; }
        public DateTime CreatedOn { get; set; }
        public int? Player1Id { get; set; }
        public int? Player2Id { get; set; }

        public GameStatusModel Game = new GameStatusModel();
    }
}
