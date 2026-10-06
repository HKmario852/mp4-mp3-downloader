"""Render via GitHub's Markdown API, then write a local desktop preview."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "artifacts" / "readme-preview"


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(
        ["gh", "api", "-X", "POST", "markdown", "-f", "mode=markdown", "-F", "text=@README.md"],
        cwd=ROOT, capture_output=True, check=True,
    )
    markdown = result.stdout.decode("utf-8")
    (OUTPUT / "github-markdown.html").write_text(markdown, encoding="utf-8")
    # The API supplies the content; this CSS approximates GitHub's desktop document wrapper.
    css = """
    *{box-sizing:border-box} body{margin:0;background:#fff;color:#1f2328;
    font:16px/1.5 -apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}
    article{max-width:1012px;margin:24px auto;padding:32px;border:1px solid #d1d9e0;border-radius:6px}
    a{color:#0969da;text-decoration:none} a:hover{text-decoration:underline}
    p,ul,ol,table,pre,details{margin:0 0 16px} h1{font-size:32px} h2{font-size:24px}
    h1,h2{padding-bottom:.3em;border-bottom:1px solid #d1d9e0;margin:24px 0 16px;line-height:1.25}
    img{max-width:100%;vertical-align:middle} table{border-collapse:collapse;width:100%}
    th,td{padding:6px 13px;border:1px solid #d1d9e0} tr:nth-child(2n){background:#f6f8fa}
    code,pre{font-family:Consolas,monospace;font-size:85%} code{background:#eff1f3;padding:.2em .4em;border-radius:6px}
    pre{background:#f6f8fa;padding:16px;overflow:auto;border-radius:6px} pre code{padding:0;background:none}
    blockquote{margin:0 0 16px;padding:0 1em;border-left:4px solid #d1d9e0;color:#59636e}
    summary{cursor:pointer} .anchor{display:none}
    @media(max-width:1050px){article{margin:16px;padding:24px}}
    """
    html = '<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><base href="/"><title>Omni Downloader — README preview</title><style>' + css + '</style><article class="markdown-body">' + markdown + '</article></html>'
    (OUTPUT / "index.html").write_text(html, encoding="utf-8")
    print("Rendered README using gh api, mode=markdown: artifacts/readme-preview/index.html")


if __name__ == "__main__":
    main()
