# syntax=docker/dockerfile:1.7-labs
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1 AS build
ARG BUILD_HEIGHT
ARG BUILD_SHA
WORKDIR /src

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    BUILD_HEIGHT="$BUILD_HEIGHT" BUILD_SHA="$BUILD_SHA" \
    dotnet restore EggIdentity.Host/EggIdentity.Host.csproj && \
    BUILD_HEIGHT="$BUILD_HEIGHT" BUILD_SHA="$BUILD_SHA" \
    dotnet publish EggIdentity.Host/EggIdentity.Host.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.1 AS runtime
ARG BUILD_SHA
ENV GIT_SHA=$BUILD_SHA
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8090
ENTRYPOINT ["dotnet", "EggIdentity.Host.dll"]
