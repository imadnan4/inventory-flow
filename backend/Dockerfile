FROM mcr.microsoft.com/dotnet/sdk:9.0.316 AS base
ARG APP_UID=10001
ARG APP_GID=10001
RUN groupadd --system --gid ${APP_GID} inventoryflow \
    && useradd --uid ${APP_UID} --gid inventoryflow --create-home --home-dir /home/inventoryflow --shell /usr/sbin/nologin inventoryflow \
    && mkdir -p /src /app /home/inventoryflow/.nuget/packages \
    && chown -R inventoryflow:inventoryflow /src /app /home/inventoryflow
ENV DOTNET_CLI_HOME=/home/inventoryflow \
    NUGET_PACKAGES=/home/inventoryflow/.nuget/packages
WORKDIR /src
USER inventoryflow

FROM base AS restore
COPY --chown=inventoryflow:inventoryflow Directory.Build.props Directory.Packages.props InventoryFlow.sln ./
COPY --chown=inventoryflow:inventoryflow src/InventoryFlow.Api/InventoryFlow.Api.csproj src/InventoryFlow.Api/
COPY --chown=inventoryflow:inventoryflow src/InventoryFlow.Application/InventoryFlow.Application.csproj src/InventoryFlow.Application/
COPY --chown=inventoryflow:inventoryflow src/InventoryFlow.Domain/InventoryFlow.Domain.csproj src/InventoryFlow.Domain/
COPY --chown=inventoryflow:inventoryflow src/InventoryFlow.Infrastructure/InventoryFlow.Infrastructure.csproj src/InventoryFlow.Infrastructure/
COPY --chown=inventoryflow:inventoryflow src/InventoryFlow.Shared/InventoryFlow.Shared.csproj src/InventoryFlow.Shared/
RUN dotnet restore src/InventoryFlow.Api/InventoryFlow.Api.csproj

FROM restore AS build
COPY --chown=inventoryflow:inventoryflow src ./src
RUN dotnet build src/InventoryFlow.Api/InventoryFlow.Api.csproj --configuration Release --no-restore

FROM build AS publish
RUN dotnet publish src/InventoryFlow.Api/InventoryFlow.Api.csproj --configuration Release --no-build --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0.316 AS final
ARG APP_UID=10001
ARG APP_GID=10001
RUN groupadd --system --gid ${APP_GID} inventoryflow \
    && useradd --uid ${APP_UID} --gid inventoryflow --create-home --home-dir /home/inventoryflow --shell /usr/sbin/nologin inventoryflow \
    && mkdir -p /app \
    && chown inventoryflow:inventoryflow /app
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=publish --chown=inventoryflow:inventoryflow /app/publish .
USER inventoryflow
ENTRYPOINT ["dotnet", "InventoryFlow.Api.dll"]

FROM build AS migrator
COPY --chown=inventoryflow:inventoryflow .config .config
RUN dotnet tool restore
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/InventoryFlow.Infrastructure", "--startup-project", "src/InventoryFlow.Api"]
