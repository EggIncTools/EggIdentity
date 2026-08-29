# syntax=docker/dockerfile:1.7-labs
# Build context is the repo root (ProjectReferences reach outside this project's own directory).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish EggIdentity.Agent/EggIdentity.Agent.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Executor.cs shells out to `git` (git-pull step) and `docker` (docker-pull/docker-build/docker-pull
# steps) via Process.Start, both must be on PATH inside the container. docker.io ships a client that
# talks to a mounted /var/run/docker.sock; no daemon runs in this container.
RUN apt-get update \
    && apt-get install -y --no-install-recommends git docker.io ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "EggIdentity.Agent.dll"]
