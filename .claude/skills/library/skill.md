---
name: dependency-library
description: Local dependency source code exploration for .NET/C# projects. Use when needing to understand how a NuGet package works, investigate its API behavior, find usage examples in tests, read source-level documentation, or debug integration issues. Clone dependency repos locally and use ripgrep to explore source code instead of web navigation.
---

# Dependency Library

Explore dependency source code locally using ripgrep instead of web navigation.

## Library Structure

```
.claude/library/
└── <PackageName>/
    ├── NOTES.md          # What you've learned about this dependency
    └── repos/
        └── <repo-name>/  # Cloned git repo(s)
```

One directory per logical dependency. May contain multiple repos (main + docs, or split implementations).

## Workflow

### 1. Check if library entry exists

```bash
ls .claude/library/<PackageName>/
cat .claude/library/<PackageName>/NOTES.md
```

If it exists, read NOTES.md and proceed to step 4 (verify version).

If `.claude/library/` doesn't exist at all, check the repo's `.gitignore` and add `.claude/library` if not already present, then continue with step 2.

### 2. Create library entry

Find repo URL from nuget.org ("Source repository" link) or search GitHub.

```bash
mkdir -p .claude/library/<PackageName>/repos
cd .claude/library/<PackageName>/repos
git clone <repo-url>
```

### 3. Create NOTES.md

Discover how versioning works for this repo (tag format, branch naming, etc.) and record it:

```bash
cat > .claude/library/<PackageName>/NOTES.md << 'EOF'
# <PackageName>

## Repos
- <repo-name>: <why this repo>

## Versioning
How to find requested version: <e.g., grep PackageName in *.csproj>
How to check current checkout: <e.g., git describe --tags>
How to checkout a version: <e.g., git checkout v{version}>

## Locations of Interest
- <path>: <what's there>
EOF
```

### 4. Verify version (every time)

Follow the instructions in NOTES.md to:
1. Check the requested version (what the project uses)
2. Check the current checkout
3. Checkout the correct version if they don't match

### 5. Maintain Locations of Interest

**At initialization:** Look for where docs are stored in the repo (e.g., `docs/`, `Documentation/`, or XML docs only). Record this in Locations of Interest—docs are always useful and won't be discoverable later if not noted upfront. This is especially important for monorepos (`dotnet/runtime`, `dotnet/aspnetcore`) where relevant code may be deeply nested.

**During exploration:** When you find useful locations, add them to NOTES.md:
- Paths to public API definitions
- Reference docs for features you use heavily
- Paths to deeply nested submodules of interest

Format: `- <path>: <what's there>`

## Multi-Repo Dependencies

Some dependencies span multiple repos (e.g., core + sinks, main + docs). Clone all relevant repos under the same library entry:

```
.claude/library/Serilog/
├── NOTES.md
└── repos/
    ├── serilog/
    └── serilog-sinks-file/
```

Document in NOTES.md why each repo is present.
