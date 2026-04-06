using WeatherDashboard.Models;

namespace WeatherDashboard.Services;

/// <summary>
/// Provides weather lookup operations consumed by MVC controllers.
/// </summary>
public interface IWeatherService
{
    /// <summary>
    /// Retrieves current weather and forecast details for a city.
    /// </summary>
    Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null);

    /// <summary>
    /// Retrieves current weather details for coordinates.
    /// </summary>
    Task<List<WeatherData>?> GetWeatherByCoordinatesAsync(double latitude, double longitude);
}

/// <summary>
/// Encapsulates external OpenWeatherMap API calls.
/// </summary>
public interface IWeatherApiClient
{
    /// <summary>
    /// Gets current weather from the upstream API for a city.
    /// </summary>
    Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null);

    /// <summary>
    /// Gets current weather from the upstream API for coordinates.
    /// </summary>
    Task<WeatherData?> GetWeatherByCoordinatesAsync(double latitude, double longitude);

    /// <summary>
    /// Gets forecast data from the upstream API for a city.
    /// </summary>
    Task<ForecastSummary> GetForecastByCityAsync(string city, string? country = null);
}

/// <summary>
/// Provides cache access for weather and forecast responses.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value by key.
    /// </summary>
    Task<T?> GetAsync<T>(string key);

    /// <summary>
    /// Stores a value in cache with a relative expiration.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan expiration);

    /// <summary>
    /// Removes a cached value by key.
    /// </summary>
    Task RemoveAsync(string key);
}

/// <summary>
/// Persists and retrieves weather-related user preferences.
/// </summary>
public interface IUserPreferencesService
{
    /// <summary>
    /// Gets stored preferences for a user.
    /// </summary>
    Task<UserWeatherPreference?> GetUserPreferencesAsync(string userId);

    /// <summary>
    /// Saves full preference state for a user.
    /// </summary>
    Task SaveUserPreferencesAsync(UserWeatherPreference preference);

    /// <summary>
    /// Adds a city to the user's favorites.
    /// </summary>
    Task AddFavoriteCityAsync(string userId, string city);

    /// <summary>
    /// Removes a city from the user's favorites.
    /// </summary>
    Task RemoveFavoriteCityAsync(string userId, string city);
}
