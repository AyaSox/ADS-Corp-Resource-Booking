@echo off
echo Setting up Resource Booking System Database...
echo.

echo Step 1: Installing Entity Framework Tools (if not already installed)
dotnet tool install --global dotnet-ef

echo.
echo Step 2: Creating Initial Migration
dotnet ef migrations add InitialCreate

echo.
echo Step 3: Updating Database
dotnet ef database update

echo.
echo Step 4: Running the application
echo Database setup complete! You can now run the application with:
echo dotnet run
echo.
pause