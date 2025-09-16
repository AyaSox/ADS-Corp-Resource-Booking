# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as distinct layers for better caching
COPY *.csproj ./
RUN dotnet restore ResourceBooking.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish ResourceBooking.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Set ASP.NET Core to listen on the port Render expects
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Copy published app
COPY --from=build /app/publish .

# Expose port 8080 (Render will route traffic here)
EXPOSE 8080

# Start the app
ENTRYPOINT ["dotnet", "ResourceBooking.dll"]
