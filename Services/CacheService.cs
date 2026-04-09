using System.Text.Json;
using System.Globalization;
using WeatherDashboard.Models;
using Microsoft.Extensions.Caching.Memory;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace WeatherDashboard.Services;

// ============================================================================
// CACHE SERVICE - IN-MEMORY CACHE
// ============================================================================
// Provides caching abstraction for storing temporary data.
// 
// Current Implementation: In-memory cache (IMemoryCache)
// - Fast access (RAM)
// - Lost on application restart
// - Single process only (not distributed)
// 
// Production Implementation: Amazon ElastiCache (Redis)
// - Persistent across restarts
// - Shared across multiple instances
// - Better scalability
// 
// Cache Usage:
// - Weather data: 30-minute TTL (configurable)
// - Cache key format: "weather:city:country"
// - Automatic expiration based on TTL

/// <summary>
/// Cache service implementation using in-memory cache
/// Wrapper around IMemoryCache for abstraction and logging
/// 
/// In production, would connect to Amazon ElastiCache Redis cluster:
/// - Endpoint: weather-cache.xxxxx.ng.0001.use1.cache.amazonaws.com:6379
/// - Requires StackExchange.Redis NuGet package
/// </summary>
public class AmazonElastiCacheService : ICacheService
{
    private readonly ILogger<AmazonElastiCacheService> _logger;  // For logging cache operations
    private readonly IMemoryCache _memoryCache;                  // In-memory cache (development)

    /// <summary>
    /// Constructor: Inject dependencies
    /// </summary>
    public AmazonElastiCacheService(
        ILogger<AmazonElastiCacheService> logger,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _memoryCache = memoryCache;
    }

    /// <summary>
    /// Retrieve value from cache by key
    /// Generic&lt;T&gt; allows storing any serializable type
    /// </summary>
    public Task<T?> GetAsync<T>(string key)
    {
        try
        {
            // ─────────────────────────────────────────────────────────────────────
            // CACHE LOOKUP
            // ─────────────────────────────────────────────────────────────────────
            // TryGetValue returns true if key exists and is not expired
            if (_memoryCache.TryGetValue(key, out var value))
            {
                _logger.LogDebug("Cache hit for key: {Key}", key);  // Performance-critical, debug level
                return Task.FromResult((T?)value);
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);  // Cache miss is normal
            return Task.FromResult<T?>(default);  // Return null for reference types, default for value types
        }
        catch (Exception ex)
        {
            // Log error but don't throw - treat cache failures gracefully
            _logger.LogError(ex, "Error retrieving from cache: {Key}", key);
            return Task.FromResult<T?>(default);  // Return null/default to fall back to API call
        }
    }

    /// <summary>
    /// Store value in cache with time-to-live (TTL) expiration
    /// </summary>
    public Task SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        try
        {
            // ─────────────────────────────────────────────────────────────────────
            // CONFIGURE CACHE EXPIRATION OPTIONS
            // ─────────────────────────────────────────────────────────────────────
            // AbsoluteExpirationRelativeToNow: Item expires after specified duration
            // Example: 30 minutes from now
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            // ─────────────────────────────────────────────────────────────────────
            // INSERT INTO CACHE
            // ─────────────────────────────────────────────────────────────────────
            // Set creates or updates cache entry
            // If key already exists, old value is replaced
            _memoryCache.Set(key, value, cacheOptions);
            
            _logger.LogDebug("Cached value for key: {Key} with expiration: {Expiration}", key, expiration);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - non-critical to continue if caching fails
            _logger.LogError(ex, "Error setting cache for key: {Key}", key);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Remove value from cache immediately
    /// Useful for invalidating stale data
    /// </summary>
    public Task RemoveAsync(string key)
    {
        try
        {
            // Remove entry from cache
            // Safe to call on non-existent keys (no error)
            _memoryCache.Remove(key);
            
            _logger.LogDebug("Removed key from cache: {Key}", key);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            // Log error but don't throw
            _logger.LogError(ex, "Error removing from cache: {Key}", key);
            return Task.CompletedTask;
        }
    }
}

// ============================================================================
// USER PREFERENCES SERVICE - DYNAMODB
// ============================================================================
// Manages user preferences stored in AWS DynamoDB.
// Handles favorite cities and temperature unit preferences.
// 
// DynamoDB Table: "UserWeatherPreferences"
// Partition Key: UserId (String)
// Attributes:
// - UserId (String, partition key)
// - FavoriteCities (List of Strings)
// - RecentLocations (List of Strings, newest first)
// - TemperatureUnit (String: "Celsius" or "Fahrenheit")
// - LastUpdated (Number, Unix timestamp)
// 
// Billing Mode: PAY_PER_REQUEST (pay only for what you use)
// 
// TODO:
// Install NuGet: AWSSDK.DynamoDBv2
// Implement actual DynamoDB operations using AmazonDynamoDBClient

/// <summary>
/// User preferences service using DynamoDB for persistence
/// Stores favorite cities and temperature preferences per user
/// </summary>
public class DynamoDbUserPreferencesService : IUserPreferencesService
{
    private readonly ILogger<DynamoDbUserPreferencesService> _logger;  // For logging database operations
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;

    /// <summary>
    /// Constructor: Inject logger dependency
    /// </summary>
    public DynamoDbUserPreferencesService(
        ILogger<DynamoDbUserPreferencesService> logger,
        IAmazonDynamoDB dynamoDb,
        IConfiguration configuration)
    {
        _logger = logger;
        _dynamoDb = dynamoDb;
        _tableName = configuration["AWS:DynamoDB:TableName"] ?? "UserWeatherPreferences";
    }

    /// <summary>
    /// Retrieve user preferences from DynamoDB
    /// Returns: UserWeatherPreference or null if user has no preferences
    /// 
    /// TODO: Implement DynamoDB GetItem operation
    /// Example:
    /// var client = new AmazonDynamoDBClient();
    /// var request = new GetItemRequest
    /// {
    ///     TableName = "UserWeatherPreferences",
    ///     Key = new Dictionary&lt;string, AttributeValue&gt; { { "UserId", new AttributeValue { S = userId } } }
    /// };
    /// var response = await client.GetItemAsync(request);
    /// </summary>
    public async Task<UserWeatherPreference?> GetUserPreferencesAsync(string userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("GetUserPreferencesAsync called with empty userId");
                return null;
            }

            var response = await _dynamoDb.GetItemAsync(new GetItemRequest
            {
                TableName = _tableName,
                Key = BuildUserKey(userId),
                ConsistentRead = true
            });

            if (response.Item == null || response.Item.Count == 0)
            {
                _logger.LogInformation("No preferences found for user: {UserId}", userId);
                return null;
            }

            _logger.LogInformation("Retrieved preferences for user: {UserId}", userId);
            return MapPreference(response.Item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving preferences for user: {UserId}", userId);
            return null;
        }
    }

    /// <summary>
    /// Save or update user preferences in DynamoDB
    /// Performs a PUT operation (overwrites entire item if exists)
    /// 
    /// TODO: Implement DynamoDB PutItem operation
    /// </summary>
    public async Task SaveUserPreferencesAsync(UserWeatherPreference preference)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(preference.UserId))
            {
                _logger.LogWarning("SaveUserPreferencesAsync called with empty userId");
                return;
            }

            preference.LastUpdated = DateTime.UtcNow;

            var item = new Dictionary<string, AttributeValue>
            {
                ["UserId"] = new AttributeValue { S = preference.UserId },
                ["TemperatureUnit"] = new AttributeValue
                {
                    S = string.IsNullOrWhiteSpace(preference.TemperatureUnit)
                        ? "Celsius"
                        : preference.TemperatureUnit
                },
                ["LastUpdated"] = new AttributeValue
                {
                    N = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
                }
            };

            var favorites = preference.FavoriteCities
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (favorites.Count > 0)
            {
                item["FavoriteCities"] = new AttributeValue { SS = favorites };
            }

            var recentLocations = preference.RecentLocations
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            if (recentLocations.Count > 0)
            {
                item["RecentLocations"] = new AttributeValue
                {
                    L = recentLocations
                        .Select(c => new AttributeValue { S = c })
                        .ToList()
                };
            }

            await _dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = _tableName,
                Item = item
            });
            
            _logger.LogInformation("Saved preferences for user: {UserId}", preference.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving preferences for user: {UserId}", preference.UserId);
            throw;
        }
    }

    /// <summary>
    /// Add city to user's favorite cities list
    /// Updates the FavoriteCities array in DynamoDB
    /// Idempotent: safe to call multiple times with same city
    /// 
    /// TODO: Implement DynamoDB UpdateItem with ADD operation on list
    /// </summary>
    public async Task AddFavoriteCityAsync(string userId, string city)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(city))
            {
                _logger.LogWarning("AddFavoriteCityAsync called with invalid input");
                return;
            }

            var normalizedCity = city.Trim();
            var preference = await GetUserPreferencesAsync(userId) ?? new UserWeatherPreference
            {
                UserId = userId,
                TemperatureUnit = "Celsius"
            };

            preference.FavoriteCities = preference.FavoriteCities
                .Append(normalizedCity)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await SaveUserPreferencesAsync(preference);
            
            _logger.LogInformation("Added favorite city {City} for user: {UserId}", city, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding favorite city for user: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Remove city from user's favorite cities list
    /// Updates the FavoriteCities array in DynamoDB
    /// Idempotent: safe to call on non-existent cities
    /// 
    /// TODO: Implement DynamoDB UpdateItem with DELETE operation on list
    /// </summary>
    public async Task RemoveFavoriteCityAsync(string userId, string city)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(city))
            {
                _logger.LogWarning("RemoveFavoriteCityAsync called with invalid input");
                return;
            }

            var normalizedCity = city.Trim();
            var preference = await GetUserPreferencesAsync(userId);
            if (preference == null)
            {
                _logger.LogInformation("No preferences to update while removing favorite for user: {UserId}", userId);
                return;
            }

            preference.FavoriteCities = preference.FavoriteCities
                .Where(c => !string.Equals(c?.Trim(), normalizedCity, StringComparison.OrdinalIgnoreCase))
                .ToList();

            await SaveUserPreferencesAsync(preference);
            
            _logger.LogInformation("Removed favorite city {City} for user: {UserId}", city, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing favorite city for user: {UserId}", userId);
            throw;
        }
    }

    private static Dictionary<string, AttributeValue> BuildUserKey(string userId) =>
        new()
        {
            ["UserId"] = new AttributeValue { S = userId }
        };

    private static UserWeatherPreference MapPreference(Dictionary<string, AttributeValue> item)
    {
        var userId = item.TryGetValue("UserId", out var userIdAttr) ? userIdAttr.S ?? string.Empty : string.Empty;
        var temperatureUnit = item.TryGetValue("TemperatureUnit", out var unitAttr) ? unitAttr.S ?? "Celsius" : "Celsius";

        var favorites = new List<string>();
        if (item.TryGetValue("FavoriteCities", out var citiesAttr))
        {
            if (citiesAttr.SS != null && citiesAttr.SS.Count > 0)
            {
                favorites.AddRange(citiesAttr.SS);
            }
            else if (citiesAttr.L != null && citiesAttr.L.Count > 0)
            {
                favorites.AddRange(
                    citiesAttr.L
                        .Where(v => !string.IsNullOrWhiteSpace(v.S))
                        .Select(v => v.S!));
            }
        }

        var recentLocations = new List<string>();
        if (item.TryGetValue("RecentLocations", out var recentAttr) && recentAttr.L != null)
        {
            recentLocations.AddRange(
                recentAttr.L
                    .Where(v => !string.IsNullOrWhiteSpace(v.S))
                    .Select(v => v.S!.Trim()));
        }

        var lastUpdated = DateTime.UtcNow;
        if (item.TryGetValue("LastUpdated", out var updatedAttr))
        {
            if (!string.IsNullOrWhiteSpace(updatedAttr.N) &&
                long.TryParse(updatedAttr.N, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
            {
                lastUpdated = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }
            else if (!string.IsNullOrWhiteSpace(updatedAttr.S) &&
                     DateTime.TryParse(updatedAttr.S, CultureInfo.InvariantCulture,
                         DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                lastUpdated = parsed;
            }
        }

        return new UserWeatherPreference
        {
            UserId = userId,
            FavoriteCities = favorites.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RecentLocations = recentLocations
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList(),
            TemperatureUnit = temperatureUnit,
            LastUpdated = lastUpdated
        };
    }
}
