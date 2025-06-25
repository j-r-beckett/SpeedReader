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

## Build

Supported platforms: linux-x64, win-x64, osx-arm64
```bash
dotnet publish -c Release -r <platform> Src/Core
```
The `speedread` binary will be in `Src/Core/bin/Release/net10.0/<platform>/publish/`
