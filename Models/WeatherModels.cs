using System.Text.Json.Serialization;

namespace WeatherDashboard.Models;

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
    public List<ForecastDay> NextDaysForecast { get; set; } = new();
}

public class ForecastDay
{
    public DateTime Date { get; set; }
    public double MinTemperature { get; set; }
    public double MaxTemperature { get; set; }
    public string Description { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
}

public class WeatherSearchRequest
{
    public string City { get; set; } = string.Empty;
    public string? Country { get; set; }
}

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

public class OpenWeatherMapForecastResponse
{
    [JsonPropertyName("list")]
    public List<ForecastItem>? List { get; set; }

    [JsonPropertyName("city")]
    public ForecastCity? City { get; set; }
}

public class ForecastItem
{
    [JsonPropertyName("dt")]
    public long Dt { get; set; }

    [JsonPropertyName("main")]
    public MainData? MainData { get; set; }

    [JsonPropertyName("weather")]
    public List<Weather>? Weather { get; set; }
}

public class ForecastCity
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("timezone")]
    public int? Timezone { get; set; }
}

public class Sys
{
    [JsonPropertyName("country")]
    public string? Country { get; set; }
}

public class Coord
{
    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }
}

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

public class Wind
{
    [JsonPropertyName("speed")]
    public double Speed { get; set; }

    [JsonPropertyName("deg")]
    public int? Deg { get; set; }

    [JsonPropertyName("gust")]
    public double? Gust { get; set; }
}

public class UserWeatherPreference
{
    public string UserId { get; set; } = string.Empty;
    public List<string> FavoriteCities { get; set; } = new();
    public string TemperatureUnit { get; set; } = "Celsius";
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    public string Message { get; set; } = string.Empty;
}
