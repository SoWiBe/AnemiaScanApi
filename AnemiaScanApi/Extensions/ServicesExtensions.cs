using AnemiaScanApi.Attributes;
using AnemiaScanApi.Filters;
using AnemiaScanApi.Infrastructure.Repositories;
using AnemiaScanApi.Services;
using AnemiaScanApi.Services.Core;
using AnemiaScanApi.Settings;
using Microsoft.OpenApi;

namespace AnemiaScanApi.Extensions;

/// <summary>
/// Extension methods for configuring services.
/// </summary>
public static class ServicesExtensions
{
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
    /// Adds validation filters
    /// </summary>
    public static IServiceCollection AddValidationFilters(this IServiceCollection services) 
    {
        services.AddScoped<ValidateImageAttribute>();
        services.AddScoped<UniqueUsernameAttribute>();
        return services;
    }

    /// <summary>
    /// Adds MongoDB configuration
    /// </summary>
    /// <param name="configuration"></param>
    public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
        return services;
    }
    
    /// <summary>
    /// Adds service implementations
    /// </summary>
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IAnemiaScansRepository, AnemiaScansRepository>();
        services.AddScoped<IAnemiaAnalysisService, AnemiaAnalysisService>();
        return services;
    }

    /// <summary>
    /// Configures and uses custom Swagger UI
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