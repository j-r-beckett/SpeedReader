FROM ubuntu:22.04

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Create app directory and change ownership
WORKDIR /app
RUN chown appuser:appuser /app

# Switch to non-root user
USER appuser

# Application will be copied via docker cp

# Set extraction directory to /tmp
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp

ENTRYPOINT ["tail", "-f", "/dev/null"]
