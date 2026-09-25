using System.Text.Json.Serialization;

using AnemiaScanApi.Extensions;
using AnemiaScanApi.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.AddLogging();

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:8081")
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.AddJwtAuthentication(configuration);
builder.Services.AddSenderOptions(configuration);

builder.Services
    .AddMongoDb(configuration)
    .AddServices()
    .AddAppOptions(configuration)
    .AddSasRateLimiter(configuration)
    .AddSasHealthChecks()
    .AddAnemiaPredictionModel()
    .AddHemoglobinPredictionModel()
    .AddValidationFilters()
    .AddEndpointsApiExplorer()
    .AddSwagger();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });

var app = builder.Build();

// Схема API и Scalar UI наружу в проде не выставляются: это карта всех
// эндпоинтов без единой проверки прав (P0 №7 в docs/plans/MVP_PLAN.md).
// Понадобилось открыть на стенде — ApiDocs__Enabled=true в окружении.
if (app.Environment.IsDevelopment() || configuration.GetValue<bool>("ApiDocs:Enabled"))
{
    app.MapSwagger("/openapi/{documentName}.json");
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("DevCorsPolicy");
app.UseAuthentication();
app.UseAuthorization();
// После аутентификации: политика анализа партиционирует лимит по пользователю
// из JWT, до UseAuthentication() claims ещё пустые и все бы делили лимит по IP.
app.UseRateLimiter();

app.UseMiddleware<SASMiddleware>();

app.MapSasHealthChecks();
app.MapControllers();

app.Run();
