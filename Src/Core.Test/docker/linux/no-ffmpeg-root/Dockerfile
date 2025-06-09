FROM ubuntu:20.04

# Install minimal system (NO FFmpeg)
RUN apt-get update && \
    apt-get install -y ca-certificates && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create app directory
WORKDIR /app

# Run as root (default)
USER root

# Keep container running
ENTRYPOINT ["tail", "-f", "/dev/null"]