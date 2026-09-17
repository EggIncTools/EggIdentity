# syntax=docker/dockerfile:1.7-labs
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-rc.1 AS build
WORKDIR /src

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish EggIdentity.Agent/EggIdentity.Agent.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.1 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "EggIdentity.Agent.dll"]
