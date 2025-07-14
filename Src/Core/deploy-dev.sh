#!/bin/bash
set -e

dotnet publish -c Release -r linux-x64 -o bin/Release/net10.0/linux-x64/publish
docker build -t 192.168.0.12:4000/speedreader:0.3.0.dev .
docker push 192.168.0.12:4000/speedreader:0.3.0.dev

scp compose.yml jimmy@192.168.0.12:/home/jimmy/speedreader-dev

ssh jimmy@192.168.0.12 "cd /home/jimmy/speedreader-dev && docker compose -p speedreader-dev down && docker pull 192.168.0.12:4000/speedreader:0.3.0.dev && docker compose up -d"
