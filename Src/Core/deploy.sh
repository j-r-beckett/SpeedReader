dotnet publish -c Release -r linux-x64
scp bin/Release/net10.0/linux-x64/publish/speedread root@jimmybeckett.com:/srv/speedreader/binaries/0.1.0/linux-x64/speedread

dotnet publish -c Release -r win-x64
scp bin/Release/net10.0/win-x64/publish/speedread.exe root@jimmybeckett.com:/srv/speedreader/binaries/0.1.0/win-x64/speedread.exe

dotnet publish -c Release -r osx-arm64
scp bin/Release/net10.0/osx-arm64/publish/speedread root@jimmybeckett.com:/srv/speedreader/binaries/0.1.0/osx-arm64/speedread

