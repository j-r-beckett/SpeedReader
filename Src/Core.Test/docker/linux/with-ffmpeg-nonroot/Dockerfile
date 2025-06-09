FROM ubuntu:20.04

# Install FFmpeg
RUN apt-get update && \
    apt-get install -y ffmpeg && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user
RUN useradd -m -s /bin/bash testuser

# Create app directory and set permissions
WORKDIR /app
RUN chown testuser:testuser /app

# Switch to non-root user
USER testuser

# Keep container running
ENTRYPOINT ["tail", "-f", "/dev/null"]