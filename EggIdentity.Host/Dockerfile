# syntax=docker/dockerfile:1.7-labs
# Build context is the repo root (ProjectReferences reach outside this project's own directory).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore EggIdentity.Host.CssBuild/EggIdentity.Host.CssBuild.csproj && \
    dotnet publish EggIdentity.Host/EggIdentity.Host.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8090
ENTRYPOINT ["dotnet", "EggIdentity.Host.dll"]
