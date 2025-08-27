namespace quizTool.Models
{
    public class LoginResponseModel
    {
        public int userId { get; set; }
        public string name { get; set; }
        public string email { get; set; }
        public string role { get; set; }
        public string token { get; set; }
    }
}
