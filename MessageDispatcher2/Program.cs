using MessageDispatcher2.Services;

// Entry point for the MessageDispatcher2 gRPC application
var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddGrpc();  // Adds gRPC support
builder.Services.AddHttpClient();  // Adds HTTP client support
builder.Services.AddSingleton<HealthCheckService>();  // Registers HealthCheckService as singleton
builder.Services.AddHostedService(provider => provider.GetRequiredService<HealthCheckService>());  // Reuses HealthCheckService as hosted service
builder.Services.AddSingleton<MessageServiceImpl>();  // Registers MessageServiceImpl as singleton

// Build the application
var app = builder.Build();

// Configure endpoints
app.MapGrpcService<MessageServiceImpl>();  // Maps gRPC service
app.MapGet("/", () => "Use a gRPC client to connect.");  // Simple GET endpoint

// Start the application
app.Run();