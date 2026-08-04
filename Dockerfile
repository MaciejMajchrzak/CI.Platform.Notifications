ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

ARG BAGET_URL
ARG API_PROJECT=src/CI.Platform.Notifications.API/CI.Platform.Notifications.API.csproj

COPY nuget.config .
COPY ["src/CI.Platform.Notifications.Domain/CI.Platform.Notifications.Domain.csproj",                 "src/CI.Platform.Notifications.Domain/"]
COPY ["src/CI.Platform.Notifications.Core/CI.Platform.Notifications.Core.csproj",                     "src/CI.Platform.Notifications.Core/"]
COPY ["src/CI.Platform.Notifications.Infrastructure/CI.Platform.Notifications.Infrastructure.csproj", "src/CI.Platform.Notifications.Infrastructure/"]
COPY ["src/CI.Platform.Notifications.API/CI.Platform.Notifications.API.csproj",                       "src/CI.Platform.Notifications.API/"]
RUN dotnet restore ${API_PROJECT}

COPY . .
RUN dotnet publish ${API_PROJECT} -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "CI.Platform.Notifications.API.dll"]
