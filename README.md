# SpeedReader

Ultra-fast OCR with first-class video support.

## Quickstart

Download and run a prebuilt binary from the author's website.

The current most recent version is `0.1.0`.

### Linux
```bash
version=0.1.0
wget https://jimmybeckett.com/speedreader/binaries/$version/linux-x64/speedread
chmod +x speedread
wget https://jimmybeckett.com/speedreader/examples/rat.png
./speedread process rat.png
```

### Windows
```powershell
$version="0.1.0"
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest -Uri "https://jimmybeckett.com/speedreader/binaries/$version/win-x64/speedread.exe" -OutFile speedread.exe
Invoke-WebRequest -Uri "https://jimmybeckett.com/speedreader/examples/rat.png" -OutFile rat.png
.\speedread.exe process rat.png
```

### MacOS
```bash
version=0.1.0
wget https://jimmybeckett.com/speedreader/binaries/$version/osx-arm64/speedread
chmod +x speedread
wget https://jimmybeckett.com/speedreader/examples/rat.png
./speedread process rat.png
```

## Server Mode

```bash
./speedread serve
curl -X POST -H "Content-Type: image/jpeg" --data-binary @image.jpg http://localhost:5000/api/ocr
```

### Configuration

Configure the server interface and port using the `ASPNETCORE_URLS` environment variable:

```bash
# Listen on all interfaces, port 8080
ASPNETCORE_URLS=http://0.0.0.0:8080 ./speedread serve

# Listen on specific interface and port
ASPNETCORE_URLS=http://192.168.1.100:5000 ./speedread serve

# Multiple URLs
ASPNETCORE_URLS="http://localhost:5000;https://localhost:5001" ./speedread serve
```

Default: `http://localhost:5000`

## Build

Supported platforms: linux-x64, win-x64, osx-arm64
```bash
dotnet publish -c Release -r <platform> Src/Core
```
The `speedread` binary will be in `Src/Core/bin/Release/net10.0/<platform>/publish/`

## Benchmarks

| Date      | Commit | CPU (Cores) | RAM (Gb) | GPU | Throughput (Items / Sec) | Notes                                               |
|-----------|--------|-------------|----------| --- |--------------------------|-----------------------------------------------------|
| 2025-7-14 | ecba50ed7d7b | 6           | 8        | - | 0.5 | -                                                   |
| 2025-7-18 | 5ccfc1a4d7cb | 6 | 8 | - | 1.1 | DBNet inference w/ size 640x640 instead of 1344x736 |
| 2025-7-18 | 2ee550e49bb6 | 6 | 8 | - | 5.8 | Turn on ONNX intra-operation parallelism            |

