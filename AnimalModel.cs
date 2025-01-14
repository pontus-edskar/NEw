using System.ComponentModel.DataAnnotations;

namespace Project.Models
{
    public class AnimalModel
    {
        [Key]
        public int AnimalId { get; set; }

        [Required]
        public string Name { get; set; }
    }
}
