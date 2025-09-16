# Render Build Script
#!/bin/bash

# Install .NET SDK and restore packages
dotnet restore
dotnet publish -c Release -o ./publish --self-contained false --runtime linux-x64