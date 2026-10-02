# Build stage: restore and publish with the full SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so the restore layer is reused until a dependency changes.
COPY Directory.Build.props Directory.Packages.props LyricBuilder.sln ./
COPY Src/LyricBuilder.Abstractions/LyricBuilder.Abstractions.csproj Src/LyricBuilder.Abstractions/
COPY Src/LyricBuilder.Domain/LyricBuilder.Domain.csproj Src/LyricBuilder.Domain/
COPY Src/LyricBuilder.Infrastructure/LyricBuilder.Infrastructure.csproj Src/LyricBuilder.Infrastructure/
COPY Src/LyricBuilder.Core/LyricBuilder.Core.csproj Src/LyricBuilder.Core/
COPY Src/LyricBuilder.Host/LyricBuilder.Host.csproj Src/LyricBuilder.Host/
RUN dotnet restore Src/LyricBuilder.Host/LyricBuilder.Host.csproj

COPY Src/ Src/
RUN dotnet publish Src/LyricBuilder.Host/LyricBuilder.Host.csproj \
    -c Release -o /app/publish --no-restore -p:UseAppHost=false

# Runtime stage: only the ASP.NET Core runtime and the published output.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    Database__MigrateOnStartup=true

COPY --from=build /app/publish .

# The image's built-in non-root user.
USER $APP_UID

EXPOSE 8080

# Hosting platforms pass the port to listen on in PORT; fall back to 8080 without one.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet LyricBuilder.Host.dll"]
