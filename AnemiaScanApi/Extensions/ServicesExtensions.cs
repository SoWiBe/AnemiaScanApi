using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

using AnemiaScanApi.Attributes;
using AnemiaScanApi.Filters;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Infrastructure.Services;
using AnemiaScanApi.Infrastructure.Services.Core;
using AnemiaScanApi.Infrastructure.Utils;
using AnemiaScanApi.Infrastructure.Utils.Core;
using AnemiaScanApi.Settings;
using AnemiaScanApi.Services;
using AnemiaScanApi.Utils;
using AnemiaScanApi.Utils.Core;

namespace AnemiaScanApi.Extensions;

/// <summary>
/// Extension methods for configuring services.
/// </summary>
public static class ServicesExtensions
{
    /// <summary>
    /// Включаем свагу
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            // API Information
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1.0.0",
                Title = "Anemia Scan API",
                Description = "ASP.NET Core Web API for Anemia Analysis using Machine Learning. " +
                                "This API provides endpoints for user authentication, ML-based anemia detection, " +
                                "and analysis management.",
                Contact = new OpenApiContact
                {
                    Name = "Anemia Scan Team",
                    Email = "support@anemiascan.com",
                    Url = new Uri("https://github.com/yourusername/anemiascan")
                },
                License = new OpenApiLicense
                {
                    Name = "MIT License",
                    Url = new Uri("https://opensource.org/licenses/MIT")
                }
            });

            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Group endpoints by tags
            options.TagActionsBy(api => [api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Default"]);
            options.DocInclusionPredicate((_, _) => true);
        });

        return services;
    }

    /// <summary>
    /// Фильтры
    /// </summary>
    public static IServiceCollection AddValidationFilters(this IServiceCollection services) 
    {
        services.AddScoped<ValidateImageAttribute>();
        services.AddScoped<UniqueEmailAttribute>();
        services.AddScoped<ValidationCodeFilter>();
        return services;
    }

    /// <summary>
    /// Добавляем монгу.
    ///
    /// Клиент, база и GridFS-бакет — синглтоны: MongoClient потокобезопасен и
    /// сам держит пул соединений, а создание нового клиента на каждый
    /// Scoped-репозиторий означало бы TLS-хендшейк на каждый HTTP-запрос и
    /// быстрый выход на лимит соединений тарифа Atlas
    /// (P0 №2 в docs/plans/MVP_PLAN.md).
    /// </summary>
    /// <param name="configuration"></param>
    public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return new MongoClient(settings.ConnectionString);
        });

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<MongoDbSettings>>().Value;
            return sp.GetRequiredService<IMongoClient>().GetDatabase(settings.DatabaseName);
        });

        services.AddSingleton<IGridFSBucket>(sp => new GridFSBucket(sp.GetRequiredService<IMongoDatabase>()));

        return services;
    }
    
    /// <summary>
    /// Настройки, которые должны меняться без передеплоя:
    /// <c>Legal</c> — версия политики и тексты (P0 №12 и №13; версия попадает
    /// в согласие пользователя, поэтому живёт в конфиге, а не в коде),
    /// <c>ImageStorage</c> — параметры сжатия снимка перед GridFS (P0 №9).
    /// </summary>
    public static IServiceCollection AddAppOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LegalSettings>(configuration.GetSection("Legal"));
        // Незаданная переменная окружения приезжает пустой строкой и перетирает
        // дефолт. Для версии политики это тихая порча согласий (P0 №13), поэтому
        // пустое значение возвращаем к дефолту.
        services.PostConfigure<LegalSettings>(legal =>
        {
            if (string.IsNullOrWhiteSpace(legal.PolicyVersion))
                legal.PolicyVersion = LegalSettings.DefaultPolicyVersion;
        });
        services.Configure<ImageStorageSettings>(configuration.GetSection("ImageStorage"));
        return services;
    }

    /// <summary>
    /// Регистрируем все внутренние сервисы и репозитории
    /// </summary>
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IAnemiaScansRepository, AnemiaScansRepository>();
        services.AddScoped<ICoursesRepository, CoursesRepository>();
        services.AddScoped<ICourseContentRepository, CourseContentRepository>();
        services.AddScoped<ICourseEnrollmentsRepository, CourseEnrollmentsRepository>();

        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IAnemiaAnalysisService, AnemiaAnalysisService>();
        services.AddScoped<IPredictionService, PredictionService>();
        services.AddScoped<IHemoglobinPredictionService, HemoglobinPredictionService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IStreakService, StreakService>();
        services.AddScoped<ICourseCatalogService, CourseCatalogService>();
        services.AddScoped<ICourseEnrollmentService, CourseEnrollmentService>();

        services.AddScoped<IEmailSender, EmailSender>();
        services.AddScoped<ICodeGenerator, CodeGenerator>();
        // Без состояния, тяжёлых зависимостей и на каждый скан один вызов — singleton.
        services.AddSingleton<IImageCompressor, ImageCompressor>();

        services.AddMemoryCache();

        return services;
    }

    /// <summary>
    /// SWAG UI
    /// </summary>
    /// <param name="app"></param>
    public static void UseCustomSwaggerUi(this WebApplication app)
    {
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Anemia Scan API v1.0.0");
            options.RoutePrefix = string.Empty;
        
            // Customize UI
            options.DocumentTitle = "Anemia Scan API Documentation";
            options.DefaultModelsExpandDepth(2);
            options.DefaultModelExpandDepth(2);
            options.DisplayRequestDuration();
            options.EnableDeepLinking();
            options.EnableFilter();
            options.ShowExtensions();
        });
    }
}
