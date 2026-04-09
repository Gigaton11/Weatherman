using Microsoft.AspNetCore.Mvc;
using WeatherDashboard.Models;
using WeatherDashboard.Services;

namespace WeatherDashboard.Controllers;

public class HomeController : Controller
{
    private const string CityFieldName = "city";
    private const string TempDataSuccessKey = "SuccessMessage";
    private const string TempDataErrorKey = "ErrorMessage";
    private const string UserIdCookieName = "wd_user_id";
    private const string EmptyCityErrorMessage = "Please enter a city name";
    private const string WeatherNotFoundErrorMessage = "Could not find weather data for this location";
    private const string WeatherFetchErrorMessage = "An error occurred while fetching weather data";
    private const string FavoriteCityRequiredMessage = "City is required to add a favorite.";
    private const string FavoriteAddFailedMessage = "Could not save favorite city. Please try again.";
    private const string FavoriteRemoveFailedMessage = "Could not remove favorite city. Please try again.";
    private const int MaxRecentLocationsToDisplay = 3;

    private static readonly (string City, string? Country)[] DefaultRecentLocations =
    {
        ("London", "GB"),
        ("Athens", "GR"),
        ("Tokyo", "JP")
    };

    private readonly IWeatherService _weatherService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IWeatherService weatherService,
        IUserPreferencesService userPreferencesService,
        ILogger<HomeController> logger)
    {
        _weatherService = weatherService;
        _userPreferencesService = userPreferencesService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("Home index page accessed");
        await LoadHomePageDataAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SearchWeather(string city, string? country)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            ModelState.AddModelError(CityFieldName, EmptyCityErrorMessage);
            await LoadHomePageDataAsync();
            return View("Index");
        }

        try
        {
            var weather = await _weatherService.GetWeatherByCityAsync(city, country);

            if (weather == null)
            {
                ModelState.AddModelError(string.Empty, WeatherNotFoundErrorMessage);
                await LoadHomePageDataAsync();
                return View("Index");
            }

            ViewBag.IsFavoriteCity = await IsFavoriteCityAsync(weather.City, weather.Country);

            return View("WeatherDetail", weather);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching weather for {City}", city);
            ModelState.AddModelError(string.Empty, WeatherFetchErrorMessage);
            await LoadHomePageDataAsync();
            return View("Index");
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> WeatherDetail(string city, string? country)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            TempData[TempDataErrorKey] = EmptyCityErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var weather = await _weatherService.GetWeatherByCityAsync(city, country);

            if (weather == null)
            {
                TempData[TempDataErrorKey] = WeatherNotFoundErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            ViewBag.IsFavoriteCity = await IsFavoriteCityAsync(weather.City, weather.Country);

            return View(weather);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading weather detail for {City}", city);
            TempData[TempDataErrorKey] = WeatherFetchErrorMessage;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFavoriteCity(string city, string? country)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            TempData[TempDataErrorKey] = FavoriteCityRequiredMessage;
            return RedirectToAction(nameof(Index));
        }

        var userId = GetOrCreateUserId();
        var favoriteValue = BuildFavoriteValue(city, country);
        var addedSuccessfully = false;

        try
        {
            await _userPreferencesService.AddFavoriteCityAsync(userId, favoriteValue);
            TempData[TempDataSuccessKey] = $"Added '{favoriteValue}' to favorites.";
            addedSuccessfully = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed adding favorite city {City} for user {UserId}", favoriteValue, userId);
            TempData[TempDataErrorKey] = FavoriteAddFailedMessage;
        }

        if (addedSuccessfully)
            return RedirectToAction(nameof(WeatherDetail), new { city, country });

        return RedirectToAction(nameof(WeatherDetail), new { city, country });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFavoriteCity(string city, string? country)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            TempData[TempDataErrorKey] = FavoriteCityRequiredMessage;
            return RedirectToAction(nameof(Index));
        }

        var userId = GetOrCreateUserId();
        var favoriteValue = BuildFavoriteValue(city, country);

        try
        {
            await _userPreferencesService.RemoveFavoriteCityAsync(userId, favoriteValue);

            // Backward compatibility: older favorites may have been stored as city-only.
            if (!string.IsNullOrWhiteSpace(country))
            {
                await _userPreferencesService.RemoveFavoriteCityAsync(userId, city.Trim());
            }

            TempData[TempDataSuccessKey] = $"Removed '{favoriteValue}' from favorites.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed removing favorite city {City} for user {UserId}", favoriteValue, userId);
            TempData[TempDataErrorKey] = FavoriteRemoveFailedMessage;
        }

        return RedirectToAction(nameof(WeatherDetail), new { city, country });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = HttpContext.TraceIdentifier,
            Message = "An unexpected error occurred"
        });
    }

    private async Task LoadHomePageDataAsync()
    {
        var userId = GetOrCreateUserId();
        var preferences = await _userPreferencesService.GetUserPreferencesAsync(userId);
        var favoriteCities = preferences?.FavoriteCities ?? new List<string>();
        ViewBag.FavoriteCities = favoriteCities;

        var recentCandidates = BuildRecentCandidates(favoriteCities);
        var recentWeather = await LoadRecentWeatherAsync(recentCandidates);
        ViewBag.RecentLocations = recentWeather;
    }

    private static List<(string City, string? Country)> BuildRecentCandidates(IEnumerable<string> favorites)
    {
        var fromFavorites = favorites
            .Select(ParseFavoriteCity)
            .Where(x => !string.IsNullOrWhiteSpace(x.City))
            .Take(MaxRecentLocationsToDisplay)
            .ToList();

        if (fromFavorites.Count > 0)
            return fromFavorites;

        return DefaultRecentLocations.ToList();
    }

    private async Task<List<WeatherData>> LoadRecentWeatherAsync(IEnumerable<(string City, string? Country)> candidates)
    {
        var results = new List<WeatherData>();

        foreach (var (city, country) in candidates)
        {
            try
            {
                var weather = await _weatherService.GetWeatherByCityAsync(city, country);
                if (weather != null)
                    results.Add(weather);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed loading recent location weather for {City}", city);
            }
        }

        return results;
    }

    private static (string City, string? Country) ParseFavoriteCity(string favorite)
    {
        if (string.IsNullOrWhiteSpace(favorite))
            return (string.Empty, null);

        var parts = favorite
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var city = parts.Length > 0 ? parts[0] : string.Empty;
        var country = parts.Length > 1 ? parts[1] : null;

        return (city, country);
    }

    private async Task<bool> IsFavoriteCityAsync(string city, string? country)
    {
        if (string.IsNullOrWhiteSpace(city))
            return false;

        var userId = GetOrCreateUserId();
        var preferences = await _userPreferencesService.GetUserPreferencesAsync(userId);
        var favorites = preferences?.FavoriteCities ?? new List<string>();

        if (favorites.Count == 0)
            return false;

        var cityTrimmed = city.Trim();
        var countryTrimmed = country?.Trim();
        var exactFavoriteValue = BuildFavoriteValue(cityTrimmed, countryTrimmed);

        return favorites.Any(f =>
            string.Equals(f, exactFavoriteValue, StringComparison.OrdinalIgnoreCase)
            || IsFavoriteMatch(f, cityTrimmed, countryTrimmed));
    }

    private static bool IsFavoriteMatch(string favorite, string city, string? country)
    {
        var parsed = ParseFavoriteCity(favorite);
        if (!string.Equals(parsed.City, city, StringComparison.OrdinalIgnoreCase))
            return false;

        // Treat city-only favorites as matching any country variant for the same city.
        if (string.IsNullOrWhiteSpace(parsed.Country) || string.IsNullOrWhiteSpace(country))
            return true;

        return string.Equals(parsed.Country, country, StringComparison.OrdinalIgnoreCase);
    }

    private string GetOrCreateUserId()
    {
        if (Request.Cookies.TryGetValue(UserIdCookieName, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return existing;

        var userId = Guid.NewGuid().ToString("N");

        // Keeps per-browser favorites without requiring authentication.
        Response.Cookies.Append(UserIdCookieName, userId, new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });

        return userId;
    }

    private static string BuildFavoriteValue(string city, string? country)
    {
        // Store as "city,COUNTRY" so persisted favorites are compact and human-readable.
        var cityPart = city.Trim();
        if (string.IsNullOrWhiteSpace(country))
            return cityPart;

        return $"{cityPart},{country.Trim().ToUpperInvariant()}";
    }
}
