# Weatherman 🌍

Weather dashboard built with ASP.NET Core using OpenWeatherMap and AWS DynamoDB. Data persisted per browser user ID, and the app is hosted on Google Cloud Run.

## 🟢 Live Demo
Visit [Weatherman](https://weatherman-750230352076.europe-west1.run.app)

## Screenshots
<summary>Home Page</summary> <details><img width="1137" height="1002" alt="Home page screenshot" src="https://github.com/user-attachments/assets/3bf5e9c7-2ff0-4f91-ab80-e62140f75d31" /></details>
<summary>Forecast Page</summary> <details><img width="1063" height="1164" alt="Forecast page screenshot" src="https://github.com/user-attachments/assets/34d29cdc-4dd2-4893-9b93-23158f864ade" /></details>

## Features

- Search current weather by city, with optional country code support.
- View a 2-day forecast aggregated from OpenWeatherMap 3-hour forecast data.
- Cache-aside weather retrieval with configurable TTL.
- Favorite cities persisted in AWS DynamoDB per browser user ID.
- Country normalization (`UK` → `GB`, `USA` → `US`).
- Responsive Razor UI with clear weather summaries.

## Tech Stack

- .NET 10 (`net10.0`)
- ASP.NET Core MVC + Razor
- Serilog (console + file logging)
- AWS SDK (DynamoDB, Secrets Manager, CloudWatch)
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
- AWS credentials/profile for DynamoDB and Secrets Manager

## Local Setup

1. Restore packages:

```bash
dotnet restore
```

2. Configure the API key (recommended via user-secrets):

```bash
dotnet user-secrets init
dotnet user-secrets set "WeatherApi:ApiKey" "YOUR_OPENWEATHERMAP_KEY"
```

3. Build and run:

```bash
dotnet build
dotnet run
```

4. Open the local URL shown in the terminal (typically `https://localhost:7001`).

## Forecast Notes

- Forecast data uses OpenWeatherMap `/forecast` (3-hour intervals) and aggregates the next 2 local calendar days.
- Forecast responses are cached according to `Caching:DurationMinutes`.

## Configuration

### `WeatherApi`

- `BaseUrl`: OpenWeatherMap base URL.
- `ApiKey`: Local/dev API key (preferred via user-secrets).
- `ApiKeySecretName`: AWS Secrets Manager secret name fallback.
- `TimeoutSeconds`: configured request timeout.

### `Caching`

- `DurationMinutes`: cache expiration for weather responses.

### `AWS`

- `Region`: AWS region for AWS clients.
- `DynamoDB:TableName`: DynamoDB table for user preferences.

## Security Notes

- `SearchWeather`, `AddFavoriteCity`, and `RemoveFavoriteCity` are POST endpoints with antiforgery validation.
- Razor forms include antiforgery tokens.
- User ID cookie is `HttpOnly` and `SameSite=Lax`.

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

Then open `http://localhost:8080`.

Production-style local check:

```bash
docker run --rm -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=Production weather-dashboard
curl http://localhost:8080/health
```

## Deploy to Google Cloud Run

This repository is structured for Dockerfile-based Cloud Run deployment.

- `Dockerfile` is in the repository root.
- `.dockerignore` trims the build context.
- The application listens on port `8080` and exposes `/health`.

### Browser deployment flow

1. Open Cloud Run in Google Cloud Console.
2. Click **Create service** and choose **Continuously deploy from a repository**.
3. Set up Cloud Build and connect GitHub if needed.
4. Select the repository and branch (for example `main`).
5. Choose **Dockerfile** build type.
6. Select a region (for example `europe-west1`).
7. Allow unauthenticated access if the service is public.
8. Add environment variables:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `AWS_ACCESS_KEY_ID=<your-aws-access-key-id>`
   - `AWS_SECRET_ACCESS_KEY=<your-aws-secret-access-key>`
   - `AWS__Region=eu-north-1`
   - `AWS__DynamoDB__TableName=UserWeatherPreferences`
   - `WeatherApi__ApiKey=<optional if not using Secrets Manager>`
9. Create the service and verify deployment.

### Post-deploy smoke checks

- Open the service URL and confirm the home page loads.
- Verify `/health` returns `OK`.
- Search for a city and confirm weather details appear.
- Add/remove a favorite city and confirm there are no server errors.
- Review Cloud Run logs for startup and request details.

## Deploy via CLI (optional)

```bash
gcloud config set project weatherman-492508
gcloud run deploy weather-dashboard \
  --source . \
  --region europe-west1 \
  --allow-unauthenticated
```

To deploy with a specific service account:

```bash
gcloud run deploy weather-dashboard \
  --source . \
  --region europe-west1 \
  --allow-unauthenticated \
  --service-account 750230352076-compute@developer.gserviceaccount.com
```

If you need to update your remote URL:

```bash
git remote set-url origin https://github.com/Gigaton11/Weatherman.git
```
