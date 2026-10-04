# syntax=docker/dockerfile:1

# --- web: build the React app ---
FROM node:22-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# --- sdk: restore and publish the API ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS sdk
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Foyer.Core/Foyer.Core.csproj src/Foyer.Core/
COPY src/Foyer.Api/Foyer.Api.csproj src/Foyer.Api/
RUN dotnet restore src/Foyer.Api/Foyer.Api.csproj
COPY .editorconfig ./
COPY src/ src/
# Set by release.yml: X.Y.Z for version tags, 0.0.0-dev+<sha> for develop.
ARG VERSION=0.0.0-local
RUN dotnet publish src/Foyer.Api/Foyer.Api.csproj -c Release -o /app --no-restore \
    -p:Version="$VERSION" -p:InformationalVersion="$VERSION"
COPY --from=web /src/web/dist /app/wwwroot
RUN mkdir -p /data

# --- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=sdk /app ./
COPY --from=sdk --chown=$APP_UID:$APP_UID /data /data
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080 \
    FOYER_DATA_DIR=/data
EXPOSE 8080
VOLUME /data
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD ["dotnet", "Foyer.Api.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "Foyer.Api.dll"]
