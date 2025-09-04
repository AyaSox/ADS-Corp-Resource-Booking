#!/usr/bin/env pwsh

Write-Host "?? Resetting ADS Corp Resource Booking Database..." -ForegroundColor Yellow

# Stop the application if it's running
Write-Host "Stopping any running instances..." -ForegroundColor Gray
Get-Process -Name "ResourceBooking" -ErrorAction SilentlyContinue | Stop-Process -Force

# Run the reset SQL script
Write-Host "Clearing database data..." -ForegroundColor Gray
try {
    sqlcmd -S "(localdb)\mssqllocaldb" -d "ResourceBookingDb" -i "reset-database.sql" -o "reset-output.log"
    Write-Host "? Database cleared successfully" -ForegroundColor Green
} catch {
    Write-Host "??  Database reset failed - continuing anyway..." -ForegroundColor Yellow
}

# Run the application to trigger reseeding
Write-Host "Starting application to reseed data..." -ForegroundColor Gray
Write-Host "?? Application will start at http://localhost:5077" -ForegroundColor Cyan
Write-Host "Press Ctrl+C to stop the application" -ForegroundColor Yellow

dotnet run