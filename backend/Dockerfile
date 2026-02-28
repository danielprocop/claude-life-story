# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY DiarioIntelligente.Core/DiarioIntelligente.Core.csproj DiarioIntelligente.Core/
COPY DiarioIntelligente.Infrastructure/DiarioIntelligente.Infrastructure.csproj DiarioIntelligente.Infrastructure/
COPY DiarioIntelligente.AI/DiarioIntelligente.AI.csproj DiarioIntelligente.AI/
COPY DiarioIntelligente.API/DiarioIntelligente.API.csproj DiarioIntelligente.API/
RUN dotnet restore DiarioIntelligente.API/DiarioIntelligente.API.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish DiarioIntelligente.API/DiarioIntelligente.API.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# App Runner expects port 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "DiarioIntelligente.API.dll"]
