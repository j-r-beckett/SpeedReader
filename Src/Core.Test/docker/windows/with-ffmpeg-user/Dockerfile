# escape=`

FROM mcr.microsoft.com/windows/servercore:ltsc2022

# Install FFmpeg using chocolatey
RUN powershell -Command `
    Set-ExecutionPolicy Bypass -Scope Process -Force; `
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; `
    iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1')); `
    choco install ffmpeg -y

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