# SpeedReader

SpeedReader is an OCR pipeline for images and video. It handles threading, batching, backpressure, and inference optimization internally, providing simple interfaces: CLI tool and HTTP API.

**Status**: Early development (v0.x). Breaking changes expected before 1.0.

## Quickstart

Download and run a prebuilt binary from the author's website.

The current most recent version is `0.1.0`.

### Linux
```bash
version=0.1.0
wget https://jimmybeckett.com/speedreader/binaries/$version/linux-x64/speedread
chmod +x speedread
wget https://jimmybeckett.com/speedreader/examples/rat.png
./speedread rat.png
```

### Windows
```powershell
$version="0.1.0"
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest -Uri "https://jimmybeckett.com/speedreader/binaries/$version/win-x64/speedread.exe" -OutFile speedread.exe
Invoke-WebRequest -Uri "https://jimmybeckett.com/speedreader/examples/rat.png" -OutFile rat.png
.\speedread.exe rat.png
```

### MacOS
```bash
version=0.1.0
wget https://jimmybeckett.com/speedreader/binaries/$version/osx-arm64/speedread
chmod +x speedread
wget https://jimmybeckett.com/speedreader/examples/rat.png
./speedread rat.png
```

## Server Mode

Run as an HTTP server with a simple POST endpoint:

```bash
./speedread --serve
curl -X POST -H "Content-Type: image/jpeg" --data-binary @image.jpg http://localhost:5000/api/ocr
```

### Configuration

Configure the server interface and port using the `ASPNETCORE_URLS` environment variable:

```bash
# Listen on all interfaces, port 8080
ASPNETCORE_URLS=http://0.0.0.0:8080 ./speedread --serve

# Listen on specific interface and port
ASPNETCORE_URLS=http://192.168.1.100:5000 ./speedread --serve

# Multiple URLs
ASPNETCORE_URLS="http://localhost:5000;https://localhost:5001" ./speedread --serve
```

Default: `http://localhost:5000`

## Development

### Building from Source

Supported platforms: linux-x64, win-x64, osx-arm64

```bash
dotnet publish -c Release -r <platform> Src/Core
```

The `speedread` binary will be in `Src/Core/bin/Release/net10.0/<platform>/publish/`

### Pre-commit Hook

To install the pre-commit hook:

```bash
ln -sf "../../pre-commit.sh" .git/hooks/pre-commit
```

This enforces code formatting, builds with warnings as errors, and requires 100% test pass rate.

### Project Guidance

See [CLAUDE.md](CLAUDE.md) for detailed development guidelines and project structure.

## Architecture

SpeedReader uses a two-stage OCR pipeline:

1. **Text Detection**: [DBNet](https://github.com/open-mmlab/mmocr) (ResNet18 backbone) detects text regions in images
2. **Text Recognition**: [SVTRv2](https://github.com/Topdu/OpenOCR) recognizes characters within detected regions

Both models are converted to ONNX format and quantized to INT8 for performance. The pipeline includes adaptive CPU concurrency tuning that adjusts parallelism based on throughput.

### Models

- **DBNet**: [MMOCR](https://github.com/open-mmlab/mmocr) v1.0.1, `dbnet_resnet18_fpnc_1200e_icdar2015`
- **SVTRv2**: [OpenOCR](https://github.com/Topdu/OpenOCR), `repsvtr_ch`

Models are automatically downloaded and converted during the build process.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.
