using Microsoft.AspNetCore.Mvc;
using WeatherDashboard.Models;
using WeatherDashboard.Services;

namespace WeatherDashboard.Controllers;

// ============================================================================
// HOME CONTROLLER
// ============================================================================
// Handles all user interactions for the weather dashboard:
// - Displays search form
// - Processes weather searches
// - Shows weather details
// 
// Dependencies (injected via constructor):
// - IWeatherService: Gets weather data (with caching)
// - ILogger: Logs controller actions and errors

public class HomeController : Controller
{
    private readonly IWeatherService _weatherService;  // Service for fetching weather data
    private readonly ILogger<HomeController> _logger;   // Logger for this controller

    /// <summary>
    /// Constructor: Dependencies are injected by ASP.NET Core dependency injection
    /// </summary>
    public HomeController(
        IWeatherService weatherService,
        ILogger<HomeController> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
    }

    /// <summary>
    /// GET /Home/Index
    /// Displays the weather search page (empty form)
    /// </summary>
    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("Home index page accessed");
        return View();  // Renders Views/Home/Index.cshtml
    }

    /// <summary>
    /// POST /Home/SearchWeather
    /// Processes weather search form submission
    /// Parameters:
    /// - city: City name (required)
    /// - country: Country code (optional, e.g., "US", "FR")
    /// </summary>
    [HttpPost]  // Only accepts POST requests
    public async Task<IActionResult> SearchWeather(string city, string? country)
    {
        // ─────────────────────────────────────────────────────────────────────
        // INPUT VALIDATION
        // ─────────────────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(city))
        {
            // Add error to ModelState so view can display it
            ModelState.AddModelError("city", "Please enter a city name");
            return View("Index");  // Return to search form
        }

        try
        {
            // ─────────────────────────────────────────────────────────────────────
            // FETCH WEATHER DATA
            // ─────────────────────────────────────────────────────────────────────
            // This calls WeatherService which:
            // 1. Checks cache first (Redis/ElastiCache in production)
            // 2. If not cached, calls OpenWeatherMap API
            // 3. Caches the result for 30 minutes
            var weather = await _weatherService.GetWeatherByCityAsync(city, country);

            if (weather == null)
            {
                // Weather data not found for this location
                ModelState.AddModelError("", "Could not find weather data for this location");
                return View("Index");
            }

            // ─────────────────────────────────────────────────────────────────────
            // SUCCESS - Display weather details
            // ─────────────────────────────────────────────────────────────────────
            // weather.IsFromCache will be true if it came from cache
            return View("WeatherDetail", weather);  // Renders Views/Home/WeatherDetail.cshtml
        }
        catch (Exception ex)
        {
            // ─────────────────────────────────────────────────────────────────────
            // ERROR HANDLING
            // ─────────────────────────────────────────────────────────────────────
            // Log the full exception for debugging
            _logger.LogError(ex, "Error searching weather for {City}", city);
            // Show user-friendly error message
            ModelState.AddModelError("", "An error occurred while fetching weather data");
            return View("Index");
        }
    }

    /// <summary>
    /// GET /Home/Privacy
    /// Shows privacy policy information
    /// </summary>
    public IActionResult Privacy()
    {
        return View();  // Renders Views/Home/Privacy.cshtml
    }

    /// <summary>
    /// GET /Home/Error
    /// Displays error page when exceptions occur
    /// ResponseCache ensures no caching (always fresh error page)
    /// </summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel 
        { 
            RequestId = HttpContext.TraceIdentifier,  // Unique ID for this request
            Message = "An unexpected error occurred"
        });
    }
}
