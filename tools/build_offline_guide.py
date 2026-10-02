"""Render the guide's supported Markdown subset into standalone offline pages."""
from pathlib import Path
import argparse
import html
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
GUIDE = ROOT / 'docs/user-guide'


def inline(text: str) -> str:
    text = html.escape(text)
    def link(match):
        image, label, target = match.groups()
        if not target.startswith(('http://', 'https://')) and target.endswith('.md'):
            target = target[:-3] + '.html'
        if not target.startswith(('http://', 'https://', 'images/')) and not re.fullmatch(r'[A-Za-z0-9-]+\.html', target):
            raise ValueError('Unsupported guide link')
        if image:
            return f'<img alt="{label}" src="{target}">'
        return f'<a href="{target}">{label}</a>'
    text = re.sub(r'(!?)\[([^\]]*)\]\(([^\s)]+)\)', link, text)
    text = re.sub(r'`([^`]+)`', r'<code>\1</code>', text)
    return re.sub(r'\*\*([^*]+)\*\*', r'<strong>\1</strong>', text)


def render(text: str) -> str:
    result = []
    paragraph = []
    listing = None
    def flush():
        if paragraph:
            result.append('<p>' + inline(' '.join(paragraph)) + '</p>')
            paragraph.clear()
    def close_list():
        nonlocal listing
        if listing:
            result.append(f'</{listing}>'); listing = None
    # Join wrapped blockquote lines into one notice rather than several boxes.
    lines = []
    for line in text.splitlines():
        if line.startswith('>') and lines and lines[-1].startswith('>'):
            lines[-1] += ' ' + line.lstrip('> ')
        else:
            lines.append(line)
    for line in lines:
        heading = re.match(r'^(#{1,6}) (.+)$', line)
        item = re.match(r'^(?:([-]) |(\d+)\. )(.+)$', line)
        if not line.strip():
            flush(); close_list()
        elif heading:
            flush(); close_list()
            level = len(heading[1]); result.append(f'<h{level}>{inline(heading[2])}</h{level}>')
        elif item:
            flush()
            kind = 'ul' if item[1] else 'ol'
            if listing != kind:
                close_list(); result.append(f'<{kind}>'); listing = kind
            result.append('<li>' + inline(item[3]) + '</li>')
        elif listing and line.startswith('  '):
            # Continuation belongs to the previous list item.
            result[-1] = result[-1][:-5] + ' ' + inline(line.strip()) + '</li>'
        elif line.startswith('>'):
            flush(); close_list(); result.append('<blockquote>' + inline(line.lstrip('> ')) + '</blockquote>')
        else:
            close_list(); paragraph.append(line.strip())
    flush(); close_list()
    return '\n'.join(result)


def build(destination: Path):
    destination = destination.resolve()
    output_root = (ROOT / 'release').resolve()
    if not destination.is_relative_to(output_root) or destination == output_root:
        raise ValueError('Guide output must be a child of the repository release directory')
    if destination.exists():
        raise ValueError('Choose a fresh guide output directory')
    pages = sorted(GUIDE.glob('*.md'))
    images = sorted((GUIDE / 'images').glob('*'))
    for source in pages + images:
        if source.is_symlink() or not source.is_file():
            raise ValueError('Guide sources must be ordinary files')
        subprocess.run(['sonar', 'analyze', 'secrets', str(source)], check=True)
    navigation = ' · '.join(f'<a href="{page.stem}.html">{html.escape(page.stem.replace("-", " "))}</a>' for page in pages)
    rendered = {page.stem: render(page.read_text(encoding='utf-8')) for page in pages}
    destination.mkdir(parents=True)
    shutil.copytree(GUIDE / 'images', destination / 'images')
    for name, body in rendered.items():
        document = '<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">'
        document += f'<title>TDSBLive — {html.escape(name)}</title>'
        document += '<style>body{font:18px/1.6 system-ui;margin:auto;padding:24px;max-width:960px;color:#17202a;background:white}nav{font-size:15px}img{max-width:100%;height:auto}code{overflow-wrap:anywhere}blockquote{border-left:4px solid #758399;padding-left:16px}a{color:#005cab}li{margin-bottom:8px}</style>'
        document += f'<nav aria-label="User guide">{navigation}</nav><main>{body}</main></html>'
        (destination / (name + '.html')).write_text(document, encoding='utf-8')
    print(f'Built {len(pages)} offline guide pages')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(); parser.add_argument('destination', type=Path)
    build(parser.parse_args().destination)
