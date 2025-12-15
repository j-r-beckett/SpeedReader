#!/usr/bin/env -S uv run --script
# /// script
# requires-python = ">=3.11"
# dependencies = ["click", "pyyaml"]
# ///

import re
import zipfile
from pathlib import Path

import click
import yaml


ALLOWED_PROPERTIES = {'name', 'description', 'license', 'allowed-tools', 'metadata'}


def validate_skill(skill_path: Path) -> tuple[bool, str]:
    """Basic validation of a skill."""
    skill_md = skill_path / 'SKILL.md'
    if not skill_md.exists():
        return False, "SKILL.md not found"

    content = skill_md.read_text()
    if not content.startswith('---'):
        return False, "No YAML frontmatter found"

    match = re.match(r'^---\n(.*?)\n---', content, re.DOTALL)
    if not match:
        return False, "Invalid frontmatter format"

    frontmatter_text = match.group(1)

    try:
        frontmatter = yaml.safe_load(frontmatter_text)
        if not isinstance(frontmatter, dict):
            return False, "Frontmatter must be a YAML dictionary"
    except yaml.YAMLError as e:
        return False, f"Invalid YAML in frontmatter: {e}"

    unexpected_keys = set(frontmatter.keys()) - ALLOWED_PROPERTIES
    if unexpected_keys:
        return False, (
            f"Unexpected key(s) in SKILL.md frontmatter: {', '.join(sorted(unexpected_keys))}. "
            f"Allowed properties are: {', '.join(sorted(ALLOWED_PROPERTIES))}"
        )

    if 'name' not in frontmatter:
        return False, "Missing 'name' in frontmatter"
    if 'description' not in frontmatter:
        return False, "Missing 'description' in frontmatter"

    name = frontmatter.get('name', '')
    if not isinstance(name, str):
        return False, f"Name must be a string, got {type(name).__name__}"
    name = name.strip()
    if name:
        if not re.match(r'^[a-z0-9-]+$', name):
            return False, f"Name '{name}' should be hyphen-case (lowercase letters, digits, and hyphens only)"
        if name.startswith('-') or name.endswith('-') or '--' in name:
            return False, f"Name '{name}' cannot start/end with hyphen or contain consecutive hyphens"
        if len(name) > 64:
            return False, f"Name is too long ({len(name)} characters). Maximum is 64 characters."

    description = frontmatter.get('description', '')
    if not isinstance(description, str):
        return False, f"Description must be a string, got {type(description).__name__}"
    description = description.strip()
    if description:
        if '<' in description or '>' in description:
            return False, "Description cannot contain angle brackets (< or >)"
        if len(description) > 1024:
            return False, f"Description is too long ({len(description)} characters). Maximum is 1024 characters."

    return True, "Skill is valid!"


def package_skill(skill_path: Path, output_dir: Path | None = None) -> Path | None:
    """Package a skill folder into a .skill file."""
    skill_path = skill_path.resolve()

    if not skill_path.exists():
        click.echo(f"Error: Skill folder not found: {skill_path}", err=True)
        return None

    if not skill_path.is_dir():
        click.echo(f"Error: Path is not a directory: {skill_path}", err=True)
        return None

    skill_md = skill_path / "SKILL.md"
    if not skill_md.exists():
        click.echo(f"Error: SKILL.md not found in {skill_path}", err=True)
        return None

    click.echo("Validating skill...")
    valid, message = validate_skill(skill_path)
    if not valid:
        click.echo(f"Validation failed: {message}", err=True)
        click.echo("   Please fix the validation errors before packaging.", err=True)
        return None
    click.echo(f"{message}\n")

    skill_name = skill_path.name
    if output_dir:
        output_path = output_dir.resolve()
        output_path.mkdir(parents=True, exist_ok=True)
    else:
        output_path = Path.cwd()

    skill_filename = output_path / f"{skill_name}.skill"

    try:
        with zipfile.ZipFile(skill_filename, 'w', zipfile.ZIP_DEFLATED) as zipf:
            for file_path in skill_path.rglob('*'):
                if file_path.is_file():
                    arcname = file_path.relative_to(skill_path.parent)
                    zipf.write(file_path, arcname)
                    click.echo(f"  Added: {arcname}")

        click.echo(f"\nSuccessfully packaged skill to: {skill_filename}")
        return skill_filename

    except Exception as e:
        click.echo(f"Error creating .skill file: {e}", err=True)
        return None


@click.command()
@click.argument('skill_folder', type=click.Path(exists=True, path_type=Path))
@click.option('--output', type=click.Path(path_type=Path), help='Output directory for the .skill file')
def main(skill_folder: Path, output: Path | None):
    """Package a skill folder into a distributable .skill file."""
    click.echo(f"Packaging skill: {skill_folder}")
    if output:
        click.echo(f"   Output directory: {output}")
    click.echo()

    result = package_skill(skill_folder, output)
    raise SystemExit(0 if result else 1)


if __name__ == "__main__":
    main()
