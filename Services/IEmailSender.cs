//using System.Threading.Tasks;

//namespace quizTool.Services
//{
//    public interface IEmailSender
//    {
//        Task SendAsync(string to, string subject, string htmlBody);
//    }
//}


using System.Threading;

namespace quizTool.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    }
}
