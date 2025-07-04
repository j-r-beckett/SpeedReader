#!/bin/bash
set -e

dotnet publish -c Release -r linux-x64 -o bin/Release/net10.0/linux-x64/publish
docker build -t speedreader .