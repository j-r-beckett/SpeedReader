OUTPUT_DIR="${1:-models}"
echo "OUTPUT_DIR received: '$OUTPUT_DIR'"
echo "Building models..."

docker build -t modelbuilder:latest --progress plain .
CONTAINER_ID=$(docker create modelbuilder:latest)
docker start "$CONTAINER_ID" > /dev/null

mkdir -p "$OUTPUT_DIR"
docker cp "$CONTAINER_ID":/models/. "$OUTPUT_DIR"/

docker rm "$CONTAINER_ID" > /dev/null
