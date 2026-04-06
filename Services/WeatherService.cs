using System.Globalization;
using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using WeatherDashboard.Models;

namespace WeatherDashboard.Services;

public class WeatherService : IWeatherService
{
    private const string WeatherCachePrefix = "weather";
    private const string ForecastCachePrefix = "forecast";
    private const string GeoCachePrefix = "weather:geo";

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

        var weatherCacheKey = $"{WeatherCachePrefix}:{normalizedCity}:{normalizedCountry}";
        var forecastCacheKey = $"{ForecastCachePrefix}:{normalizedCity}:{normalizedCountry}";
        var cacheDuration = TimeSpan.FromMinutes(_configuration.GetValue<int>("Caching:DurationMinutes", 30));

        var forecast = await _cacheService.GetAsync<ForecastSummary>(forecastCacheKey);
        if (forecast == null)
        {
            forecast = await _apiClient.GetForecastByCityAsync(city, country);
            if (forecast.NextHoursForecast.Count > 0 || forecast.NextDaysForecast.Count > 0)
                await _cacheService.SetAsync(forecastCacheKey, forecast, cacheDuration);
        }

        var cachedData = await _cacheService.GetAsync<WeatherData>(weatherCacheKey);
        if (cachedData != null)
        {
            cachedData.IsFromCache = true;
            ApplyForecastData(cachedData, forecast);
            _logger.LogInformation("Weather data retrieved from cache for {City}", city);
            return cachedData;
        }

        var weatherData = await _apiClient.GetWeatherByCityAsync(city, country);
        if (weatherData != null)
        {
            ApplyForecastData(weatherData, forecast);
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
        if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
        {
            _logger.LogWarning("Invalid coordinates provided: {Latitude},{Longitude}", latitude, longitude);
            return null;
        }

        var cacheKey = $"{GeoCachePrefix}:{latitude:F4}:{longitude:F4}";
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

    private static void ApplyForecastData(WeatherData weatherData, ForecastSummary? forecast)
    {
        weatherData.NextHoursForecast = forecast?.NextHoursForecast ?? new List<HourlyForecast>();
        weatherData.NextDaysForecast = forecast?.NextDaysForecast ?? new List<ForecastDay>();
    }
}

public class OpenWeatherMapClient : IWeatherApiClient
{
    private static readonly string[] ApiKeyEnvironmentVariables =
    {
        "WeatherApi__ApiKey",
        "WEATHER_API_KEY",
        "OPENWEATHER_API_KEY"
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenWeatherMapClient> _logger;
    private readonly string _baseUrl;
    private readonly ISecretsManagerService _secretsManager;
    private readonly string? _configuredApiKey;
    private readonly string _apiKeySecretName;
    private string? _resolvedApiKey;
    private static readonly Dictionary<string, string> CountryAliases = BuildCountryAliasMap();

    public OpenWeatherMapClient(
        HttpClient httpClient,
        ILogger<OpenWeatherMapClient> logger,
        IConfiguration configuration,
        ISecretsManagerService secretsManager)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = configuration["WeatherApi:BaseUrl"] ?? "https://api.openweathermap.org/data/2.5";
        _secretsManager = secretsManager;

        _configuredApiKey = configuration["WeatherApi:ApiKey"];
        _apiKeySecretName = configuration["WeatherApi:ApiKeySecretName"]
            ?? "weather-dashboard/openweather-api-key";
    }

    public async Task<WeatherData?> GetWeatherByCityAsync(string city, string? country = null)
    {
        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("OpenWeatherMap API key is not configured");
                return null;
            }

            var (normalizedCity, normalizedCountryCode) = NormalizeLocationInput(city, country);
            var query = !string.IsNullOrWhiteSpace(normalizedCountryCode)
                ? $"{normalizedCity},{normalizedCountryCode}"
                : normalizedCity;
            var url = $"{_baseUrl}/weather?q={Uri.EscapeDataString(query)}&appid={apiKey}&units=metric";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "OpenWeatherMap returned {StatusCode} for city {City}. Response: {Response}",
                    (int)response.StatusCode,
                    city,
                    errorBody);
                return null;
            }

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

    public async Task<ForecastSummary> GetForecastByCityAsync(string city, string? country = null)
    {
        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("OpenWeatherMap API key is not configured");
                return new ForecastSummary();
            }

            var (normalizedCity, normalizedCountryCode) = NormalizeLocationInput(city, country);
            var query = !string.IsNullOrWhiteSpace(normalizedCountryCode)
                ? $"{normalizedCity},{normalizedCountryCode}"
                : normalizedCity;
            var url = $"{_baseUrl}/forecast?q={Uri.EscapeDataString(query)}&appid={apiKey}&units=metric";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var forecastResponse = JsonSerializer.Deserialize<OpenWeatherMapForecastResponse>(content);
            return MapToForecastSummary(forecastResponse);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load forecast for {City}", city);
            return new ForecastSummary();
        }
    }

    private static ForecastSummary MapToForecastSummary(OpenWeatherMapForecastResponse? response)
    {
        if (response?.List == null || response.List.Count == 0)
            return new ForecastSummary();

        // Forecast timestamps are UTC. Convert each point to city-local time so hourly and daily
        // summaries align with the target location rather than the server timezone.
        var timezoneOffset = TimeSpan.FromSeconds(response.City?.Timezone ?? 0);
        var localNow = DateTime.UtcNow.Add(timezoneOffset);
        var todayLocal = localNow.Date;
        var nextFullHour = new DateTime(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            localNow.Hour,
            0,
            0,
            localNow.Kind).AddHours(1);

        var mapped = response.List
            .Where(i => i.MainData != null)
            .Select(i =>
            {
                var localDateTime = DateTimeOffset.FromUnixTimeSeconds(i.Dt).UtcDateTime.Add(timezoneOffset);
                return new
                {
                    LocalDateTime = localDateTime,
                    LocalDate = localDateTime.Date,
                    LocalHour = localDateTime.Hour,
                    Temperature = i.MainData!.Temp,
                    Min = i.MainData.TempMin,
                    Max = i.MainData.TempMax,
                    Description = i.Weather?.FirstOrDefault()?.Description ?? string.Empty,
                    Icon = i.Weather?.FirstOrDefault()?.Icon ?? string.Empty
                };
            })
            .ToList();

        // Build 6 upcoming hourly slots and choose the first forecast data point at/after each slot.
        var nextHoursForecast = Enumerable.Range(0, 6)
            .Select(offset => nextFullHour.AddHours(offset))
            .Select(hourSlot =>
            {
                var source = mapped
                    .Where(i => i.LocalDateTime >= hourSlot)
                    .OrderBy(i => i.LocalDateTime)
                    .FirstOrDefault()
                    ?? mapped.OrderByDescending(i => i.LocalDateTime).First();

                return new HourlyForecast
                {
                    DateTime = hourSlot,
                    Temperature = source.Temperature,
                    Description = source.Description,
                    IconUrl = string.IsNullOrWhiteSpace(source.Icon)
                        ? string.Empty
                        : $"https://openweathermap.org/img/wn/{source.Icon}@2x.png"
                };
            })
            .ToList();

        // For each upcoming day, pick an entry closest to noon as the representative condition.
        var nextDaysForecast = mapped
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

        return new ForecastSummary
        {
            NextHoursForecast = nextHoursForecast,
            NextDaysForecast = nextDaysForecast
        };
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

        // Include common aliases and region-derived names so input like "UK" or "Hellas"
        // still resolves to ISO country codes accepted by OpenWeatherMap.
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
        normalized = normalized.Replace("�", string.Empty);
        normalized = normalized.Replace("-", " ");
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public async Task<WeatherData?> GetWeatherByCoordinatesAsync(double latitude, double longitude)
    {
        try
        {
            var apiKey = await GetApiKeyAsync();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("OpenWeatherMap API key is not configured");
                return null;
            }

            var url = $"{_baseUrl}/weather?lat={latitude}&lon={longitude}&appid={apiKey}&units=metric";

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

    private async Task<string> GetApiKeyAsync()
    {
        if (!string.IsNullOrWhiteSpace(_resolvedApiKey))
            return _resolvedApiKey;

        if (!string.IsNullOrWhiteSpace(_configuredApiKey))
        {
            _resolvedApiKey = _configuredApiKey;
            return _resolvedApiKey;
        }

        foreach (var envVarName in ApiKeyEnvironmentVariables)
        {
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                _resolvedApiKey = envValue;
                return _resolvedApiKey;
            }
        }

        _resolvedApiKey = await _secretsManager.GetSecretAsync(_apiKeySecretName) ?? string.Empty;
        return _resolvedApiKey;
    }
}

public interface ISecretsManagerService
{
    Task<string?> GetSecretAsync(string secretName);
}

public class SecretsManagerService : ISecretsManagerService
{
    private readonly IAmazonSecretsManager _secretsManagerClient;
    private readonly ILogger<SecretsManagerService> _logger;

    public SecretsManagerService(
        IAmazonSecretsManager secretsManagerClient,
        ILogger<SecretsManagerService> logger)
    {
        _secretsManagerClient = secretsManagerClient;
        _logger = logger;
    }

    public async Task<string?> GetSecretAsync(string secretName)
    {
        if (string.IsNullOrWhiteSpace(secretName))
            return null;

        try
        {
            var response = await _secretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest
            {
                SecretId = secretName
            });

            if (!string.IsNullOrWhiteSpace(response.SecretString))
            {
                return ExtractSecretValue(response.SecretString);
            }

            if (response.SecretBinary != null)
            {
                response.SecretBinary.Position = 0;
                using var reader = new StreamReader(response.SecretBinary);
                var binaryValue = await reader.ReadToEndAsync();
                return ExtractSecretValue(binaryValue);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve secret {SecretName} from AWS Secrets Manager", secretName);
            return null;
        }
    }

    private static string? ExtractSecretValue(string rawSecret)
    {
        if (string.IsNullOrWhiteSpace(rawSecret))
            return null;

        var trimmed = rawSecret.Trim();
        if (!trimmed.StartsWith('{'))
            return trimmed;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return trimmed;

            var commonKeys = new[]
            {
                "apiKey",
                "ApiKey",
                "weatherApiKey",
                "WeatherApiKey",
                "openWeatherApiKey",
                "OPENWEATHER_API_KEY",
                "value"
            };

            foreach (var key in commonKeys)
            {
                if (root.TryGetProperty(key, out var valueNode) && valueNode.ValueKind == JsonValueKind.String)
                {
                    var value = valueNode.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }

            if (root.EnumerateObject().Count() == 1)
            {
                var only = root.EnumerateObject().First();
                if (only.Value.ValueKind == JsonValueKind.String)
                {
                    var value = only.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }

            return trimmed;
        }
        catch
        {
            return trimmed;
        }
    }
}


