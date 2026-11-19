# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /source
ARG SOURCE_REVISION=
COPY .editorconfig Directory.Build.props Directory.Packages.props global.json ./
COPY src/MyBlazorPlayground.Domain/MyBlazorPlayground.Domain.csproj src/MyBlazorPlayground.Domain/
COPY src/MyBlazorPlayground.Data/MyBlazorPlayground.Data.csproj src/MyBlazorPlayground.Data/
COPY src/MyBlazorPlayground.Web/MyBlazorPlayground.Web.csproj src/MyBlazorPlayground.Web/
RUN dotnet restore src/MyBlazorPlayground.Web/MyBlazorPlayground.Web.csproj
COPY src/ src/
RUN dotnet publish src/MyBlazorPlayground.Web/MyBlazorPlayground.Web.csproj \
        --configuration Release \
        --no-restore \
        --output /app \
        -p:UseAppHost=false \
        -p:SourceRevisionId="${SOURCE_REVISION}"

FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=8 CMD curl -fsS http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "MyBlazorPlayground.Web.dll"]
