"""Validate local README links/images and translation structure (Python 3)."""
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
LANGUAGES = {"en": "English", "zh-TW": "繁體中文", "zh-CN": "简体中文", "ja": "日本語", "ko": "한국어", "es": "Español"}


class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.targets = []

    def handle_starttag(self, tag, attrs):
        for key, value in attrs:
            if key in ("href", "src") and value:
                self.targets.append(value)


def inspect(path):
    text = re.sub(r"^```.*?^```\s*$", "", path.read_text(encoding="utf-8"), flags=re.M | re.S)
    parser = Links()
    parser.feed(text)
    parser.targets.extend(re.findall(r"!?\[[^\]]*\]\(([^\s)]+)\)", text))
    errors = []
    for value in parser.targets:
        url = urlsplit(value)
        if url.scheme or url.netloc:
            continue
        target = (path.parent / unquote(url.path)).resolve() if url.path else path
        if not target.is_relative_to(ROOT) or not target.exists():
            errors.append(f"Missing/outside repository: {value}")
            continue
        if url.fragment and target.suffix == ".md":
            headings = re.findall(r"^#{1,6}\s+(.+)$", target.read_text(encoding="utf-8"), re.M)
            slugs = [re.sub(r"[^\w -]", "", heading.lower()).replace(" ", "-") for heading in headings]
            if unquote(url.fragment) not in slugs:
                errors.append(f"Missing heading: {value}")
    return text, len(parser.targets), errors


def main():
    count = 0
    failed = False
    for lang, label in LANGUAGES.items():
        path = ROOT / ("README.md" if lang == "en" else f"docs/i18n/README.{lang}.md")
        if not path.exists():
            print(f"FAIL {path.relative_to(ROOT)}: missing translation")
            failed = True
            continue
        text, links, errors = inspect(path)
        count += links
        if len(re.findall(r"^## ", text, re.M)) != 8:
            errors.append("Expected the same eight sections as the English README")
        if f"<strong>{label}</strong>" not in text or text.count("<strong>") != 1:
            errors.append("Language switcher must mark only its own language in bold")
        for other_lang, other_label in LANGUAGES.items():
            if other_lang != lang:
                destination = "README.md" if other_lang == "en" else f"README.{other_lang}.md"
                if not re.search(r'href="[^"]*' + re.escape(destination) + r'">' + re.escape(other_label) + "</a>", text):
                    errors.append(f"Missing language switch: {other_label}")
        if "width=\"49%\"" not in text or "<details>" not in text:
            errors.append("Expected screenshot pair and expandable extra screenshots")
        print(f"{'FAIL' if errors else 'OK'} {path.relative_to(ROOT)} ({links} paths/links)")
        for error in errors:
            print(f"  {error}")
        failed |= bool(errors)
    for name in ("DEVELOPMENT.md", "README-MAINTENANCE.md"):
        path = ROOT / "docs" / name
        _, links, errors = inspect(path)
        count += links
        print(f"{'FAIL' if errors else 'OK'} {path.relative_to(ROOT)} ({links} paths/links)")
        for error in errors:
            print(f"  {error}")
        failed |= bool(errors)
    print(f"Checked {count} references. Remote HTTP links are not fetched by this check.")
    return int(failed)


if __name__ == "__main__":
    sys.exit(main())
