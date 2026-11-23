# ClaimTrack API container image.
# Build:  docker build -t claimtrack-api .
# Secrets are never baked in: pass them as environment variables when the container runs
# (ConnectionStrings__ClaimTrack, Jwt__SigningKey). See README.md.

# ---------- Stage 1: build ----------
# The full .NET SDK (compiler and tools). Used only to build; none of it ends up in the final image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy only the project file and restore first. Docker caches each step, so as long as the
# project file doesn't change, later builds reuse the downloaded NuGet packages.
COPY src/ClaimTrack.Api/ClaimTrack.Api.csproj src/ClaimTrack.Api/
RUN dotnet restore src/ClaimTrack.Api/ClaimTrack.Api.csproj

# Now the source code, then compile and publish a Release build to /app.
COPY src/ClaimTrack.Api/ src/ClaimTrack.Api/
RUN dotnet publish src/ClaimTrack.Api/ClaimTrack.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# An empty folder for uploaded documents. Created here because the runtime image has no shell
# to run mkdir; it's copied across below, owned by the non-root user so the API can write to it.
RUN mkdir -p /data/documents

# ---------- Stage 2: runtime ----------
# Ubuntu "chiseled" ASP.NET runtime: only what .NET needs, with no shell and no package manager,
# so there's very little for an attacker to use. "-extra" adds the culture and time-zone data
# that the SQL Server client library needs.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app

# Only the published output from stage 1: no source code, no SDK, no tests.
COPY --from=build /app .

# Document storage: the only folder the app can write to. Outside /app, so uploads never mix
# with the application's own files; docker compose mounts a volume here so they survive restarts.
COPY --from=build --chown=$APP_UID /data /data
ENV Documents__StoragePath=/data/documents

# Run as the image's built-in non-root user, never as root.
USER $APP_UID

# .NET listens on 8080 in containers by default (non-root users can't use ports below 1024).
EXPOSE 8080

# No ASPNETCORE_ENVIRONMENT set here, so it's Production: no Swagger, no demo data, JSON logs.
# Exec form (a JSON array) is required: there's no shell in this image to run a command string.
ENTRYPOINT ["dotnet", "ClaimTrack.Api.dll"]
