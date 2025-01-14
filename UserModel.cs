namespace Project.Models
{
    public class UserModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Points { get; set; }
        public string Password { get; set; }
        public bool Ready { get; set; }       
        public string GameSessionId { get; set; }
    }
}