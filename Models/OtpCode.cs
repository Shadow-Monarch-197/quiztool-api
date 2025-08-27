using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace quizTool.Models
{
    public enum OtpPurpose { Login = 0, ResetPassword = 1 }

    public class OtpCode
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string Email { get; set; } = "";

        [Required]
        public string CodeHash { get; set; } = "";

        [Required]
        public OtpPurpose Purpose { get; set; }

        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ConsumedAt { get; set; }
        public int Attempts { get; set; } = 0;
    }
}
