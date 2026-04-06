using System.Text.Json.Serialization;

namespace WeatherDashboard.Models;

/// <summary>
/// Combined current weather payload used by the UI.
/// </summary>
public class WeatherData
{
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Temperature { get; set; }
    public double FeelsLike { get; set; }
    public int Humidity { get; set; }
    public double Pressure { get; set; }
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public double WindSpeed { get; set; }
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    public bool IsFromCache { get; set; }
    public List<HourlyForecast> NextHoursForecast { get; set; } = new();
    public List<ForecastDay> NextDaysForecast { get; set; } = new();
}

/// <summary>
/// Single hourly forecast projection displayed on the detail page.
/// </summary>
public class HourlyForecast
{
    public DateTime DateTime { get; set; }
    public double Temperature { get; set; }
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
}

/// <summary>
/// Daily min/max forecast projection used for the 2-day summary.
/// </summary>
public class ForecastDay
{
    public DateTime Date { get; set; }
    public double MinTemperature { get; set; }
    public double MaxTemperature { get; set; }
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
}

/// <summary>
/// Grouped forecast response for upcoming hours and days.
/// </summary>
public class ForecastSummary
{
    public List<HourlyForecast> NextHoursForecast { get; set; } = new();
    public List<ForecastDay> NextDaysForecast { get; set; } = new();
}

/// <summary>
/// Request model for city-based weather searches.
/// </summary>
public class WeatherSearchRequest
{
    public string City { get; set; } = string.Empty;
    public string? Country { get; set; }
}

/// <summary>
/// Root JSON contract for OpenWeatherMap current-weather API responses.
/// </summary>
public class OpenWeatherMapResponse
{
    [JsonPropertyName("coord")]
    public Coord? Coord { get; set; }

    [JsonPropertyName("weather")]
    public List<Weather>? Weather { get; set; }

    [JsonPropertyName("main")]
    public MainData? MainData { get; set; }

    [JsonPropertyName("visibility")]
    public int? Visibility { get; set; }

    [JsonPropertyName("wind")]
    public Wind? Wind { get; set; }

    [JsonPropertyName("sys")]
    public Sys? Sys { get; set; }

    [JsonPropertyName("dt")]
    public int? Dt { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("cod")]
    public int? Cod { get; set; }
}

/// <summary>
/// Root JSON contract for OpenWeatherMap forecast API responses.
/// </summary>
public class OpenWeatherMapForecastResponse
{
    [JsonPropertyName("list")]
    public List<ForecastItem>? List { get; set; }

    [JsonPropertyName("city")]
    public ForecastCity? City { get; set; }
}

/// <summary>
/// Forecast entry from OpenWeatherMap's 3-hour interval list.
/// </summary>
public class ForecastItem
{
    [JsonPropertyName("dt")]
    public long Dt { get; set; }

    [JsonPropertyName("main")]
    public MainData? MainData { get; set; }

    [JsonPropertyName("weather")]
    public List<Weather>? Weather { get; set; }
}

/// <summary>
/// City metadata attached to forecast responses.
/// </summary>
public class ForecastCity
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("timezone")]
    public int? Timezone { get; set; }
}

/// <summary>
/// System metadata section in OpenWeatherMap responses.
/// </summary>
public class Sys
{
    [JsonPropertyName("country")]
    public string? Country { get; set; }
}

/// <summary>
/// Coordinates section in OpenWeatherMap responses.
/// </summary>
public class Coord
{
    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }
}

/// <summary>
/// Weather condition section in OpenWeatherMap responses.
/// </summary>
public class Weather
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("main")]
    public string Main { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// Main measurement section in OpenWeatherMap responses.
/// </summary>
public class MainData
{
    [JsonPropertyName("temp")]
    public double Temp { get; set; }

    [JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }

    [JsonPropertyName("temp_min")]
    public double TempMin { get; set; }

    [JsonPropertyName("temp_max")]
    public double TempMax { get; set; }

    [JsonPropertyName("pressure")]
    public int Pressure { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }
}

/// <summary>
/// Wind section in OpenWeatherMap responses.
/// </summary>
public class Wind
{
    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    [JsonPropertyName("deg")]
    public int? Deg { get; set; }

    [JsonPropertyName("gust")]
    public double? Gust { get; set; }
}

/// <summary>
/// Persisted user preferences for favorites and units.
/// </summary>
public class UserWeatherPreference
{
    public string UserId { get; set; } = string.Empty;
    public List<string> FavoriteCities { get; set; } = new();
    public string TemperatureUnit { get; set; } = "Celsius";
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error payload used by the shared MVC error view.
/// </summary>
public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    public string Message { get; set; } = string.Empty;
}
