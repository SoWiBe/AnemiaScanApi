namespace AnemiaScanApi.Utils.Core;

public interface IEmailSender
{
    public Task SendEmailAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default);
}