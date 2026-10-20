FROM mcr.microsoft.com/dotnet/aspnet:11.0.0-rc.2
ARG BUILD_SHA
ENV GIT_SHA=$BUILD_SHA
WORKDIR /app
COPY . .

EXPOSE 8090
ENTRYPOINT ["dotnet", "EggIdentity.Host.dll"]
