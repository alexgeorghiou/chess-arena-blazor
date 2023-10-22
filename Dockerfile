# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY nuget.config Directory.Build.props ./
COPY src/ChessArena.Engine/ChessArena.Engine.csproj src/ChessArena.Engine/
COPY src/ChessArena.Core/ChessArena.Core.csproj src/ChessArena.Core/
COPY src/ChessArena.Web/ChessArena.Web.csproj src/ChessArena.Web/
RUN dotnet restore src/ChessArena.Web/ChessArena.Web.csproj

COPY src/ src/
RUN dotnet publish src/ChessArena.Web/ChessArena.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN mkdir -p /app/data && chown -R $APP_UID /app/data
VOLUME ["/app/data"]

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Arena="Data Source=/app/data/chess-arena.db" \
    Arena__HubBaseUrl="http://localhost:8080/"

COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChessArena.Web.dll"]
