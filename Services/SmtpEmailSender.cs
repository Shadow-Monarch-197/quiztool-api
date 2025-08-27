//using Microsoft.Extensions.Options;
//using System.Net;
//using System.Net.Mail;
//using System.Net.Security;
//using System.Security.Cryptography.X509Certificates;

//namespace quizTool.Services
//{
//    public class SmtpEmailSender : IEmailSender
//    {
//        private readonly SmtpOptions _opt;

//        public SmtpEmailSender(IOptions<SmtpOptions> options)
//        {
//            _opt = options.Value;
//        }

//        public async Task SendAsync(string to, string subject, string htmlBody)
//        {
//            if (_opt.Disabled)
//            {
//                Console.WriteLine($"[EMAIL DISABLED] To: {to}\nSubject: {subject}\n{htmlBody}\n");
//                return;
//            }

//            using var msg = new MailMessage
//            {
//                From = new MailAddress(
//                    string.IsNullOrWhiteSpace(_opt.FromAddress) ? _opt.Username : _opt.FromAddress,
//                    string.IsNullOrWhiteSpace(_opt.FromName) ? "QuizTool" : _opt.FromName
//                ),
//                Subject = subject,
//                Body = htmlBody,
//                IsBodyHtml = true
//            };
//            msg.To.Add(to);

//            using var client = new SmtpClient(_opt.Host, _opt.Port)
//            {
//                EnableSsl = _opt.EnableSsl,
//                DeliveryMethod = SmtpDeliveryMethod.Network,
//                UseDefaultCredentials = false,
//                Credentials = new NetworkCredential(_opt.Username, _opt.Password),
//                Timeout = 15000
//            };

//            ServicePointManager.SecurityProtocol =
//                SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

//            await client.SendMailAsync(msg);
//        }
//    }
//}
