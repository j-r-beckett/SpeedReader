FROM ubuntu:20.04

# Install FFmpeg
RUN apt-get update && \
    apt-get install -y ffmpeg && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create app directory
WORKDIR /app

# Copy the built application from linux-x64 subdirectory
COPY bin/Debug/net10.0/linux-x64/ /app/

# Make wheft executable
RUN chmod +x /app/wheft

ENTRYPOINT ["tail", "-f", "/dev/null"]
