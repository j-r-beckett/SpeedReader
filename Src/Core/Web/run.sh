#!/bin/bash
set -e

# Build and run SpeedReader locally with Caddy
cd ..
dotnet publish -c Release -r linux-x64 -o bin/Release/net10.0/linux-x64/publish
docker build -t speedreader:local .

cd Web
export CADDY_HOST=localhost
export CADDY_PORT=8080
export SPEEDREADER_IMAGE=speedreader:local
docker compose down
docker compose up