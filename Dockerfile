# syntax=docker/dockerfile:1

# ==============================================================================
# Stage 1: Build ASP.NET Core 10 Authoritative WebSocket Server
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Server/NuGet.Config Server/WolfAndRabbits.Server.csproj ./Server/
RUN dotnet restore ./Server/WolfAndRabbits.Server.csproj --configfile ./Server/NuGet.Config

COPY Server/ ./Server/
RUN dotnet publish ./Server/WolfAndRabbits.Server.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ==============================================================================
# Stage 2: Runtime (ASP.NET Core 10 + Unity WebGL client in wwwroot/)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    GAME_ENV=Release \
    ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=build /app/publish ./

RUN mkdir -p /tmp/wolf_and_rabbits_webgl_cache \
    && chown -R $APP_UID:$APP_UID /app /tmp/wolf_and_rabbits_webgl_cache

USER $APP_UID
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD bash -c 'exec 3<>/dev/tcp/127.0.0.1/8080 && printf "GET /health HTTP/1.0\r\nHost: localhost\r\n\r\n" >&3 && grep -q "200 OK" <&3'

ENTRYPOINT ["dotnet", "WolfAndRabbits.Server.dll", "--mode=release"]
