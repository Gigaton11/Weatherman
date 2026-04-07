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
        await LoadFavoriteCitiesAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SearchWeather(string city, string? country)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            ModelState.AddModelError(CityFieldName, EmptyCityErrorMessage);
            await LoadFavoriteCitiesAsync();
            return View("Index");
        }

        try
        {
            var weather = await _weatherService.GetWeatherByCityAsync(city, country);

            if (weather == null)
            {
                ModelState.AddModelError(string.Empty, WeatherNotFoundErrorMessage);
                await LoadFavoriteCitiesAsync();
                return View("Index");
            }

            return View("WeatherDetail", weather);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching weather for {City}", city);
            ModelState.AddModelError(string.Empty, WeatherFetchErrorMessage);
            await LoadFavoriteCitiesAsync();
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

        try
        {
            await _userPreferencesService.AddFavoriteCityAsync(userId, favoriteValue);
            TempData[TempDataSuccessKey] = $"Added '{favoriteValue}' to favorites.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed adding favorite city {City} for user {UserId}", favoriteValue, userId);
            TempData[TempDataErrorKey] = FavoriteAddFailedMessage;
        }

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
            TempData[TempDataSuccessKey] = $"Removed '{favoriteValue}' from favorites.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed removing favorite city {City} for user {UserId}", favoriteValue, userId);
            TempData[TempDataErrorKey] = FavoriteRemoveFailedMessage;
        }

        return RedirectToAction(nameof(Index));
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

    private async Task LoadFavoriteCitiesAsync()
    {
        var userId = GetOrCreateUserId();
        var preferences = await _userPreferencesService.GetUserPreferencesAsync(userId);
        ViewBag.FavoriteCities = preferences?.FavoriteCities ?? new List<string>();
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
