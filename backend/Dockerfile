# syntax=docker/dockerfile:1
# Salah Bahzad API — production image. Build context = backend/.
# "Containerising ffmpeg" = installing the ffmpeg apt package into the runtime image; the API shells out to
# the ffmpeg/ffprobe binaries in-process (FfmpegMediaTranscoder.cs), it is NOT a separate service.
# The EF migrations bundle is baked in and run as a gated, deliberate deploy step (NFR-AVAIL-004) — see
# docs/IMPLEMENTATION-PLAN-deployment.md §6.4. The Aspire AppHost is dev-only and never built here.

# ---- build + publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer: copy manifests first so `dotnet restore` is cached until a .csproj/central-package changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/SalahBahazad.Api/SalahBahazad.Api.csproj                         src/SalahBahazad.Api/
COPY src/SalahBahazad.Infrastructure/SalahBahazad.Infrastructure.csproj   src/SalahBahazad.Infrastructure/
COPY src/SalahBahazad.Application/SalahBahazad.Application.csproj          src/SalahBahazad.Application/
COPY src/SalahBahazad.Domain/SalahBahazad.Domain.csproj                   src/SalahBahazad.Domain/
COPY src/SalahBahazad.ServiceDefaults/SalahBahazad.ServiceDefaults.csproj src/SalahBahazad.ServiceDefaults/
# Restore ONLY the Api project graph (Api -> Infrastructure/ServiceDefaults -> Application -> Domain).
# AppHost is not referenced by Api, so it is excluded from the image.
RUN dotnet restore src/SalahBahazad.Api/SalahBahazad.Api.csproj

COPY src/ src/
RUN dotnet publish src/SalahBahazad.Api/SalahBahazad.Api.csproj \
      -c Release -o /app/publish --no-restore /p:UseAppHost=false

# EF migrations bundle — self-contained executable, no SDK/source needed at deploy time.
# Migrations live in the Infrastructure assembly; startup project is Api.
RUN dotnet tool install --global dotnet-ef --version 10.0.* \
 && export PATH="$PATH:/root/.dotnet/tools" \
 && dotnet ef migrations bundle \
      --project src/SalahBahazad.Infrastructure/SalahBahazad.Infrastructure.csproj \
      --startup-project src/SalahBahazad.Api/SalahBahazad.Api.csproj \
      --configuration Release -o /app/publish/efbundle

# ---- runtime (aspnet + ffmpeg) ----
# Full Debian-based aspnet image (not chiseled/alpine) so apt can install ffmpeg; ICU is present.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg \
 && rm -rf /var/lib/apt/lists/* \
 && ffmpeg -version | head -n1 && ffprobe -version | head -n1
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    Transcode__FfmpegPath=/usr/bin/ffmpeg \
    DOTNET_gcServer=0
# Non-root 'app' user ships in the .NET images; /tmp (HLS transcode scratch) is world-writable.
USER app
EXPOSE 8080
# No Dockerfile HEALTHCHECK (the aspnet image has no curl) — Traefik/Dokploy probe GET /healthz.
ENTRYPOINT ["dotnet", "SalahBahazad.Api.dll"]
