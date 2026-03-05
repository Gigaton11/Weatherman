using WeatherDashboard.Models;

namespace WeatherDashboard.Services;

public interface IWeatherService
{
    Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null);
    Task<List<WeatherData>?> GetWeatherByCoordinatesAsync(double latitude, double longitude);
}

public interface IWeatherApiClient
{
    Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null);
    Task<WeatherData?> GetWeatherByCoordinatesAsync(double latitude, double longitude);
    Task<List<ForecastDay>> GetForecastByCityAsync(string city, string? country = null);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan expiration);
    Task RemoveAsync(string key);
}

public interface IUserPreferencesService
{
    Task<UserWeatherPreference?> GetUserPreferencesAsync(string userId);
    Task SaveUserPreferencesAsync(UserWeatherPreference preference);
    Task AddFavoriteCityAsync(string userId, string city);
    Task RemoveFavoriteCityAsync(string userId, string city);
}
