# SpeedReader

SpeedReader is an OCR pipeline for images and video. It handles threading, batching, backpressure, and inference optimization internally, providing simple interfaces: CLI tool and HTTP API.

**Status**: Early development (v0.x). Breaking changes expected before 1.0.

## Development

Set up pre-commit hook:
```bash
echo '#!/bin/sh
exec uv run pre-commit.py "$@"' > .git/hooks/pre-commit && chmod +x .git/hooks/pre-commit
```

Build release binaries locally with [act](github.com/nektos/act).

`rm -f /tmp/speedreader && act push --artifact-server-path /tmp/speedreader -r`

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.
