# One image: builds the Vue app and the .NET API, then serves both from ASP.NET on port 8080.

# 1) Build the SPA
FROM node:22-bookworm-slim AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# 2) Publish the API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY src/TCSched.Api/TCSched.Api.csproj src/TCSched.Api/
RUN dotnet restore src/TCSched.Api/TCSched.Api.csproj
COPY src/TCSched.Api/ src/TCSched.Api/
RUN dotnet publish src/TCSched.Api/TCSched.Api.csproj -c Release -o /app --no-restore

# 3) Runtime: API + built SPA in wwwroot
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot

# SQLite lives on a volume. The aspnet image runs as the non-root "app" user.
USER root
RUN mkdir -p /data && chown app:app /data
USER app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=/data/tcsched.db"
EXPOSE 8080
ENTRYPOINT ["dotnet", "TCSched.Api.dll"]
