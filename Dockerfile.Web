FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore (layer caching)
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Shared/Shared.csproj src/Shared/
COPY src/Web/Web.csproj src/Web/

RUN dotnet restore src/Web/Web.csproj

# Copy source and build
COPY src/ src/
RUN dotnet publish src/Web/Web.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Web.dll"]
