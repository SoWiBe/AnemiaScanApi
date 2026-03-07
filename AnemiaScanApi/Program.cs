using System.Text.Json.Serialization;

using AnemiaScanApi.Extensions;
using AnemiaScanApi.Middleware;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

builder.AddLogging();

builder.Services.AddJwtAuthentication(configuration);
builder.Services.AddSenderOptions(configuration);

builder.Services
    .AddMongoDb(configuration)
    .AddServices()
    .AddAnemiaPredictionModel()
    .AddValidationFilters()
    .AddEndpointsApiExplorer()
    .AddSwagger();

builder.Services
    .AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); });

var app = builder.Build();

app.MapSwagger("/openapi/{documentName}.json");
app.MapScalarApiReference();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.UseMiddleware<SASMiddleware>();

app.MapControllers();

app.Run();