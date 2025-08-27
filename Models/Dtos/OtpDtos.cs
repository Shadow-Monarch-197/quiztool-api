namespace quizTool.Models
{
    public class LoginStep1Dto { public string email { get; set; } = ""; public string password { get; set; } = ""; }
    public class LoginStep1Response { public int challengeId { get; set; } public string message { get; set; } = ""; }

    public class LoginStep2Dto { public int challengeId { get; set; } public string email { get; set; } = ""; public string otp { get; set; } = ""; }

    public class ForgotStartDto { public string email { get; set; } = ""; }
    public class ResetPasswordDto { public string email { get; set; } = ""; public string otp { get; set; } = ""; public string newPassword { get; set; } = ""; }
}
