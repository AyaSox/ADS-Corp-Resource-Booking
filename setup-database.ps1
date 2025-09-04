Write-Host "Setting up Resource Booking System Database..." -ForegroundColor Green
Write-Host ""

Write-Host "Step 1: Installing Entity Framework Tools (if not already installed)" -ForegroundColor Yellow
dotnet tool install --global dotnet-ef

Write-Host ""
Write-Host "Step 2: Creating Initial Migration" -ForegroundColor Yellow
dotnet ef migrations add InitialCreate

Write-Host ""
Write-Host "Step 3: Updating Database" -ForegroundColor Yellow
dotnet ef database update

Write-Host ""
Write-Host "Database setup complete! You can now run the application with:" -ForegroundColor Green
Write-Host "dotnet run" -ForegroundColor Cyan
Write-Host ""
Read-Host "Press Enter to continue"