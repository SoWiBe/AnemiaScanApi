using AnemiaScanApi.Extensions;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Add logging
builder.AddLogging();

builder.Services.AddJwtAuthentication(configuration);
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services
    .AddMongoDb(configuration)
    .AddServices()
    .AddAnemiaPredictionModel()
    .AddValidationFilters()
    .AddEndpointsApiExplorer()
    .AddSwagger();

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseCustomSwaggerUi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.UseAuthentication();
app.MapControllers();

app.Run();