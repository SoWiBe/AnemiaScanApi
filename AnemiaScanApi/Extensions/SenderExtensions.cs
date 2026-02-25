using AnemiaScanApi.Infrastructure.Settings;
using AnemiaScanApi.Settings;

namespace AnemiaScanApi.Extensions;

public static class SenderExtensions
{
    public static void AddSenderOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailSenderSettings>(configuration.GetSection("EmailSender"));
        services.Configure<SmtpSettings>(configuration.GetSection("Smtp"));
        services.Configure<CodeGeneratorSettings>(configuration.GetSection("CodeGenerator"));
    }
}