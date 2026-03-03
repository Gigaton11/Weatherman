using Serilog;
using WeatherDashboard.Services;
using Amazon;
using Amazon.DynamoDBv2;

// ============================================================================
// APPLICATION ENTRY POINT - Weather Dashboard
// ============================================================================
// This minimal hosting configuration sets up the ASP.NET Core web application
// with dependency injection, logging, and service registration.

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────────────
// LOGGING CONFIGURATION
// ─────────────────────────────────────────────────────────────────────────
// Serilog is configured to output structured logs to both console and file.
// Logs are rolled daily to separate log files.
// Minimum log level is Information (includes Information, Warning, Error, Fatal)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()  // Real-time console output
    .WriteTo.File("logs/weather-dashboard-.txt", rollingInterval: RollingInterval.Day)  // Daily rolling log files
    .CreateLogger();

builder.Host.UseSerilog();

// ─────────────────────────────────────────────────────────────────────────
// CORE FRAMEWORK SERVICES
// ─────────────────────────────────────────────────────────────────────────
// Register ASP.NET Core framework services needed for MVC/Razor Views
builder.Services.AddControllersWithViews();  // MVC controllers and view support
builder.Services.AddHttpClient();            // HTTP client factory for API calls
builder.Services.AddMemoryCache();           // In-memory caching (local development)
builder.Services.AddSingleton<IAmazonDynamoDB>(_ =>
{
    var regionName = builder.Configuration["AWS:Region"] ?? "eu-north-1";
    var region = RegionEndpoint.GetBySystemName(regionName);
    return new AmazonDynamoDBClient(region);
});

// ─────────────────────────────────────────────────────────────────────────
// APPLICATION SERVICES DEPENDENCY INJECTION
// ─────────────────────────────────────────────────────────────────────────
// These services are registered with scoped lifetime:
// - New instance created per HTTP request
// - Shared within the same request
// - Disposed when request completes

builder.Services.AddScoped<IWeatherService, WeatherService>();                          // Main weather service (caching + API)
builder.Services.AddScoped<IWeatherApiClient, OpenWeatherMapClient>();                   // OpenWeatherMap API client
builder.Services.AddScoped<ICacheService, AmazonElastiCacheService>();                   // Cache service (in-memory now, Redis in production)
builder.Services.AddScoped<IUserPreferencesService, DynamoDbUserPreferencesService>();   // DynamoDB user preferences
builder.Services.AddScoped<ISecretsManagerService, SecretsManagerService>();             // AWS Secrets Manager integration

// Build the application after all services are registered
var app = builder.Build();

// ─────────────────────────────────────────────────────────────────────────
// HTTP REQUEST PIPELINE MIDDLEWARE
// ─────────────────────────────────────────────────────────────────────────
// Middleware is executed in the order it's defined below

if (!app.Environment.IsDevelopment())
{
    // Production-only middleware
    app.UseExceptionHandler("/Home/Error");  // Handle unhandled exceptions
    app.UseHsts();                            // HTTP Strict Transport Security header
}

app.UseHttpsRedirection();  // Redirect HTTP to HTTPS
app.UseStaticFiles();       // Serve static files (CSS, JS, images)

app.UseRouting();           // Enable routing

// ─────────────────────────────────────────────────────────────────────────
// ROUTE MAPPING
// ─────────────────────────────────────────────────────────────────────────
// Default route: /Home/Index
// Pattern: {controller=Home}/{action=Index}/{id?}
// Example: /WeatherSearch/Index/42
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ─────────────────────────────────────────────────────────────────────────
// APPLICATION STARTUP WITH ERROR HANDLING
// ─────────────────────────────────────────────────────────────────────────
try
{
    Log.Information("Starting Weather Dashboard application");
    app.Run();  // Start the web server and listen for requests
}
catch (Exception ex)
{
    // Log fatal errors that prevent application startup
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    // Ensure all buffered log events are written before shutdown
    Log.CloseAndFlush();
}
