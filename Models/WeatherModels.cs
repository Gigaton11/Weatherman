using System.Text.Json.Serialization;

namespace WeatherDashboard.Models;

// ============================================================================
// WEATHER DATA MODEL
// ============================================================================
// Represents current weather information for a location
// Returned by WeatherService and displayed in views
// Can indicate whether data came from cache or fresh from API

public class WeatherData
{
    /// <summary>City name (e.g., "London", "New York")</summary>
    public string City { get; set; } = string.Empty;
    
    /// <summary>Country code or name (e.g., "GB", "US")</summary>
    public string Country { get; set; } = string.Empty;
    
    /// <summary>Geographic latitude coordinate</summary>
    public double Latitude { get; set; }
    
    /// <summary>Geographic longitude coordinate</summary>
    public double Longitude { get; set; }
    
    /// <summary>Current temperature in Celsius</summary>
    public double Temperature { get; set; }
    
    /// <summary>"Feels like" temperature accounting for wind chill/humidity (Celsius)</summary>
    public double FeelsLike { get; set; }
    
    /// <summary>Relative humidity as percentage (0-100)</summary>
    public int Humidity { get; set; }
    
    /// <summary>Atmospheric pressure in hPa</summary>
    public double Pressure { get; set; }
    
    /// <summary>Weather description (e.g., "Partly cloudy", "Rainy")</summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>URL to weather icon image from OpenWeatherMap</summary>
    public string IconUrl { get; set; } = string.Empty;
    
    /// <summary>Wind speed in m/s</summary>
    public double WindSpeed { get; set; }
    
    /// <summary>When this data was retrieved (UTC)</summary>
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Whether this data came from cache (true) or fresh from API (false)</summary>
    public bool IsFromCache { get; set; }
}

/// <summary>
/// Web form model for weather search input
/// Typically used in POST requests from HTML forms
/// </summary>
public class WeatherSearchRequest
{
    /// <summary>City name to search for (required)</summary>
    public string City { get; set; } = string.Empty;
    
    /// <summary>Country code to narrow search (optional, e.g., "GB", "FR")</summary>
    public string? Country { get; set; }
}

// ============================================================================
// OPENWEATHERMAP API RESPONSE MODELS
// ============================================================================
// These classes deserialize JSON responses from OpenWeatherMap API.
// Used internally by OpenWeatherMapClient and mapped to WeatherData.

/// <summary>
/// Root response object from OpenWeatherMap current weather API
/// https://openweathermap.org/current
/// </summary>
public class OpenWeatherMapResponse
{
    [JsonPropertyName("coord")]
    public Coord? Coord { get; set; }         // Geographic coordinates
    [JsonPropertyName("weather")]
    public List<Weather>? Weather { get; set; }  // Weather conditions array
    [JsonPropertyName("main")]
    public MainData? MainData { get; set; }   // Main weather data (temperature, humidity, etc)
    [JsonPropertyName("visibility")]
    public int? Visibility { get; set; }      // Visibility distance in meters
    [JsonPropertyName("wind")]
    public Wind? Wind { get; set; }           // Wind speed and direction
    [JsonPropertyName("sys")]
    public Sys? Sys { get; set; }             // Country and sunrise/sunset metadata
    [JsonPropertyName("dt")]
    public int? Dt { get; set; }              // Data calculation timestamp (Unix)
    [JsonPropertyName("name")]
    public string? Name { get; set; }         // City name
    [JsonPropertyName("cod")]
    public int? Cod { get; set; }             // HTTP status code from API
}

/// <summary>
/// System metadata from OpenWeatherMap
/// </summary>
public class Sys
{
    [JsonPropertyName("country")]
    public string? Country { get; set; }      // ISO 3166 country code (e.g., "FR")
}

/// <summary>
/// Coordinate pair - latitude and longitude
/// </summary>
public class Coord
{
    [JsonPropertyName("lon")]
    public double Lon { get; set; }  // Longitude (-180 to 180)
    [JsonPropertyName("lat")]
    public double Lat { get; set; }  // Latitude (-90 to 90)
}

/// <summary>
/// Weather condition details
/// Contains weather group ID and short description
/// </summary>
public class Weather
{
    [JsonPropertyName("id")]
    public int Id { get; set; }                  // Weather condition ID (for icon mapping)
    [JsonPropertyName("main")]
    public string Main { get; set; } = string.Empty;        // Main weather type ("Clear", "Clouds", "Rain")
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;  // Detailed description ("Partly cloudy", "Light rain")
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;         // Icon code ("04d" = partly cloudy day)
}

/// <summary>
/// Main weather parameters (temperature, pressure, humidity)
/// </summary>
public class MainData
{
    [JsonPropertyName("temp")]
    public double Temp { get; set; }          // Current temperature (Celsius)
    [JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }     // "Feels like" temperature (Celsius)
    [JsonPropertyName("temp_min")]
    public double TempMin { get; set; }       // Minimum temperature (Celsius)
    [JsonPropertyName("temp_max")]
    public double TempMax { get; set; }       // Maximum temperature (Celsius)
    [JsonPropertyName("pressure")]
    public int Pressure { get; set; }         // Atmospheric pressure (hPa)
    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }         // Relative humidity (0-100%)
}

/// <summary>
/// Wind information
/// </summary>
public class Wind
{
    [JsonPropertyName("speed")]
    public double Speed { get; set; }   // Wind speed (m/s)
    [JsonPropertyName("deg")]
    public int? Deg { get; set; }       // Wind direction (degrees, 0-360)
    [JsonPropertyName("gust")]
    public double? Gust { get; set; }   // Wind gust speed (m/s)
}

/// <summary>
/// User preferences stored in DynamoDB
/// Tracks favorite cities and temperature unit preference
/// UserId is the partition key in DynamoDB
/// </summary>
public class UserWeatherPreference
{
    /// <summary>Unique user identifier (DynamoDB partition key)</summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>List of favorite cities this user tracks</summary>
    public List<string> FavoriteCities { get; set; } = new();
    
    /// <summary>Preferred temperature unit: "Celsius" or "Fahrenheit"</summary>
    public string TemperatureUnit { get; set; } = "Celsius";
    
    /// <summary>When user preferences were last modified (UTC)</summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error page view model
/// Passed to Error.cshtml when unhandled exceptions occur
/// </summary>
public class ErrorViewModel
{
    /// <summary>Unique request ID for tracking the error</summary>
    public string? RequestId { get; set; }
    
    /// <summary>Computed property: whether RequestId should be displayed</summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    
    /// <summary>User-friendly error message</summary>
    public string Message { get; set; } = string.Empty;
}
