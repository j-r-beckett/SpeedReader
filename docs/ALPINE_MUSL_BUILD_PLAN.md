# Alpine/musl Build Plan

Goal: Modify the existing build process to produce a static binary for Alpine Linux (musl).

## Development Environment

Run the build with act:
```bash
rm -rf /tmp/artifacts && act push --artifact-server-path /tmp/artifacts -r
```

The `-r` flag reuses the container from the previous build. To experiment:
```bash
# Get container ID after a build
docker ps -a | grep act

# Exec into container
docker start <container-id> && docker exec -it <container-id> /bin/bash
```

During ONNX builds, don't use `-r` - cmake state corruption is too easy and hard to diagnose. Use clean builds:
```bash
rm -rf /tmp/artifacts && act push --artifact-server-path /tmp/artifacts -j build-onnx-musl
```

Once ONNX is built successfully, use `-r` for speedreader_ort and .NET builds - container reuse is crucial there since those builds are iterative.

## Development Philosophy

**Don't stop until done.** Continue working until either: (1) the build is hopelessly blocked and the plan is fundamentally unsound, or (2) a statically linked and functional speedreader binary has been successfully built and tested.

**Minimal changes only.** We should only make changes when they're needed, and only make the minimal change needed to solve the problem.

- Don't throw flags at something to make it work - try different flags carefully and identify exactly which flag(s) are needed and why
- Don't apply patches preemptively - try without them first, add only when a specific error requires it
- Document why each change was necessary

## Approach

1. Modify existing `tools/build.py` and related scripts to accept `--musl` flag
2. When `--musl` is set, apply musl-specific configuration
3. Use `jirutka/setup-alpine@v1` in GitHub Actions to create Alpine chroot
4. Try Zig compiler first; fall back to native gcc only if Zig fails
5. Static linking for musl builds
6. ONNX Runtime version: 1.15.0 (same as current)
7. Alpine version: edge (required for .NET 10 SDK - `dotnet10-sdk` package)

## What We Know

### Alpine Community Patches (for reference)

Alpine's onnxruntime package (v1.23.0) applies these patches. Saved in `tools/patches/alpine-onnx/`:

| Patch | Purpose |
|-------|---------|
| `no-execinfo.patch` | Guards `<execinfo.h>` with `__GLIBC__` (musl lacks this header) |
| `flatbuffers-locale.patch.noauto` | Guards `strtod_l` etc with `__GLIBC__` (musl lacks locale functions) |
| `system.patch` | Uses system libs instead of bundled (abseil, protobuf, re2, nlohmann-json) |
| `gcc-15.patch` | Adds missing `#include <cstdint>` |
| `abseil.patch` | Removes `absl::low_level_hash` reference |
| `upb-fix.patch` | Workaround for protobuf/upb conflicts |
| `0001-Remove-MATH_NO_EXCEPT-macro.patch` | Removes problematic macro |
| `26187_disable-hascpudevice-test.patch.noauto` | Disables failing test on arm64/riscv64 |

**We don't know which (if any) of these patches are needed for our build.** Alpine uses v1.23.0 with system libraries; we use v1.15.0 with bundled libraries.

**Approach**: Try building without any patches first. If it fails, identify the specific error and apply the minimal fix. Only add patches that are actually needed.

### Build Configuration Differences

| | Ubuntu (glibc) | Alpine (musl) |
|---|---|---|
| ONNX Runtime | 1.15.0 | 1.15.0 |
| Compiler | gcc-11 | Zig (preferred) or gcc |
| Link mode | Static + Dynamic | Static only |
| Runtime ID | linux-x64 | linux-musl-x64 |
| Platform dir | linux-x64 | linux-musl-x64 |

## Implementation

### Phase 1: ONNX Runtime on Alpine

Modify `tools/build.py` to accept `--musl` flag:
- When `--musl` is set, use `platform_dir = "../target/platforms/linux-musl-x64"` instead of `linux-x64`
- Pass `musl` flag to `build_onnx()` and `build_speedreader_libs()`

The `build_onnx()` function may need musl-specific changes (compiler flags, patches). Test first without changes to see what fails.

### Phase 2: speedreader_ort on Alpine

Modify `tools/build_speedreader_libs.py` to handle musl. Zig should work since it targets musl natively.

### Phase 3: .NET Build on Alpine

The `Frontend.csproj` already uses `$(RuntimeIdentifier)` for `LibDir`, so `linux-musl-x64` will automatically resolve to `target/platforms/linux-musl-x64/lib`.

Need to add `linux-musl-x64` conditions mirroring the existing `linux-x64` conditions. The linker args should be similar but may need adjustment based on testing.

Install SDK: `apk add dotnet10-sdk-aot` (from Alpine edge community repo)

Build command:
```bash
dotnet publish -r linux-musl-x64 Src/Frontend
```

### Phase 4: Update build.yml

Update `.github/workflows/build.yml` to add musl build jobs using `jirutka/setup-alpine@v1` with `branch: edge`.

## References

- [A native, static binary with SQLite support in C#](https://pileofhacks.dev/post/a-native-static-binary-with-sqlite-support-in-c/) - Guide on building truly static .NET binaries on Alpine
- [Install .NET on Alpine](https://learn.microsoft.com/en-us/dotnet/core/install/linux-alpine) - Official .NET Alpine install docs
- [Alpine onnxruntime APKBUILD](https://git.alpinelinux.org/aports/tree/community/onnxruntime/APKBUILD)
- [setup-alpine action](https://github.com/jirutka/setup-alpine)
- [.NET linux-musl-x64 RID](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
- `tools/patches/alpine-onnx/` - Alpine's patches (for reference)
- `.github/workflows/build-ubuntu.yml` - Original glibc build (for reference)
