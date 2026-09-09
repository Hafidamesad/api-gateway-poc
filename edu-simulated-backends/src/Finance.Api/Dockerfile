# --- Étape 1 : build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN apt-get update && apt-get install --no-install-recommends -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# --- Étape 2 : runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install --no-install-recommends -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
RUN groupadd -r appgroup && useradd -r -g appgroup -d /app -s /sbin/nologin appuser \
    && chown -R appuser:appgroup /app
USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "Finance.Api.dll"]
