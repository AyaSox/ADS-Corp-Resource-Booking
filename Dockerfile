# Dockerfile for ResourceBooking App

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY ["ResourceBooking.csproj", "."]
RUN dotnet restore "./ResourceBooking.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/."
RUN dotnet build "ResourceBooking.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ResourceBooking.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ResourceBooking.dll"]
