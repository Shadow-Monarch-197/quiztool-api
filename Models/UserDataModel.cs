using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace quizTool.Models
{
    public class UserDataModel
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int userid { get; set; }

        [Required]
        public string name { get; set; }

        [Required]
        public string email { get; set; }

        public string? mobileno { get; set; }

        [Required]
        public string password { get; set; }

        public string role { get; set; } = "basic";

        public DateTime? createddate { get; set; } = DateTime.UtcNow;
    }
}
