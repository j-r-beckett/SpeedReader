---
name: library
description: >-
  ALWAYS use this skill FIRST (before web search) when you need to answer
  questions about external software: CLI tools (act, docker, git, gh),
  libraries, frameworks, or any tool with a public repo. This includes:
  "does X have...", "can X do...", "how does X work?", "what does this
  flag/option do?", "does X support...", capability discovery, understanding
  behavior, finding usage examples, debugging integration issues. The source
  code is the authoritative answer - web search gives you secondhand
  information.
---

# Dependency Library

Maintain a local library of cloned dependency repos. Use ripgrep to explore.

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

Find the repo URL from the package registry (look for "Source repository" or similar link) or search GitHub.

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
How to find requested version: <where the project specifies this dependency's version>
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

**At initialization:** Look for where docs are stored in the repo (e.g., `docs/`, `Documentation/`, or inline docs only). Record this in Locations of Interest—docs are always useful and won't be discoverable later if not noted upfront. This is especially important for monorepos where relevant code may be deeply nested.

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
