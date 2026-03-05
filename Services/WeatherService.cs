using System.Globalization;
using System.Text.Json;
using WeatherDashboard.Models;

namespace WeatherDashboard.Services;

public class WeatherService : IWeatherService
{
    private readonly IWeatherApiClient _apiClient;
    private readonly ICacheService _cacheService;
    private readonly ILogger<WeatherService> _logger;
    private readonly IConfiguration _configuration;

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

    public async Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            _logger.LogWarning("GetWeatherByCityAsync called with empty city");
            return null;
        }

        var normalizedCity = city.Trim().ToLowerInvariant();
        var normalizedCountry = string.IsNullOrWhiteSpace(country) ? "any" : country.Trim().ToLowerInvariant();

        var weatherCacheKey = $"weather:{normalizedCity}:{normalizedCountry}";
        var forecastCacheKey = $"forecast:{normalizedCity}:{normalizedCountry}";
        var cacheDuration = TimeSpan.FromMinutes(_configuration.GetValue<int>("Caching:DurationMinutes", 30));

        var forecast = await _cacheService.GetAsync<List<ForecastDay>>(forecastCacheKey);
        if (forecast == null)
        {
            forecast = await _apiClient.GetForecastByCityAsync(city, country);
            if (forecast.Count > 0)
                await _cacheService.SetAsync(forecastCacheKey, forecast, cacheDuration);
        }

        var cachedData = await _cacheService.GetAsync<WeatherData>(weatherCacheKey);
        if (cachedData != null)
        {
            cachedData.IsFromCache = true;
            cachedData.NextDaysForecast = forecast ?? new List<ForecastDay>();
            _logger.LogInformation("Weather data retrieved from cache for {City}", city);
            return cachedData;
        }

        var weatherData = await _apiClient.GetWeatherByCityAsync(city, country);
        if (weatherData != null)
        {
            weatherData.NextDaysForecast = forecast ?? new List<ForecastDay>();
            await _cacheService.SetAsync(weatherCacheKey, weatherData, cacheDuration);
            _logger.LogInformation("Weather data retrieved from API and cached for {City}", city);
        }
        else
        {
            _logger.LogWarning("Failed to retrieve weather data for {City}", city);
        }

        return weatherData;
    }

    public async Task<List<WeatherData>?> GetWeatherByCoordinatesAsync(double latitude, double longitude)
    {
        var cacheKey = $"weather:geo:{latitude:F4}:{longitude:F4}";
        var cachedData = await _cacheService.GetAsync<List<WeatherData>>(cacheKey);
        if (cachedData != null)
        {
            _logger.LogInformation("Weather data retrieved from cache for coordinates");
            return cachedData;
        }

        var weatherData = await _apiClient.GetWeatherByCoordinatesAsync(latitude, longitude);
        if (weatherData != null)
        {
            var cacheDuration = TimeSpan.FromMinutes(_configuration.GetValue<int>("Caching:DurationMinutes", 30));
            await _cacheService.SetAsync(cacheKey, new List<WeatherData> { weatherData }, cacheDuration);
        }

        return weatherData != null ? new List<WeatherData> { weatherData } : null;
    }
}

public class OpenWeatherMapClient : IWeatherApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenWeatherMapClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private static readonly Dictionary<string, string> CountryAliases = BuildCountryAliasMap();

    public OpenWeatherMapClient(
        HttpClient httpClient,
        ILogger<OpenWeatherMapClient> logger,
        IConfiguration configuration,
        ISecretsManagerService secretsManager)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _baseUrl = _configuration["WeatherApi:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5";

        var configuredApiKey = _configuration["WeatherApi:ApiKey"];
        var secretName = _configuration["WeatherApi:ApiKeySecretName"]
            ?? "weather-dashboard/openweather-api-key";

        _apiKey = !string.IsNullOrWhiteSpace(configuredApiKey)
            ? configuredApiKey
            : (secretsManager.GetSecretAsync(secretName).Result ?? string.Empty);
    }

    public async Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null)
    {
        try
        {
            var (normalizedCity, normalizedCountryCode) = NormalizeLocationInput(city, country);
            var query = !string.IsNullOrWhiteSpace(normalizedCountryCode)
                ? $"{normalizedCity},{normalizedCountryCode}"
                : normalizedCity;
            var url = $"{_baseUrl}/weather?q={Uri.EscapeDataString(query)}&appid={_apiKey}&units=metric";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var openWeatherResponse = JsonSerializer.Deserialize<OpenWeatherMapResponse>(content);

            return MapToWeatherData(openWeatherResponse);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error retrieving weather for {City}", city);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving weather for {City}", city);
            return null;
        }
    }

    public async Task<List<ForecastDay>> GetForecastByCityAsync(string city, string? country = null)
    {
        try
        {
            var (normalizedCity, normalizedCountryCode) = NormalizeLocationInput(city, country);
            var query = !string.IsNullOrWhiteSpace(normalizedCountryCode)
                ? $"{normalizedCity},{normalizedCountryCode}"
                : normalizedCity;
            var url = $"{_baseUrl}/forecast?q={Uri.EscapeDataString(query)}&appid={_apiKey}&units=metric";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var forecastResponse = JsonSerializer.Deserialize<OpenWeatherMapForecastResponse>(content);
            return MapToForecastDays(forecastResponse);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load forecast for {City}", city);
            return new List<ForecastDay>();
        }
    }

    private static List<ForecastDay> MapToForecastDays(OpenWeatherMapForecastResponse? response)
    {
        if (response?.List == null || response.List.Count == 0)
            return new List<ForecastDay>();

        var timezoneOffset = TimeSpan.FromSeconds(response.City?.Timezone ?? 0);
        var todayLocal = DateTime.UtcNow.Add(timezoneOffset).Date;

        var mapped = response.List
            .Where(i => i.MainData != null)
            .Select(i =>
            {
                var localDateTime = DateTimeOffset.FromUnixTimeSeconds(i.Dt).UtcDateTime.Add(timezoneOffset);
                return new
                {
                    LocalDate = localDateTime.Date,
                    LocalHour = localDateTime.Hour,
                    Min = i.MainData!.TempMin,
                    Max = i.MainData.TempMax,
                    Description = i.Weather?.FirstOrDefault()?.Description ?? string.Empty,
                    Icon = i.Weather?.FirstOrDefault()?.Icon ?? string.Empty
                };
            })
            .Where(i => i.LocalDate > todayLocal)
            .GroupBy(i => i.LocalDate)
            .OrderBy(g => g.Key)
            .Take(2)
            .Select(g =>
            {
                var representative = g
                    .OrderBy(x => Math.Abs(x.LocalHour - 12))
                    .ThenBy(x => x.LocalHour)
                    .First();

                return new ForecastDay
                {
                    Date = g.Key,
                    MinTemperature = g.Min(x => x.Min),
                    MaxTemperature = g.Max(x => x.Max),
                    Description = representative.Description,
                    IconUrl = string.IsNullOrWhiteSpace(representative.Icon)
                        ? string.Empty
                        : $"https://openweathermap.org/img/wn/{representative.Icon}@2x.png"
                };
            })
            .ToList();

        return mapped;
    }

    private static (string City, string? CountryCode) NormalizeLocationInput(string city, string? country)
    {
        var normalizedCity = city.Trim();
        var normalizedCountry = string.IsNullOrWhiteSpace(country) ? null : country.Trim();

        if (string.IsNullOrWhiteSpace(normalizedCountry) && normalizedCity.Contains(','))
        {
            var parts = normalizedCity.Split(',', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                normalizedCity = parts[0];
                normalizedCountry = parts[1];
            }
        }

        var countryCode = ResolveCountryCode(normalizedCountry);
        return (normalizedCity, countryCode);
    }

    private static string? ResolveCountryCode(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return null;

        var key = NormalizeCountryKey(country);
        if (CountryAliases.TryGetValue(key, out var code))
            return code;

        return country.Length == 2 ? country.ToUpperInvariant() : country;
    }

    private static Dictionary<string, string> BuildCountryAliasMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        void AddAlias(string alias, string code)
        {
            var key = NormalizeCountryKey(alias);
            if (!map.ContainsKey(key))
                map[key] = code;
        }

        AddAlias("UK", "GB");
        AddAlias("U.K.", "GB");
        AddAlias("England", "GB");
        AddAlias("USA", "US");
        AddAlias("U.S.", "US");
        AddAlias("U.S.A.", "US");
        AddAlias("Greece", "GR");
        AddAlias("Hellas", "GR");
        AddAlias("Ellada", "GR");

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            RegionInfo region;
            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch
            {
                continue;
            }

            var code = region.TwoLetterISORegionName.ToUpperInvariant();
            AddAlias(region.TwoLetterISORegionName, code);
            AddAlias(region.ThreeLetterISORegionName, code);
            AddAlias(region.EnglishName, code);
            AddAlias(region.NativeName, code);
            AddAlias(region.Name, code);
        }

        return map;
    }

    private static string NormalizeCountryKey(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        normalized = normalized.Replace(".", string.Empty);
        normalized = normalized.Replace("'", string.Empty);
        normalized = normalized.Replace("’", string.Empty);
        normalized = normalized.Replace("-", " ");
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public async Task<WeatherData?> GetWeatherByCoordinatesAsync(double latitude, double longitude)
    {
        try
        {
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

    private static WeatherData? MapToWeatherData(OpenWeatherMapResponse? response)
    {
        if (response?.MainData == null || response.Weather?.Count == 0)
            return null;

        var weather = response.Weather![0];

        return new WeatherData
        {
            City = response.Name ?? string.Empty,
            Country = response.Sys?.Country ?? string.Empty,
            Latitude = response.Coord?.Lat ?? 0,
            Longitude = response.Coord?.Lon ?? 0,
            Temperature = response.MainData.Temp,
            FeelsLike = response.MainData.FeelsLike,
            Humidity = response.MainData.Humidity,
            Pressure = response.MainData.Pressure,
            Description = weather.Description,
            IconUrl = $"https://openweathermap.org/img/wn/{weather.Icon}@2x.png",
            WindSpeed = response.Wind?.Speed ?? 0,
            RetrievedAt = DateTime.UtcNow
        };
    }
}

public interface ISecretsManagerService
{
    Task<string?> GetSecretAsync(string secretName);
}

public class SecretsManagerService : ISecretsManagerService
{
    public Task<string?> GetSecretAsync(string secretName)
    {
        return Task.FromResult<string?>(null);
    }
}


