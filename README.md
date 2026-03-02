# WeatherMan 🌤️

A modern ASP.NET Core web application for real-time weather information with intelligent caching and AWS integration.

## Features

✨ **Core Features:**
- 🔍 Real-time weather search by city name
- 📍 Coordinate-based weather lookup (latitude/longitude)
- 🗂️ Smart in-memory caching (30-minute TTL)
- 🔐 Secure API key management via AWS Secrets Manager
- 📊 Structured logging with Serilog
- 💾 User preferences stored in DynamoDB

✅ **Technologies:**
- **Framework:** ASP.NET Core 8.0+
- **Language:** C# 12
- **Web:** Razor Pages + MVC
- **Logging:** Serilog (console + daily rolling files)
- **Caching:** IMemoryCache (in-memory) → Amazon ElastiCache (production)
- **Database:** AWS DynamoDB (user preferences)
- **Secrets:** AWS Secrets Manager
- **API:** OpenWeatherMap (https://openweathermap.org)

## Project Structure

```
WeatherDashboard/
├── Controllers/          # MVC controllers handling HTTP requests
│   └── HomeController.cs # Weather search and display logic
├── Services/            # Business logic layer
│   ├── WeatherService.cs           # Main orchestration (caching + API)
│   ├── CacheService.cs             # Cache abstraction
│   ├── ServiceInterfaces.cs        # Service contracts
│   └── WeatherModels.cs           # Domain models
├── Models/              # View models and data classes
│   └── WeatherModels.cs # Weather data, API responses
├── Views/               # Razor view templates
│   ├── Home/
│   │   ├── Index.cshtml           # Search form
│   │   ├── WeatherDetail.cshtml    # Weather results
│   │   └── Privacy.cshtml
│   └── Shared/
│       ├── _Layout.cshtml         # Master layout
│       └── Error.cshtml
├── wwwroot/            # Static assets
│   ├── css/site.css
│   ├── js/
│   └── lib/
├── Properties/         # Launch settings
├── Program.cs          # Application entry point
├── appsettings.json    # Configuration (production)
└── appsettings.Development.json  # Configuration (development)
```

## Getting Started

### Prerequisites
- .NET 8.0 SDK or later
- Visual Studio Code or Visual Studio 2022+
- Git
- OpenWeatherMap API key (free tier available)

### Local Development Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/YOUR_USERNAME/WeatherMan.git
   cd WeatherMan/WeatherDashboard
   ```

2. **Get OpenWeatherMap API Key:**
   - Sign up: https://openweathermap.org/api
   - Go to API Keys section
   - Copy your free API key

3. **Configure for development:**
   ```bash
   dotnet restore
   ```

4. **Set API Key (choose one):**
   
   **Option A: appsettings.Development.json**
   ```json
   {
     "WeatherApi": {
       "ApiKey": "your-openweathermap-api-key-here"
     },
     "AWS": {
       "Region": "us-east-1"
     },
     "Caching": {
       "DurationMinutes": 30
     }
   }
   ```

   **Option B: AWS Secrets Manager (recommended for production)**
   ```bash
   aws configure
   aws secretsmanager create-secret \
     --name weather-dashboard/openweather-api-key \
     --secret-string "your-api-key-here" \
     --region us-east-1
   ```

5. **Run locally:**
   ```bash
   dotnet build
   dotnet run
   ```
   
   Open browser: https://localhost:7001

## API Endpoints

### Weather Search
- **POST** `/Home/SearchWeather` - Search weather by city
  - Parameters: `city` (required), `country` (optional)
  - Returns: Weather details or error message

### Pages
- **GET** `/Home/Index` - Search form
- **GET** `/Home/Privacy` - Privacy policy
- **GET** `/Home/Error` - Error page

## AWS Deployment Setup

### Prerequisites for AWS
Ensure you have:
- AWS Account
- AWS CLI configured with credentials
- IAM permissions for DynamoDB, Secrets Manager, CloudWatch, Elastic Beanstalk

### Step 1: Create DynamoDB Table
```bash
aws dynamodb create-table \
  --table-name UserWeatherPreferences \
  --attribute-definitions AttributeName=UserId,AttributeType=S \
  --key-schema AttributeName=UserId,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1
```

### Step 2: Store API Key in Secrets Manager
```bash
aws secretsmanager create-secret \
  --name weather-dashboard/openweather-api-key \
  --secret-string "your-openweathermap-api-key" \
  --region us-east-1
```

### Step 3: Create CloudWatch Log Group (optional)
```bash
aws logs create-log-group \
  --log-group-name /aws/weather-dashboard \
  --region us-east-1
```

### Step 4: Deploy to Elastic Beanstalk
```bash
# Install EB CLI
pip install awsebcli

# Initialize Elastic Beanstalk
eb init -p "dotnet core on 64bit amazon linux 2" WeatherMan

# Create environment and deploy
eb create weather-prod
eb deploy
```

### Step 5: Check Deployment
```bash
# View environment status
eb status

# View logs
eb logs

# Open in browser
eb open
```

## Configuration

### Environment Variables / appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "WeatherApi": {
    "BaseUrl": "https://api.openweathermap.org/data/2.5",
    "ApiKey": "your-key-here"  // Development only
  },
  "AWS": {
    "Region": "us-east-1",
    "DynamoDB": {
      "TableName": "UserWeatherPreferences"
    }
  },
  "Caching": {
    "DurationMinutes": 30
  }
}
```

## Code Architecture

### Service Layer Pattern
The application uses dependency injection with service interfaces:

```
Controller → IWeatherService → IWeatherApiClient + ICacheService
                           ↓
                  OpenWeatherMapClient
                  + AmazonElastiCacheService
```

### Cache-Aside Pattern
```
1. Check cache for weather data
   ↓
2. If miss: Call OpenWeatherMap API
   ↓
3. Store result in cache (30 min TTL)
   ↓
4. Return data with IsFromCache flag
```

## Development Notes

### Adding Comments to Code
Each file includes detailed comments explaining:
- Architecture and design patterns
- Class/method responsibilities
- Data flow and transformations
- Error handling approach
- AWS integration points

Run this to understand the codebase:
```bash
# Search for implementation details
grep -r "TODO:" .
grep -r "DynamoDB" .
grep -r "Secrets Manager" .
```

### Common Tasks

**Build for production:**
```bash
dotnet publish -c Release -o ./publish
```

**Run tests (when added):**
```bash
dotnet test
```

**Update NuGet packages:**
```bash
dotnet outdated
dotnet upgrade
```

## Troubleshooting

### "API key not found"
- Check `appsettings.Development.json` has correct API key
- Verify OpenWeatherMap account is activated (check email)
- Wait ~2 hours for free tier API key activation

### "Unable to find DynamoDB table"
```bash
# List tables
aws dynamodb list-tables --region us-east-1

# Verify table has data
aws dynamodb scan --table-name UserWeatherPreferences
```

### Cache not working
- Check logs: `tail -f logs/weather-dashboard-*.txt`
- Verify memory cache is registered in Program.cs
- Monitor cache hits/misses in debug logs (LogLevel: Debug)

### Secrets Manager errors
- Verify secret exists: `aws secretsmanager get-secret-value --secret-id weather-dashboard/openweather-api-key`
- Check IAM permissions for EC2 role

## Performance Tuning

| Metric | Current | Optimized |
|--------|---------|-----------|
| Cache TTL | 30 min | Configurable |
| Log Level | Information | Debug (dev) / Warning (prod) |
| Cache Backend | In-memory | Redis (ElastiCache) |
| HTTP Timeout | Default | Custom (configurable) |

**Recommendations:**
- Use ElastiCache Redis for multi-instance deployments
- Enable CloudWatch custom metrics for cache hit rates
- Set up alarms for API rate limit approaching

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Resources

- **OpenWeatherMap API:** https://openweathermap.org/api
- **AWS SDK for .NET:** https://aws.amazon.com/sdk-for-net/
- **ASP.NET Core Docs:** https://learn.microsoft.com/en-us/aspnet/core/
- **DynamoDB Guide:** https://docs.aws.amazon.com/dynamodb/
- **Serilog:** https://serilog.net/

## Support

For issues, questions, or suggestions:
1. Check existing GitHub Issues
2. Search troubleshooting section above
3. Create a new GitHub Issue with details

---

**Happy Weather Forecasting!** 🌦️
