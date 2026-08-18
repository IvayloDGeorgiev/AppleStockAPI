# ---- Build stage ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (better layer caching), then build.
COPY ["AppleStockAPI.csproj", "./"]
RUN dotnet restore "AppleStockAPI.csproj"

COPY . .
RUN dotnet publish "AppleStockAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Listen on 8080 (no HTTPS inside the container).
ENV ASPNETCORE_URLS=http://+:8080

# Default to SQLite so the container runs self-contained. Override at runtime to use
# SQL Server, e.g.:
#   -e Database__Provider=SqlServer
#   -e ConnectionStrings__SqlServer="Server=host.docker.internal;Database=AppleStockData;User Id=sa;Password=...;TrustServerCertificate=True;"
# Supply the Alpha Vantage key with:  -e AlphaVantage__ApiKey=YOUR_KEY
ENV Database__Provider=Sqlite

EXPOSE 8080

ENTRYPOINT ["dotnet", "AppleStockAPI.dll"]
