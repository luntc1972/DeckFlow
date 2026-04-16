# Multi-stage build for DeckFlow.Web (.NET 10)
# Usage:
#   docker build -t mtg-deck-studio .
#   docker run --rm -p 8080:8080 -v mtg_data:/data mtg-deck-studio
#
# Persistence:
#   Set MTG_DATA_DIR=/data (already set below) and mount a volume there.
#   Both the SQLite category-knowledge DB and ChatGPT analysis artifacts
#   are redirected under that single directory.

# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first so restore can be cached
COPY DeckFlow.sln .
COPY DeckFlow.Core/DeckFlow.Core.csproj DeckFlow.Core/
COPY DeckFlow.Web/DeckFlow.Web.csproj DeckFlow.Web/
COPY DeckFlow.CLI/DeckFlow.CLI.csproj DeckFlow.CLI/

# Restore only the web project
RUN dotnet restore DeckFlow.Web/DeckFlow.Web.csproj

# Copy source code
COPY DeckFlow.Core/ DeckFlow.Core/
COPY DeckFlow.Web/ DeckFlow.Web/

# Publish the app
RUN dotnet publish DeckFlow.Web/DeckFlow.Web.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Single persistent data dir
RUN mkdir -p /data

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV MTG_DATA_DIR=/data
EXPOSE 8080

VOLUME ["/data"]

# Use PORT if provided by hosting platform, otherwise default to 8080
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet DeckFlow.Web.dll"]