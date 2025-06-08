FROM ubuntu:20.04

# Set timezone and install basic dependencies
ENV TZ=America/Los_Angeles
RUN apt-get update && apt-get install -y tzdata && \
    ln -fs /usr/share/zoneinfo/America/Los_Angeles /etc/localtime && \
    dpkg-reconfigure -f noninteractive tzdata && \
    apt-get install -y \
    wget \
    ca-certificates \
    ffmpeg \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Create app directory
WORKDIR /app

# Copy the built application from linux-x64 subdirectory
COPY bin/Debug/net10.0/linux-x64/ /app/

# Make wheft executable
RUN chmod +x /app/wheft

ENTRYPOINT ["tail", "-f", "/dev/null"]
