# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore em camada separada (cache)
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/AguiaBranca.Domain/*.csproj src/AguiaBranca.Domain/
COPY src/AguiaBranca.Application/*.csproj src/AguiaBranca.Application/
COPY src/AguiaBranca.Infrastructure/*.csproj src/AguiaBranca.Infrastructure/
COPY src/AguiaBranca.Api/*.csproj src/AguiaBranca.Api/
RUN dotnet restore src/AguiaBranca.Api/AguiaBranca.Api.csproj

COPY src/ src/
RUN dotnet publish src/AguiaBranca.Api/AguiaBranca.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Imagem aspnet 8 já traz o usuário não-root "app"
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "AguiaBranca.Api.dll"]
