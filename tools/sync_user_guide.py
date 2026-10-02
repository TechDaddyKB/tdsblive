"""Validate and copy the canonical user guide into an existing wiki checkout.

This prepares files only. Review the wiki diff and commit/push separately.
Every source file is secrets-scanned before reading or copying.
"""
from __future__ import annotations

import argparse
from pathlib import Path
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
GUIDE = ROOT / "docs" / "user-guide"
LINK = re.compile(r"(!?)\[([^\]]*)\]\(([^\s)]+)\)")


def prepare(destination: Path) -> None:
    destination = destination.resolve()
    if not (destination / ".git").is_dir():
        raise ValueError("Choose an existing wiki Git checkout.")
    pages = sorted(GUIDE.glob("*.md"))
    images = sorted((GUIDE / "images").glob("*")) if (GUIDE / "images").exists() else []
    sources = pages + images
    for source in sources:
        if source.is_symlink() or not source.is_file():
            raise ValueError("Guide sources must be ordinary files.")
        if source in images and source.suffix.lower() not in {".png", ".jpg", ".jpeg", ".webp"}:
            raise ValueError("Only documented screenshot formats belong in the image directory.")
        subprocess.run(["sonar", "analyze", "secrets", str(source)], check=True)
    rendered: dict[str, str] = {}
    for page in pages:
        text = page.read_text(encoding="utf-8")

        def rewrite(match: re.Match[str]) -> str:
            image, label, target = match.groups()
            if target.startswith(("https://", "http://", "#")):
                return match.group(0)
            local, separator, anchor = target.partition("#")
            linked = (page.parent / local).resolve()
            if not linked.is_relative_to(GUIDE.resolve()) or linked not in sources:
                raise ValueError(f"Unresolved guide link in {page.name}: {target}")
            if image:
                if linked not in images:
                    raise ValueError("Image links must point to the guide screenshot directory.")
                converted = f"images/{linked.name}"
            else:
                if linked not in pages:
                    raise ValueError("Page links must point to guide Markdown pages.")
                converted = linked.stem
            if separator:
                converted += "#" + anchor
            return f"{image}[{label}]({converted})"

        rendered[page.name] = LINK.sub(rewrite, text)
    # Validate everything before mutating the checkout. Preserve unrelated wiki pages.
    for name, text in rendered.items():
        target = destination / name
        if target.is_symlink():
            raise ValueError("Wiki output cannot replace a symbolic link.")
        target.write_text(text, encoding="utf-8", newline="\n")
    if images:
        image_directory = destination / "images"
        if image_directory.is_symlink():
            raise ValueError("Wiki image directory cannot be a symbolic link.")
        image_directory.mkdir(exist_ok=True)
        for image in images:
            target = image_directory / image.name
            if target.is_symlink():
                raise ValueError("Wiki image output cannot replace a symbolic link.")
            shutil.copyfile(image, target)
    sidebar = "## User guide\n\n" + "\n".join(
        f"- [{page.stem.replace('-', ' ')}]({page.stem})" for page in pages
    ) + "\n"
    sidebar_path = destination / "_Sidebar.md"
    if sidebar_path.is_symlink():
        raise ValueError("Wiki sidebar cannot replace a symbolic link.")
    sidebar_path.write_text(sidebar, encoding="utf-8", newline="\n")
    print(f"Prepared {len(pages)} guide pages and {len(images)} screenshots; review before publishing.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("wiki_checkout", type=Path)
    prepare(parser.parse_args().wiki_checkout)
