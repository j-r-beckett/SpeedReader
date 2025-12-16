---
name: skill-creator
description: Create, improve, and troubleshoot Claude Code agent skills. Use when asked to make a new skill, write a SKILL.md file, improve skill discoverability, debug why a skill isn't activating, answer questions about skills, or review skill best practices.
---

# Skill Creator

Create well-structured Claude Code skills that are discoverable and effective.

## Skill Setup

```
.claude/skills/my-skill-name/
├── SKILL.md                    # Required: main definition file
├── scripts/                    # Optional: automation scripts
│   └── build.py
├── assets/                     # Optional: templates, samples, data
│   └── template.html
└── references/                 # Optional: reference documentation
    └── api-guide.md
```

Use the template at [assets/SKILL.template.md](assets/SKILL.template.md) as a starting SKILL.md file.

```bash
mkdir -p .claude/skills/my-skill-name
cp .claude/skills/skill-creator/assets/SKILL.template.md .claude/skills/my-skill-name/SKILL.md
```

## Writing Discoverable Descriptions

The only things Claude sees before using a skill are the name and description in the frontmatter. Optimizing the description is key to making it discoverable. In general, Claude is too conservative when deciding whether to use a skill. Maximize the likelihood of Claude using the skill by writing a description that captures the full scope of both **what** the skill does and **when** the skill should be used (and make sure to include *both*!). If Claude loads a skill then decides not to use it, no big deal. If Claude doesn't load a skill and spends time spiralling on an already solved task, the session is ruined. High skill discoverability is *extremely important*.

Don't rely on users saying magic words. Think about what *situations* call for this skill, including ones where Claude should decide to use it on its own, then write a description that captures those scenarios.

Note that the description must be 1024 characters or less, and it must be on a single line (Claude Code does not support multiline YAML).

Here are some good examples of discoverable frontmatters:

```yaml
---
name: speedreader-web
description: Handles SpeedReader server lifecycle (build, startup, shutdown) and web page rebuild/refresh. Use when you need to verify a web page works, view it, test UI interactions, or see how a page behaves. Also covers development tasks: creating, modifying, styling, reviewing.
---
```

```yaml
name: pdf
description: Toolkit for viewing, reading, extracting text, creating, editing, converting, and transforming PDFs. Use whenever you need to work with or interact with PDF files.
```

## Patterns

### Toolbox

The toolbox pattern is implemented by skills that contain non-trivial automation scripts (AKA "tools") and teach Claude how to use them. SKILL.md provides context and information about how to properly use the tools; the tools themselves are python scripts that encapsulate complexity and run commands without clogging up Claude's context window or making silly mistakes. This helps keeps Claude focused on *when* and *how* to invoke tools rather than repeatedly reimplementing their logic, which over the course of a long session substantially increases reliability.

For scripting, always prefer python. Run shell commands with `subprocess.run`. Don't use shell commands for operations that could be performed in python (file manipulation, hashing, regex, etc). When done properly, these scripts are trivially portable across platforms.

Always use `uv` with inline dependencies. In the SKILL.md text, make it clear that the scripts must be invoked with `uv`. If you're not *extremely clear* that Claude should use `uv`, Claude **will** try to run the scripts with `python3` and then be confused when it doesn't work.

Design the API of scripts with care. Always provide `--help`. Avoid exposing unneeded configuration parameters. Scripts should create clean abstractions for Claude to consume. As Claude works, contents of SKILL.md will fade but the script's API will remain, so make sure it's good.

### Knowledge Injection

To summarize: Neo learns Kung Fu. 

A knowledge injection is when you give Claude a batch of valuable knowledge that it didn't have before that lets it do new things. Examples:

1. Teach Claude how to use a command line tool or python package (for ad-hoc scripting)
2. Give Claude a knowledge dump to make it instantly an expert on a topic
3. Guide Claude through a complex, nuanced workflow

Use `references/` to store documents. If appropriate, use progressive disclosure (e.g. "Depending on the platform, read docs/gcp.md, docs/azure.md, or docs/aws.md").

## Principles

### Valuable Knowledge

A common pitfall is for Claude to create skills and fill them up with generated information about how to complete a task. The problem with this is that the generated content is all content that's already inside Claude's probability space. Claude is effectively telling itself information that it *already knows*!

Instead, Claude should strive to document in SKILL.md only information that:

1. Is outside of Claude's training data (information that Claude had to learn through research, experimentation, or experience)
2. Is context specific (something that Claude knows **now**, but won't know **later** after its context window is cleared)
3. Aligns future Claude with current Claude (information that will guide future Claude in acting how we want it to act)

Claude should also avoid recording **derived data**. Lead a horse to water, don't teach it how to drink. If there's an easily available source that will tell Claude all it needs to know, point Claude at that source. If the information Claude needs can be trivially derived from information Claude already knows or has already been provided, don't provide the derived data.

Before finalizing a skill, revisit this section. Often cruft will creep in in the course of writing the skill. A strong editing pass at the end is recommended.

### Automation

Over the course of a long session, Claude **will** screw up even simple tasks. Typos, forgotten flags, wrong directories, skipped steps. By pushing tasks into automation, we substantially improve long-term reliability. The goal is to reduce the surface area for Claude to make mistakes.

Good automation is:

1. **Single-touch** - Fold setup and teardown into the tool itself. If we can perform a step in python instead of making Claude do it manually, do it in python. Always. One command should do the whole job.

2. **Clean primitives** - Expose composable operations that can be combined. Avoid tools that do too much or have complex interdependencies. The goal is to expose a simple API to Claude that frees up Claude's attention for higher-value activities.

3. **Repo-specific** - The most powerful automation is usually repo-specific because that's where the low-hanging fruit is. Generic tools already exist; the unique workflows and pain points in your repo are where automation pays off most. Teaching Claude how to use a generic tool in your repo is high-leverage.

### Qualifications

Claude can't create a skill if Claude doesn't already know how to do the skill. Before creating a skill, Claude should experiment with the workflows itself. Research CLIs and libraries, download them, try things out, see what's possible, think of things to try and see if they work. Then write the skill using that research and experience. Make sure not to include speculation!

This is related to Valuable Knowledge. Skills must add value. The best way to do that is to invest time and effort up front in creating the skill so that when Claude loads it later the skill is a value add rather than a drag on the context window.
