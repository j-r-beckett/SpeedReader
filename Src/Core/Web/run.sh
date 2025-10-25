#!/bin/bash
set -e

# Build and run SpeedReader locally with Caddy
cd ..
dotnet publish -c Release -r linux-x64 -o bin/Release/net10.0/linux-x64/publish
docker build -t speedreader:local .

cd Web
export CADDY_HOST=localhost
export SPEEDREADER_IMAGE=speedreader:local

# Generate random Grafana admin password if not set
if [ -z "$GRAFANA_ADMIN_PASSWORD" ]; then
  export GRAFANA_ADMIN_PASSWORD=$(openssl rand -base64 16)
  echo "=========================================="
  echo "Grafana Admin Password: $GRAFANA_ADMIN_PASSWORD"
  echo "=========================================="
fi

docker compose down
docker compose up