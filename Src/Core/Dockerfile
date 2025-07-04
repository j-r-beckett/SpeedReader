FROM ubuntu:22.04
COPY bin/Release/net10.0/linux-x64/publish /app
EXPOSE 5001
ENV ASPNETCORE_URLS=http://0.0.0.0:5001
ENTRYPOINT ["/app/speedread", "serve"]