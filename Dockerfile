FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/ ./src/
RUN dotnet publish src/Promotora.WebApi -c Release -o /out/api
RUN dotnet publish src/Promotora.Worker -c Release -o /out/worker

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker
WORKDIR /app
COPY --from=build /out/worker/ ./
USER $APP_UID
ENTRYPOINT ["dotnet", "Promotora.Worker.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api/ ./
RUN mkdir -p /app/uploads && chown $APP_UID /app/uploads
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Promotora.WebApi.dll"]
