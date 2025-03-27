// Entry point for the ManagementSystem2 web application
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();  // Registers controllers
builder.Services.AddOpenApi();      // Enables OpenAPI support

// Build the application
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();  // Maps OpenAPI endpoint in development
}

app.UseHttpsRedirection();  // Redirects HTTP to HTTPS

app.MapControllers();  // Maps controller routes

var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };  // Weather summaries (unused in this context)

app.Run();