using System.Text.Json;
using Serilog;
using WeatherDashboard.Models;

namespace WeatherDashboard.Services;

// ============================================================================
// WEATHER SERVICE
// ============================================================================
// Main application service coordinating weather data retrieval.
// Implements the cache-aside pattern: check cache first, fall back to API.
// 
// Data Flow:
// 1. GetWeatherByCityAsync called with city name
// 2. Build cache key: "weather:london:gb"
// 3. Try to get from cache
// 4. If miss: Call OpenWeatherMap API
// 5. If success: Store in cache for 30 minutes
// 6. Return WeatherData with IsFromCache flag

/// <summary>
/// Main weather service that coordinates data retrieval with caching
/// </summary>
public class WeatherService : IWeatherService
{
    private readonly IWeatherApiClient _apiClient;      // API client for OpenWeatherMap
    private readonly ICacheService _cacheService;        // Cache service for storing results
    private readonly ILogger<WeatherService> _logger;    // Logger for debugging
    private readonly IConfiguration _configuration;      // Configuration (cache duration, etc)

    /// <summary>
    /// Constructor: Dependencies injected by ASP.NET Core
    /// </summary>
    public WeatherService(
        IWeatherApiClient apiClient,
        ICacheService cacheService,
        ILogger<WeatherService> logger,
        IConfiguration configuration)
    {
        _apiClient = apiClient;
        _cacheService = cacheService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Get weather for a city (optionally in specific country)
    /// Implements cache-aside pattern
    /// </summary>
    public async Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null)
    {
        // ─────────────────────────────────────────────────────────────────────
        // INPUT VALIDATION
        // ─────────────────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(city))
        {
            _logger.LogWarning("GetWeatherByCityAsync called with empty city");
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // BUILD CACHE KEY
        // ─────────────────────────────────────────────────────────────────────
        // Cache key format: "weather:city:country"
        // Example: "weather:london:gb" or "weather:london:any"
        // Lowercase to ensure case-insensitive matching
        var cacheKey = $"weather:{city.ToLower()}:{country?.ToLower() ?? "any"}";
        
        // ─────────────────────────────────────────────────────────────────────
        // CHECK CACHE
        // ─────────────────────────────────────────────────────────────────────
        // Try to get from cache first (fast path)
        var cachedData = await _cacheService.GetAsync<WeatherData>(cacheKey);
        if (cachedData != null)
        {
            // Mark as cached for UI indication
            cachedData.IsFromCache = true;
            _logger.LogInformation("Weather data retrieved from cache for {City}", city);
            return cachedData;  // Return immediately
        }

        // ─────────────────────────────────────────────────────────────────────
        // API CALL (CACHE MISS)
        // ─────────────────────────────────────────────────────────────────────
        // Cache miss - call external API
        var weatherData = await _apiClient.GetWeatherByCityAsync(city, country);
        
        // ─────────────────────────────────────────────────────────────────────
        // CACHE THE RESULT
        // ─────────────────────────────────────────────────────────────────────
        if (weatherData != null)
        {
            // Get cache duration from configuration (default: 30 minutes)
            var cacheDuration = TimeSpan.FromMinutes(
                _configuration.GetValue<int>("Caching:DurationMinutes", 30));
            
            // Store in cache with expiration
            await _cacheService.SetAsync(cacheKey, weatherData, cacheDuration);
            _logger.LogInformation("Weather data retrieved from API and cached for {City}", city);
        }
        else
        {
            // API call failed
            _logger.LogWarning("Failed to retrieve weather data for {City}", city);
        }

        return weatherData;
    }

    /// <summary>
    /// Get weather by geographic coordinates (latitude, longitude)
    /// Similar pattern to GetWeatherByCityAsync but uses coordinates instead
    /// </summary>
    public async Task<List<WeatherData>?> GetWeatherByCoordinatesAsync(double latitude, double longitude)
    {
        // ─────────────────────────────────────────────────────────────────────
        // BUILD CACHE KEY FOR COORDINATES
        // ─────────────────────────────────────────────────────────────────────
        // Cache key format: "weather:geo:lat:lon"
        // Example: "weather:geo:51.5074:0.1278" (London)
        // Formatted to 4 decimal places for precision while minimizing key variance
        var cacheKey = $"weather:geo:{latitude:F4}:{longitude:F4}";
        
        // Try to get from cache first
        var cachedData = await _cacheService.GetAsync<List<WeatherData>>(cacheKey);
        if (cachedData != null)
        {
            _logger.LogInformation("Weather data retrieved from cache for coordinates");
            return cachedData;
        }

        // Call API if not in cache
        var weatherData = await _apiClient.GetWeatherByCoordinatesAsync(latitude, longitude);
        
        if (weatherData != null)
        {
            var cacheDuration = TimeSpan.FromMinutes(
                _configuration.GetValue<int>("Caching:DurationMinutes", 30));
            
            // Store as a list for consistency
            await _cacheService.SetAsync(cacheKey, new List<WeatherData> { weatherData }, cacheDuration);
        }

        return weatherData != null ? new List<WeatherData> { weatherData } : null;
    }
}

// ============================================================================
// OPENWEATHERMAP API CLIENT
// ============================================================================
// Concrete implementation of IWeatherApiClient.
// Makes HTTP requests to OpenWeatherMap API and parses JSON responses.
// 
// API Details:
// - Base URL: https://api.openweathermap.org/data/2.5
// - Endpoint: /weather (current weather)
// - Authentication: API key (from AWS Secrets Manager or config)
// - Units: Metric (temperature in Celsius, pressure in hPa)
// - Free tier: 60 calls/minute, 1M calls/month

public class OpenWeatherMapClient : IWeatherApiClient
{
    private readonly HttpClient _httpClient;                      // For making HTTP requests
    private readonly ILogger<OpenWeatherMapClient> _logger;        // For logging
    private readonly IConfiguration _configuration;                // For reading base URL
    private readonly string _baseUrl;                              // API base URL
    private readonly string _apiKey;                               // API key for authentication
    /// <summary>
    /// Constructor: Set up API client with configuration
    /// API key is fetched from:
    /// 1. AWS Secrets Manager (production)
    /// 2. appsettings.json/appsettings.Development.json (development)
    /// </summary>
    public OpenWeatherMapClient(
        HttpClient httpClient,
        ILogger<OpenWeatherMapClient> logger,
        IConfiguration configuration,
        ISecretsManagerService secretsManager)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        
        // Read base URL from configuration (defaults to OpenWeatherMap)
        _baseUrl = _configuration["WeatherApi:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5";
        
        // ─────────────────────────────────────────────────────────────────────
        // GET API KEY FROM SECRETS MANAGER OR CONFIG
        // ─────────────────────────────────────────────────────────────────────
        // Priority:
        // 1. AWS Secrets Manager (production) - most secure
        // 2. Configuration file (development) - convenience
        // In production, API key is never stored in config files
        _apiKey = secretsManager.GetSecretAsync("weather-dashboard/openweather-api-key").Result 
            ?? _configuration["WeatherApi:ApiKey"] ?? string.Empty;
    }

    /// <summary>
    /// Call OpenWeatherMap API by city name
    /// Constructs URL with parameters and makes HTTP request
    /// </summary>
    public async Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null)
    {
        try
        {
            // ─────────────────────────────────────────────────────────────────────
            // BUILD API REQUEST URL
            // ─────────────────────────────────────────────────────────────────────
            // Query format: "city,country" or just "city"
            // Example: "London,GB" or "London"
            // Uri.EscapeDataString handles special characters safely
            var query = country != null ? $"{city},{country}" : city;
            var url = $"{_baseUrl}/weather?q={Uri.EscapeDataString(query)}&appid={_apiKey}&units=metric";

            // ─────────────────────────────────────────────────────────────────────
            // MAKE HTTP REQUEST
            // ─────────────────────────────────────────────────────────────────────
            // GET request to OpenWeatherMap
            var response = await _httpClient.GetAsync(url);
            // Throw if status code indicates error (4xx, 5xx)
            response.EnsureSuccessStatusCode();

            // ─────────────────────────────────────────────────────────────────────
            // DESERIALIZE JSON RESPONSE
            // ─────────────────────────────────────────────────────────────────────
            // Parse JSON to OpenWeatherMapResponse object
            var content = await response.Content.ReadAsStringAsync();
            var openWeatherResponse = JsonSerializer.Deserialize<OpenWeatherMapResponse>(content);

            // Map API response to our internal WeatherData model
            return MapToWeatherData(openWeatherResponse);
        }
        catch (HttpRequestException ex)
        {
            // HTTP error (network issue, invalid URL, etc.)
            _logger.LogError(ex, "HTTP error retrieving weather for {City}", city);
            return null;
        }
        catch (Exception ex)
        {
            // Other errors (JSON parsing, etc.)
            _logger.LogError(ex, "Error retrieving weather for {City}", city);
            return null;
        }
    }

    /// <summary>
    /// Call OpenWeatherMap API by geographic coordinates
    /// Similar to GetWeatherByCityAsync but uses lat/lon instead of city name
    /// </summary>
    public async Task<WeatherData?> GetWeatherByCoordinatesAsync(double latitude, double longitude)
    {
        try
        {
            // Build URL with latitude/longitude parameters
            var url = $"{_baseUrl}/weather?lat={latitude}&lon={longitude}&appid={_apiKey}&units=metric";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var openWeatherResponse = JsonSerializer.Deserialize<OpenWeatherMapResponse>(content);

            return MapToWeatherData(openWeatherResponse);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error retrieving weather for coordinates {Latitude},{Longitude}", latitude, longitude);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving weather for coordinates");
            return null;
        }
    }

    /// <summary>
    /// Convert OpenWeatherMap API response to internal WeatherData model
    /// Handles null values and missing fields gracefully
    /// </summary>
    private static WeatherData? MapToWeatherData(OpenWeatherMapResponse? response)
    {
        // Validate response has required data
        if (response?.MainData == null || response.Weather?.Count == 0)
            return null;

        // Get first weather entry (array can have multiple, we use first)
        var weather = response.Weather![0];
        
        // ─────────────────────────────────────────────────────────────────────
        // MAP FIELDS FROM API RESPONSE TO WEATHERDATA
        // ─────────────────────────────────────────────────────────────────────
        // Convert OpenWeatherMap response to internal model with sensible defaults
        return new WeatherData
        {
            City = response.Name ?? string.Empty,                                    // City name from API
            Country = string.Empty,                                                   // API doesn't provide country
            Latitude = response.Coord?.Lat ?? 0,                                     // Latitude, default 0
            Longitude = response.Coord?.Lon ?? 0,                                    // Longitude, default 0
            Temperature = response.MainData.Temp,                                    // Current temperature
            FeelsLike = response.MainData.FeelsLike,                                // "Feels like" temperature
            Humidity = response.MainData.Humidity,                                  // Humidity percentage
            Pressure = response.MainData.Pressure,                                  // Atmospheric pressure
            Description = weather.Description,                                       // Weather description
            IconUrl = $"https://openweathermap.org/img/wn/{weather.Icon}@2x.png",  // Icon image URL (2x for quality)
            WindSpeed = response.Wind?.Speed ?? 0,                                  // Wind speed, default 0
            RetrievedAt = DateTime.UtcNow                                           // Timestamp (UTC)
        };
    }
}

/// <summary>
/// Interface for AWS Secrets Manager service
/// Abstracts secret retrieval for decoupling secrets implementation
/// </summary>
public interface ISecretsManagerService
{
    /// <summary>
    /// Retrieve a secret value by name from AWS Secrets Manager
    /// Returns: Secret value as string, or null if not found/error
    /// </summary>
    Task<string?> GetSecretAsync(string secretName);
}

/// <summary>
/// Dummy implementation of Secrets Manager
/// TODO: Complete with actual AWS SDK implementation
/// 
/// When implemented, this should:
/// 1. Create AmazonSecretsManagerClient
/// 2. Call GetSecretValueAsync with secret name
/// 3. Return the SecretString or null on error
/// 
/// See AWS documentation:
/// https://docs.aws.amazon.com/secretsmanager/latest/userguide/manage_retrieve-secret.html
/// </summary>
public class SecretsManagerService : ISecretsManagerService
{
    /// <summary>
    /// TODO: Implement AWS Secrets Manager integration
    /// Currently returns null (falls back to configuration file)
    /// </summary>
    public async Task<string?> GetSecretAsync(string secretName)
    {
        // TODO: Implement AWS Secrets Manager integration
        // Example implementation:
        // var client = new AmazonSecretsManagerClient();
        // var response = await client.GetSecretValueAsync(new GetSecretValueRequest { SecretId = secretName });
        // return response.SecretString;
        
        await Task.Delay(0);  // Satisfy async requirement
        return null;          // Currently returns null, falls back to config
    }
}
