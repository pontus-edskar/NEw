using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Project.Models
{
    public class GameStatusModel
    {
        public int AnimalId { get; set; } // Animal being matched

        public string AnimalName { get; set; } // Display name of animal

        public bool Clicked { get; set; }

        public bool Matched { get; set; }

        public bool Turn { get; set; }       

        public int? UserId { get; set; } // The user who clicked the card, nullable since it could be unclicked initially

        public int GameId { get; set; }
        public bool GameStatusError()
        {
            if(AnimalId == 0 || string.IsNullOrEmpty(AnimalName) || UserId == null)
            {
                return true;
            }
            return false;
        }
    }
}