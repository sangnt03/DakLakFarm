# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file
COPY ["AgriEcommerces_MVC.csproj", "."]

# Restore dependencies
RUN dotnet restore "./AgriEcommerces_MVC.csproj"

# Copy all source code
COPY . .
WORKDIR "/src/."

# Build Release
RUN dotnet publish "AgriEcommerces_MVC.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Copy build artifacts from Stage 1
COPY --from=build /app/publish .

# Configure Port 8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

# Entry Point
ENTRYPOINT ["dotnet", "AgriEcommerces_MVC.dll"]