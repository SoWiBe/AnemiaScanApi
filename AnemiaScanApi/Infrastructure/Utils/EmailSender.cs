using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;

using AnemiaScanApi.Exceptions;
using AnemiaScanApi.Utils.Core;

namespace AnemiaScanApi.Utils;

public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(string toAddress, string subject, string body, CancellationToken cancellationToken = default)
    {
        var fromAddress = configuration.GetSection("EmailSender:Email").Get<string>()!;
        var password = configuration.GetSection("EmailSender:Password").Get<string>()!;

        var smtpServer = configuration.GetSection("Smtp:Server").Get<string>()!;
        var smtpPort = configuration.GetSection("Smtp:Port").Get<int>();
        
        using var client = new SmtpClient();
        
        try
        {
            await client.ConnectAsync(smtpServer, smtpPort, smtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, cancellationToken);
            await client.AuthenticateAsync(fromAddress, password, cancellationToken);
            await client.SendAsync(new MimeMessage
            {
                From = { new MailboxAddress("From", fromAddress) },
                To = { new MailboxAddress("To", toAddress) },
                Subject = subject,
                Body = new TextPart("plain") { Text = body }
            }, cancellationToken);
        }
        catch (SmtpCommandException ex) when (ex.StatusCode == SmtpStatusCode.MailboxUnavailable)
        {
            logger.LogError(ex, "Mailbox unavailable for {ToAddress}", toAddress);
            throw new SASException($"Recipient mailbox unavailable: {toAddress}", (int)HttpStatusCode.BadRequest);
        }
        catch (AuthenticationException ex)
        {
            logger.LogError(ex, "SMTP authentication failed");
            throw new SASException("SMTP authentication failed", (int)HttpStatusCode.Unauthorized);
        }
        catch (SmtpCommandException ex)
        {
            logger.LogError(ex, "SMTP command error while sending email");
            throw new SASException($"SMTP error: {ex.Message}", (int)HttpStatusCode.BadGateway);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while sending email");
            throw new SASException("Email sending failed", (int)HttpStatusCode.InternalServerError);
        }
        finally
        {
            await client.DisconnectAsync(true, cancellationToken);
        }
    }
}