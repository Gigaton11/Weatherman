using WeatherDashboard.Models;

namespace WeatherDashboard.Services;

// ============================================================================
// SERVICE INTERFACES
// ============================================================================
// These interfaces define contracts for different services.
// Implementations can be swapped (e.g., in-memory cache vs Redis).
// Services are injected into controllers via dependency injection.

/// <summary>
/// IWeatherService: Main weather service orchestrating data retrieval
/// 
/// Responsibilities:
/// - Coordinates between cache and API client
/// - Checks cache first, falls back to API
/// - Returns weather data with cache status indicator
/// 
/// Dependencies: IWeatherApiClient, ICacheService, IConfiguration
/// Lifetime: Scoped (one per HTTP request)
/// </summary>
public interface IWeatherService
{
    /// <summary>
    /// Get weather for a city (optionally in a specific country)
    /// Example: GetWeatherByCityAsync("London", "GB")
    /// Returns: WeatherData or null if city not found
    /// </summary>
    Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null);
    
    /// <summary>
    /// Get weather by geographic coordinates (latitude, longitude)
    /// Useful for reverse geocoding
    /// Returns: List of WeatherData or null if coordinates invalid
    /// </summary>
    Task<List<WeatherData>?> GetWeatherByCoordinatesAsync(double latitude, double longitude);
}

/// <summary>
/// IWeatherApiClient: External weather API client (abstracted)
/// 
/// Responsibilities:
/// - Makes HTTP requests to OpenWeatherMap API
/// - Deserializes JSON responses
/// - Maps API response to internal WeatherData model
/// - Handles API errors
/// 
/// Current Implementation: OpenWeatherMapClient
/// API Key: Retrieved from AWS Secrets Manager (or config)
/// Rate Limit: Free tier = 60 calls/minute
/// Lifetime: Scoped (one per HTTP request)
/// </summary>
public interface IWeatherApiClient
{
    /// <summary>
    /// Call OpenWeatherMap API by city name
    /// Makes HTTP GET to: /weather?q={city}&appid={apiKey}&units=metric
    /// Returns: WeatherData or null on error
    /// </summary>
    Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null);
    
    /// <summary>
    /// Call OpenWeatherMap API by coordinates
    /// Makes HTTP GET to: /weather?lat={lat}&lon={lon}&appid={apiKey}&units=metric
    /// Returns: WeatherData or null on error
    /// </summary>
    Task<WeatherData?> GetWeatherByCoordinatesAsync(double latitude, double longitude);
}

/// <summary>
/// ICacheService: Abstraction for caching layer
/// 
/// Responsibilities:
/// - Store and retrieve cached weather data
/// - Manage cache expiration (time-to-live)
/// - Handle cache misses gracefully
/// 
/// Current Implementation: AmazonElastiCacheService (in-memory)
/// Production Implementation: Would use Amazon ElastiCache Redis
/// Default Expiration: 30 minutes per configuration
/// Lifetime: Scoped (one per HTTP request)
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieve value from cache
    /// Returns: Cached value or null if not found/expired
    /// Generic&lt;T&gt; supports any serializable type
    /// </summary>
    Task<T?> GetAsync<T>(string key);
    
    /// <summary>
    /// Store value in cache with expiration
    /// Example: SetAsync("weather:london:gb", weatherData, TimeSpan.FromMinutes(30))
    /// Overwrites existing value if key exists
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan expiration);
    
    /// <summary>
    /// Remove value from cache immediately
    /// Safe to call on non-existent keys (no error)
    /// </summary>
    Task RemoveAsync(string key);
}

/// <summary>
/// IUserPreferencesService: DynamoDB user preference management
/// 
/// Responsibilities:
/// - Load user preferences from DynamoDB
/// - Save updated preferences
/// - Manage favorite cities per user
/// 
/// Storage: AWS DynamoDB table "UserWeatherPreferences"
/// Partition Key: UserId
/// Current Implementation: DynamoDbUserPreferencesService (stubs)
/// Lifetime: Scoped (one per HTTP request)
/// 
/// Future Enhancement:
/// - Implement actual DynamoDB calls using AWSSDK.DynamoDBv2
/// - Add authentication to identify current user
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>
    /// Retrieve user preferences from DynamoDB
    /// Returns: UserWeatherPreference or null if user has no preferences
    /// </summary>
    Task<UserWeatherPreference?> GetUserPreferencesAsync(string userId);
    
    /// <summary>
    /// Save or update user preferences in DynamoDB
    /// Performs a PUT operation (overwrites if exists)
    /// </summary>
    Task SaveUserPreferencesAsync(UserWeatherPreference preference);
    
    /// <summary>
    /// Add city to user's favorite list
    /// Updates FavoriteCities array in DynamoDB
    /// Safe to call on existing cities (idempotent)
    /// </summary>
    Task AddFavoriteCityAsync(string userId, string city);
    
    /// <summary>
    /// Remove city from user's favorite list
    /// Updates FavoriteCities array in DynamoDB
    /// Safe to call on non-existent cities (no error)
    /// </summary>
    Task RemoveFavoriteCityAsync(string userId, string city);
}
