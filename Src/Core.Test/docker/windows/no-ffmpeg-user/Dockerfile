# escape=`

FROM mcr.microsoft.com/windows/servercore:ltsc2022

# Install minimal dependencies (NO FFmpeg)
RUN powershell -Command `
    Set-ExecutionPolicy Bypass -Scope Process -Force

# Create standard user
RUN net user testuser TestPass123! /add
RUN net localgroup users testuser /add

# Create app directory
WORKDIR C:\app
RUN icacls C:\app /grant testuser:F

# Switch to standard user
USER testuser

# Keep container running
ENTRYPOINT ["powershell", "-Command", "Start-Sleep -Seconds 2147483"]