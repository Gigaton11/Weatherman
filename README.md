# Weather Dashboard

ASP.NET Core MVC weather application with OpenWeatherMap integration, caching, and DynamoDB-backed user favorites.

## Features

- Search current weather by city, with optional country support.
- Country normalization (examples: `UK` -> `GB`, `USA` -> `US`).
- Cache-aside weather retrieval (`ICacheService`) with configurable TTL.
- Favorite cities persisted per browser user id in DynamoDB.
- Responsive UI with improved readability and a subtle sunset background.

## Stack

- .NET 10 (`net10.0`)
- ASP.NET Core MVC + Razor
- Serilog (console + file)
- AWS SDK (DynamoDB, Secrets Manager, CloudWatch package reference)
- OpenWeatherMap API

## Project Structure

```text
Controllers/
Models/
Services/
Views/
wwwroot/
Program.cs
appsettings.json
```

## Prerequisites

- .NET 10 SDK
- OpenWeatherMap API key
- AWS credentials/profile if using DynamoDB/Secrets Manager in non-local environments

## Local Setup

1. Restore packages:

```bash
dotnet restore
```

2. Configure API key (recommended via user-secrets):

```bash
dotnet user-secrets init
dotnet user-secrets set "WeatherApi:ApiKey" "YOUR_OPENWEATHERMAP_KEY"
```

3. Build and run:

```bash
dotnet build
dotnet run
```

4. Open the local URL shown in terminal (typically `https://localhost:7001`).

## Configuration

### `WeatherApi`

- `BaseUrl`: OpenWeatherMap base URL.
- `ApiKey`: Local/dev API key (preferred in user-secrets).
- `ApiKeySecretName`: AWS Secrets Manager key name fallback.
- `TimeoutSeconds`: intended timeout configuration.

### `Caching`

- `DurationMinutes`: cache expiration for weather responses.

### `AWS`

- `Region`: AWS region for clients.
- `DynamoDB:TableName`: table used by `DynamoDbUserPreferencesService`.

## Security Notes

- `SearchWeather`, `AddFavoriteCity`, and `RemoveFavoriteCity` are POST endpoints with antiforgery validation.
- Razor forms posting to these actions include antiforgery tokens.
- User id cookie is `HttpOnly` and `SameSite=Lax`.

## Useful Commands

```bash
dotnet build WeatherDashboard.csproj /p:UseAppHost=false
dotnet run
```

## Release / Push Checklist

Repository target:
- `https://github.com/Gigaton11/Weatherman.git`

From repo root:

```bash
git status
git add .
git commit -m "Finalize UI/UX polish, security checks, and documentation"
git push origin main
```

If your existing remote URL differs only by casing/name, update it:

```bash
git remote set-url origin https://github.com/Gigaton11/Weatherman.git
```

## Known Follow-ups

- `SecretsManagerService` is still a stub.
- `TimeoutSeconds` exists in config but should be wired explicitly into `HttpClient` timeout setup.
