using AnemiaScanApi.Filters;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // API Information
    options.SwaggerDoc("v1", new ()
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
    options.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] ?? "Default" });
    options.DocInclusionPredicate((name, api) => true);
});

builder.Services.AddScoped<ValidateImageAttribute>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
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

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();