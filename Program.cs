using Serilog;
using WeatherDashboard.Services;
using Amazon;
using Amazon.Runtime;
using Amazon.DynamoDBv2;
using Amazon.SecretsManager;

// ============================================================================
// APPLICATION ENTRY POINT - Weather Dashboard
// ============================================================================
// This minimal hosting configuration sets up the ASP.NET Core web application
// with dependency injection, logging, and service registration.

var builder = WebApplication.CreateBuilder(args);

// Cloud Run is not EC2, so skip IMDS credential probing to avoid misleading errors.
if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("K_SERVICE")))
{
    Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "true");
}

var hasAwsAccessKey = !string.IsNullOrWhiteSpace(
    Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID")
    ?? builder.Configuration["AWS:AccessKeyId"]);
var hasAwsSecretKey = !string.IsNullOrWhiteSpace(
    Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY")
    ?? builder.Configuration["AWS:SecretAccessKey"]);

if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("K_SERVICE"))
    && (!hasAwsAccessKey || !hasAwsSecretKey))
{
    throw new InvalidOperationException(
        "AWS credentials are missing on Cloud Run. Set AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY for DynamoDB access.");
}

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
    var regionName = ResolveAwsRegion(builder.Configuration);
    var region = RegionEndpoint.GetBySystemName(regionName);
    var credentials = ResolveAwsCredentials(builder.Configuration);

    if (credentials != null)
    {
        return new AmazonDynamoDBClient(credentials, region);
    }

    return new AmazonDynamoDBClient(region);
});
builder.Services.AddSingleton<IAmazonSecretsManager>(_ =>
{
    var regionName = ResolveAwsRegion(builder.Configuration);
    var region = RegionEndpoint.GetBySystemName(regionName);
    var credentials = ResolveAwsCredentials(builder.Configuration);

    if (credentials != null)
    {
        return new AmazonSecretsManagerClient(credentials, region);
    }

    return new AmazonSecretsManagerClient(region);
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

static string ResolveAwsRegion(IConfiguration configuration)
{
    var region = configuration["AWS:Region"]
        ?? Environment.GetEnvironmentVariable("AWS_REGION")
        ?? "eu-north-1";

    return NormalizeAwsSetting(region);
}

static AWSCredentials? ResolveAwsCredentials(IConfiguration configuration)
{
    var accessKey = NormalizeAwsSetting(
        configuration["AWS:AccessKeyId"]
        ?? Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID"));
    var secretKey = NormalizeAwsSetting(
        configuration["AWS:SecretAccessKey"]
        ?? Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY"));

    if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
    {
        return null;
    }

    if (accessKey.Any(char.IsWhiteSpace) || secretKey.Any(char.IsWhiteSpace))
    {
        throw new InvalidOperationException(
            "AWS credentials contain whitespace characters. Verify Cloud Run variables AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY do not include spaces/newlines.");
    }

    var sessionToken = NormalizeAwsSetting(
        configuration["AWS:SessionToken"]
        ?? Environment.GetEnvironmentVariable("AWS_SESSION_TOKEN"));

    return string.IsNullOrWhiteSpace(sessionToken)
        ? new BasicAWSCredentials(accessKey, secretKey)
        : new SessionAWSCredentials(accessKey, secretKey, sessionToken);
}

static string? NormalizeAwsSetting(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return value;
    }

    var normalized = value.Trim();
    if (normalized.Length >= 2 && normalized.StartsWith('"') && normalized.EndsWith('"'))
    {
        normalized = normalized[1..^1].Trim();
    }

    return normalized;
}

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

// Cloud Run terminates TLS at the load balancer and forwards requests over HTTP.
// Keep local development redirect behavior, but avoid production redirect loops.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();  // Redirect HTTP to HTTPS locally
}
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

app.MapGet("/health", () => Results.Ok("OK"));

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
