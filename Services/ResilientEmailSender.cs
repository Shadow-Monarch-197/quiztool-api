using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using SendGrid;
using SendGrid.Helpers.Mail;
using System.Text.Json;

namespace quizTool.Services
{
    public class ResilientEmailSender : IEmailSender
    {
        private readonly EmailOptions _opt;
        private readonly ILogger<ResilientEmailSender> _log;

        public ResilientEmailSender(IOptions<EmailOptions> options, ILogger<ResilientEmailSender> log)
        {
            _opt = options.Value;
            _log = log;
        }

        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            if (_opt.Disabled)
            {
                _log.LogInformation("[Email Disabled] To={To}; Subject={Subject}", to, subject);
                return;
            }

            var prov = (_opt.Provider ?? "auto").ToLowerInvariant();

            if (prov == "smtp")
            {
                Console.WriteLine("Sent Via SMTP");
                await SendViaSmtp(to, subject, htmlBody, ct);
                return;
            }

            if (prov == "sendgrid")
            {
                Console.WriteLine("Sent Via SMTP");
                await SendViaSendGrid(to, subject, htmlBody, ct);
                return;
            }

            // auto: try SMTP, then SendGrid
            try
            {
                await SendViaSmtp(to, subject, htmlBody, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "SMTP failed, falling back to SendGrid");
                await SendViaSendGrid(to, subject, htmlBody, ct);
            }
        }

        private async Task SendViaSmtp(string to, string subject, string htmlBody, CancellationToken ct)
        {

            var s = _opt.Smtp;

            var msg = new MimeMessage();
            var fromAddr = string.IsNullOrWhiteSpace(s.FromAddress) ? s.Username : s.FromAddress;
            msg.From.Add(new MailboxAddress(s.FromName ?? "QuizTool", fromAddr));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject;
            msg.Body = new TextPart(TextFormat.Html) { Text = htmlBody };

            using var client = new MailKit.Net.Smtp.SmtpClient();
            client.Timeout = s.TimeoutMs <= 0 ? 10000 : s.TimeoutMs;

            SecureSocketOptions socketOpt;
            if (!s.EnableSsl) socketOpt = SecureSocketOptions.None;
            else socketOpt = s.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            await client.ConnectAsync(s.Host, s.Port, socketOpt, ct);
            if (!string.IsNullOrWhiteSpace(s.Username))
                await client.AuthenticateAsync(s.Username, s.Password, ct);

            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
        }

        private async Task SendViaSendGrid(string to, string subject, string htmlBody, CancellationToken ct)
        {

            Console.WriteLine("Execution is here");

            try
            {

                var g = _opt.SendGrid;
                if (string.IsNullOrWhiteSpace(g.ApiKey))
                    throw new InvalidOperationException("SendGrid ApiKey is not configured");

                var client = new SendGridClient(g.ApiKey);
                var from = new EmailAddress(
                    string.IsNullOrWhiteSpace(g.FromAddress) ? "no-reply@example.com" : g.FromAddress,
                    string.IsNullOrWhiteSpace(g.FromName) ? "QuizTool" : g.FromName);

                var msg = MailHelper.CreateSingleEmail(from, new EmailAddress(to), subject,
                                                       plainTextContent: null, htmlContent: htmlBody);

                var resp = await client.SendEmailAsync(msg, ct);
                
                Console.WriteLine(resp);
                Console.WriteLine(JsonSerializer.Serialize(resp));

                if ((int)resp.StatusCode >= 400)
                {
                    var body = await resp.Body.ReadAsStringAsync();
                    throw new Exception($"SendGrid error {resp.StatusCode}: {body}");
                }
            }
            catch (Exception ex) { 

                //Debug.WriteLine(ex);
                Console.WriteLine(ex);           
            }
        }
    }
}
