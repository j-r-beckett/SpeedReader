FROM ubuntu:20.04

# Install minimal system (NO FFmpeg)
RUN apt-get update && \
    apt-get install -y ca-certificates && \
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