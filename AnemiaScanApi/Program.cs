using AnemiaScanApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.AddLogging();

// Add MongoDB configuration
builder.Services.AddMongoDb(builder.Configuration);

// Add JWT authentication configuration
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Add service implementations
builder.Services.AddServices();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddValidationFilters();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwagger();

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