echo "Building models..."

# Require outDir parameter
if [ -z "$1" ]; then
  echo "Error: outDir parameter is required"
  exit 1
fi

# Check if Docker daemon is running with timeout
if ! timeout 1s docker info >/dev/null 2>&1; then
  echo "Error: Docker daemon is not running or not responding"
  exit 1
fi

OUT_DIR="$1"

docker build -t modelbuilder:latest --progress plain .
CONTAINER_ID=$(docker create modelbuilder:latest)
docker start "$CONTAINER_ID" > /dev/null

mkdir -p "$OUT_DIR"
docker cp "$CONTAINER_ID":/models/. "$OUT_DIR"/

docker rm "$CONTAINER_ID" > /dev/null
