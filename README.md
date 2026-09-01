<img src="docsite/img/logo.svg" width="72" alt="Canary logo">

# Canary
*Markdown In -> Website Out.*
Pre-rendered HTML on cold load, with a small TypeScript runtime to provide in-site navigation. The C#-based CLI generates static HTML files that are crawlable and social-link preview-friendly. Widgets allow you to add site features with fenced YAML. Toolchains can modify your markdown before rendering, keeping your source files clean.

## Status

Alpha is live, running on both https://consoland.net and https://canary.consoland.net.

Windows MSI for v0.2.0 is available in [Releases](https://github.com/haberling/canary/releases).

## Requires

- Windows: MSI (no .NET SDK)
- Build from source (Linux / macOS): .NET 10 SDK

## Quick start

Download the [Windows MSI](https://github.com/haberling/canary/releases/download/v0.2.0/CanaryInstaller.msi). It is self-contained (no .NET runtime). Open a new terminal after install.

```text
canary init my-site
canary serve --config my-site/canary.jsonc
```

Open the printed localhost URL. Edit `my-site/content/index.md` and save; the server rebuilds.

From source (Linux, macOS, or Windows without the MSI). This needs the .NET 10 SDK:

```text
git clone https://github.com/haberling/canary
cd canary
dotnet run --project src/Canary -- init my-site
dotnet run --project src/Canary -- serve --config my-site/canary.jsonc
```

## What it is

* Pre-rendered HTML per route (crawlers work with no JavaScript)
* *Warm* navigation uses the client router on in-site clicks
* Widgets are added to pages with explicit fenced YAML
* Toolchain for programmatically modifying the markdown before rendering

## Minimal example

**canary.jsonc**

```jsonc
{
  "site": { "name": "Example", "baseUrl": "https://example.com" },
  "content": { "root": "content" },
  "output": { "dir": "docs" },
  "renderMode": "hybrid"
}
```

**content/index.md**

```markdown
# Hello

This page is markdown. The build emits real HTML for this route.
```

A slideshow widget is a fenced YAML block:

```slideshow
title: Optional Title
slides:
  - src: "https://example.com/shot1.png"
    caption: "Optional caption"
  - src: !url "content/games/images/shot2.png"
```

## Docs

Guides and reference: https://canary.consoland.net

## License

[MIT](LICENSE)
