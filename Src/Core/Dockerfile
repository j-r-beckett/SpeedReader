FROM ubuntu:20.04

# Install FFmpeg
RUN apt-get update && \
    apt-get install -y ffmpeg && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create app directory
WORKDIR /app

# Application will be copied via docker cp

ENTRYPOINT ["tail", "-f", "/dev/null"]
