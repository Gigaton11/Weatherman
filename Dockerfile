FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["WeatherDashboard.csproj", "./"]
RUN dotnet restore "WeatherDashboard.csproj"

COPY . .
RUN dotnet publish "WeatherDashboard.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Cloud Run sends HTTPS information via forwarding headers.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

COPY --from=build /app/publish .
RUN useradd -m -u 10001 appuser \
	&& mkdir -p /app/logs \
	&& chown -R appuser:appuser /app

USER appuser

ENTRYPOINT ["dotnet", "WeatherDashboard.dll"]