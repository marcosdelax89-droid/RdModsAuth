# Dockerfile para deploy em containers
# Use: docker build -t belgaauth .
# Use: docker run -p 5000:5000 belgaauth

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BelgaAuthAPI.csproj", "."]
RUN dotnet restore "BelgaAuthAPI.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "BelgaAuthAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "BelgaAuthAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BelgaAuthAPI.dll"]






