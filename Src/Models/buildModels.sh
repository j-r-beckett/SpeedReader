echo "Building models..."

docker build -t modelbuilder:latest --progress plain .
CONTAINER_ID=$(docker create modelbuilder:latest)
docker start "$CONTAINER_ID" > /dev/null

mkdir -p models
docker cp "$CONTAINER_ID":/models/. models/

docker rm "$CONTAINER_ID" > /dev/null
