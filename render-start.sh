# Render Start Script
#!/bin/bash
cd publish
dotnet ResourceBooking.dll --urls "http://0.0.0.0:$PORT"