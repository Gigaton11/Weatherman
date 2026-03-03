using Microsoft.AspNetCore.Mvc;
using WeatherDashboard.Models;
using WeatherDashboard.Services;

namespace WeatherDashboard.Controllers;

public class HomeController : Controller
{
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
            ModelState.AddModelError("city", "Please enter a city name");
            await LoadFavoriteCitiesAsync();
            return View("Index");
        }

        try
        {
            var weather = await _weatherService.GetWeatherByCityAsync(city, country);

            if (weather == null)
            {
                ModelState.AddModelError("", "Could not find weather data for this location");
                await LoadFavoriteCitiesAsync();
                return View("Index");
            }

            return View("WeatherDetail", weather);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching weather for {City}", city);
            ModelState.AddModelError("", "An error occurred while fetching weather data");
            await LoadFavoriteCitiesAsync();
            return View("Index");
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFavoriteCity(string city, string? country)
    {
        if (string.IsNullOrWhiteSpace(city))
        {
            TempData["ErrorMessage"] = "City is required to add a favorite.";
            return RedirectToAction(nameof(Index));
        }

        var userId = GetOrCreateUserId();
        var favoriteValue = BuildFavoriteValue(city, country);

        await _userPreferencesService.AddFavoriteCityAsync(userId, favoriteValue);
        TempData["SuccessMessage"] = $"Added '{favoriteValue}' to favorites.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFavoriteCity(string city, string? country)
    {
        if (string.IsNullOrWhiteSpace(city))
            return RedirectToAction(nameof(Index));

        var userId = GetOrCreateUserId();
        var favoriteValue = BuildFavoriteValue(city, country);

        await _userPreferencesService.RemoveFavoriteCityAsync(userId, favoriteValue);
        TempData["SuccessMessage"] = $"Removed '{favoriteValue}' from favorites.";
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
        const string cookieName = "wd_user_id";
        if (Request.Cookies.TryGetValue(cookieName, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return existing;

        var userId = Guid.NewGuid().ToString("N");

        // Keeps per-browser favorites without requiring authentication.
        Response.Cookies.Append(cookieName, userId, new CookieOptions
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
        var cityPart = city.Trim();
        if (string.IsNullOrWhiteSpace(country))
            return cityPart;

        return $"{cityPart},{country.Trim().ToUpperInvariant()}";
    }
}
