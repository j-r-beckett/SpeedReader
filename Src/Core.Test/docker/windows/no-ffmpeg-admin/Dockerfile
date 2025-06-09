# escape=`

FROM mcr.microsoft.com/windows/servercore:ltsc2022

# Install minimal dependencies (NO FFmpeg)
RUN powershell -Command `
    Set-ExecutionPolicy Bypass -Scope Process -Force

# Create app directory
WORKDIR C:\app

# Run as Administrator (default in Windows containers)

# Keep container running
ENTRYPOINT ["powershell", "-Command", "Start-Sleep -Seconds 2147483"]