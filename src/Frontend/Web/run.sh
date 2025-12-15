#!/bin/bash
set -e

# Build and run SpeedReader locally with Caddy
cd ..
dotnet publish -c Release -r linux-x64 -p:OnnxLinkMode=Dynamic -o bin/Release/net10.0/linux-x64/publish
docker build -f Web/Dockerfile -t speedreader:local .

cd Web
export CADDY_HOST=localhost
export SPEEDREADER_IMAGE=speedreader:local

if [ -z "$GRAFANA_ADMIN_PASSWORD" ]; then
  echo "Error: GRAFANA_ADMIN_PASSWORD must be set"
  exit 1
fi

docker compose down
docker compose up