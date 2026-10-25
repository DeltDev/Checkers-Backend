# =============================================================================
# Checkers backend - multi-stage image for the ASP.NET Core Web API.
# Build context is the Checkers-Backend folder (see docker-compose.yml):
#   docker build -t checkers-backend .
# =============================================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer stays cached while only source code changes.
COPY Checkers/Checkers.csproj Checkers/
RUN dotnet restore Checkers/Checkers.csproj

# Then publish the API itself (tests and demo projects are not part of the image).
COPY Checkers/ Checkers/
RUN dotnet publish Checkers/Checkers.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime image: only the published output, listening on the container port
# 8080 (the default ASP.NET Core 8+ container port, re-declared explicitly).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Checkers.dll"]
