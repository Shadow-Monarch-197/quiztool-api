namespace quizTool.Services
{
    public class EmailOptions
    {
        // "smtp", "sendgrid" or "auto" (try smtp, then sendgrid)
        public string Provider { get; set; } = "auto";

        public bool Disabled { get; set; } = false;       // hard disable email sending
        public bool DevEchoOtp { get; set; } = true;      // include OTP in response when disabled or dev

        public SmtpSettings Smtp { get; set; } = new();
        public SendGridSettings SendGrid { get; set; } = new();

        public class SmtpSettings
        {
            public string Host { get; set; } = "smtp.gmail.com";
            public int Port { get; set; } = 587;           // 587 STARTTLS, 465 SSL
            public bool EnableSsl { get; set; } = true;
            public string Username { get; set; } = "";
            public string Password { get; set; } = "";
            public string FromAddress { get; set; } = "";
            public string FromName { get; set; } = "QuizTool";
            public int TimeoutMs { get; set; } = 10000;
        }

        public class SendGridSettings
        {
            public string ApiKey { get; set; } = "";
            public string FromAddress { get; set; } = "";
            public string FromName { get; set; } = "QuizTool";
        }
    }
}
