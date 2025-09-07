#!/bin/bash
set -e

# Build and push SpeedReader docker image
dotnet publish -c Release -r linux-x64 -o bin/Release/net10.0/linux-x64/publish
docker build -t 192.168.0.12:4000/speedreader:0.3.0.dev .
docker push 192.168.0.12:4000/speedreader:0.3.0.dev

# Create init script that runs on target
cd Web
cat > init.sh << 'EOF'
#!/bin/bash
set -e
export CADDY_HOST=192.168.0.12
export CADDY_PORT=5002
export SPEEDREADER_IMAGE=192.168.0.12:4000/speedreader:0.3.0.dev
docker pull $SPEEDREADER_IMAGE
docker compose up -d
EOF
chmod +x init.sh

# Create deployment bundle
tar -czf speedreader-dev.tar.gz compose.yml Caddyfile demo.html init.sh
rm init.sh

# Deploy bundle
scp speedreader-dev.tar.gz jimmy@192.168.0.12:/home/jimmy/
rm speedreader-dev.tar.gz

# Extract and run on target
ssh jimmy@192.168.0.12 "
    rm -rf speedreader-dev &&
    mkdir speedreader-dev &&
    cd speedreader-dev &&
    tar -xzf ../speedreader-dev.tar.gz &&
    rm ../speedreader-dev.tar.gz &&
    ./init.sh
"
