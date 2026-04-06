# Weatherman 🌍

Weather forecast application made with ASP.NET Core and OpenWeatherMap's Api. User favorites are stored on AWS DynamoDB-backed.
> Hosted on Google Cloud Run.

## 🟢 Live Demo 
Visit [Weatherman](https://weatherman-750230352076.europe-west1.run.app)

## Screenshots
<summary>Home Page</summary> <details><img width="1372" height="1209" alt="image" src="https://github.com/user-attachments/assets/78acaaa4-8aa8-44de-9480-dbb8a2a89001" /></details>
<summary>Forecast Page</summary> <details><img width="1204" height="1217" alt="image" src="https://github.com/user-attachments/assets/799cacba-42d0-4904-9649-0463e473c805" /></details>

## Features

- Search current weather by city, with optional country support.
- View a 2-day forecast (daily min/max + summary icon/description) for searched cities.
- Cache-aside weather retrieval (`ICacheService`) with configurable TTL.
- Favorite cities persisted per browser user id in DynamoDB.
- Country normalization (examples: `UK` -> `GB`, `USA` -> `US`).
- Responsive UI with improved readability.

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

## Forecast Notes

- Forecast data uses OpenWeatherMap `/forecast` (3-hour intervals), aggregated into the next 2 local calendar days for the selected city.
- Forecast responses are cached using the same cache duration configured in `Caching:DurationMinutes`.

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

## Docker

Build and run locally with Docker:

```bash
docker build -t weather-dashboard .
docker run --rm -p 8080:8080 weather-dashboard
```

Open `http://localhost:8080`.

Production-style local check:

```bash
docker run --rm -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Production weather-dashboard
curl http://localhost:8080/health
```

## Deploy to Google Cloud Run (Connect Repo in Browser)

This repository is already structured for Dockerfile-based Cloud Run builds:

- `Dockerfile` is in repository root.
- `.dockerignore` trims build context.
- App listens on port `8080` and includes `/health` endpoint.

### Browser flow (continuous deployment)

1. Open Cloud Run in Google Cloud Console.
2. Click **Create service** -> **Continuously deploy from a repository (source or function)**.
3. Click **Set up with Cloud Build** and connect GitHub if not already linked.
4. Select repository and branch (for example `main`).
5. Build type: **Dockerfile**.
6. Region: choose your target region (for example `europe-west1`).
7. Authentication: **Allow unauthenticated** if this is a public app.
8. Add environment variables:
	- `ASPNETCORE_ENVIRONMENT=Production`
	- `AWS__Region=eu-north-1`
	- `AWS__DynamoDB__TableName=UserWeatherPreferences`
	- Optionally `WeatherApi__ApiKey` if not using Secrets Manager.
9. Create the service. Cloud Run will auto-deploy on future commits to the selected branch.

### Post-deploy smoke checks

1. Open service URL and verify home page loads.
2. Verify `/health` returns `OK`.
3. Search by city and confirm weather details render.
4. Add/remove a favorite city and verify no server error.
5. Check Cloud Run logs for startup and request traces.

## Deploy via CLI (optional)

This repository contains a `Dockerfile`, so Cloud Build will build the container image during deploy.

```bash
gcloud config set project weatherman-492508
gcloud run deploy weather-dashboard \
	--source . \
	--region europe-west1 \
	--allow-unauthenticated
```

To deploy with the exact service account by number:

```bash
gcloud run deploy weather-dashboard \
	--source . \
	--region europe-west1 \
	--allow-unauthenticated \
	--service-account 750230352076-compute@developer.gserviceaccount.com
```


If your existing remote URL differs only by casing/name, update it:

```bash
git remote set-url origin https://github.com/Gigaton11/Weatherman.git
```

## Known Follow-ups

- `SecretsManagerService` is still a stub.
- `TimeoutSeconds` exists in config but should be wired explicitly into `HttpClient` timeout setup.
