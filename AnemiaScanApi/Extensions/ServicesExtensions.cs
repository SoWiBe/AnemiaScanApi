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
    /// <param name="services"></param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Swagger/OpenAPI support to the service collection.
        /// </summary>
        public void AddSwagger()
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
        }

        /// <summary>
        /// Adds validation filters
        /// </summary>
        public void AddValidationFilters() 
        {
            services.AddScoped<ValidateImageAttribute>();
            services.AddScoped<UniqueUsernameAttribute>();
        }

        /// <summary>
        /// Adds MongoDB configuration
        /// </summary>
        /// <param name="configuration"></param>
        public void AddMongoDb(IConfiguration configuration)
        {
            services.Configure<MongoDbSettings>(configuration.GetSection("MongoDB"));
        }
        
        /// <summary>
        /// Adds service implementations
        /// </summary>
        public void AddServices()
        {
            services.AddScoped<IAuthorizationService, AuthorizationService>();
            services.AddScoped<IUsersRepository, UsersRepository>();
            services.AddScoped<IAnemiaScansRepository, AnemiaScansRepository>();
            services.AddScoped<IAnemiaAnalysisService, AnemiaAnalysisService>();
        }
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