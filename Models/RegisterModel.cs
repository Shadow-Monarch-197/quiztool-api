
using System.ComponentModel.DataAnnotations;

namespace quizTool.Models
{
    public class RegisterModel
    {
        public string name { get; set; }

        public string email { get; set; }

        public string? mobileno { get; set; } 
        public string password { get; set; }
    }
}

