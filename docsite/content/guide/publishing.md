# Publishing

For a git-served static host like GitHub Pages, `canary build` writing to `output.dir` (`docs/` by default) *is* the entire deploy story — you commit and push that folder yourself, no special Canary step needed.

If you'd rather Canary run that last step for you, set a top-level `publish` field in `canary.jsonc`: a single arbitrary shell command, not a deploy-target abstraction. Canary doesn't know or want to know how your site is actually hosted — it doesn't understand git, FTP, or rsync, and never will. It just runs whatever you tell it to.

```
{ "publish": "git add docs && git commit -m \"Publish\" && git push" }
```

`canary publish` always builds first — publishing stale output would be worse than refusing to publish at all — then runs the configured command. If `publish` isn't set, it fails with a clear message rather than doing nothing silently.

## `CANARY_OUTPUT_DIR`

The one environment variable a publish command gets: the absolute path to `output.dir`, so you can reference the real build output location without hardcoding or duplicating a directory name that's already declared elsewhere in `canary.jsonc`. There's no per-page context here (unlike a toolchain tool) — publishing is a whole-site action, not a per-page one.

## Live output

A publish command's output streams to your terminal live as it runs, not buffered and dumped after the fact — the whole point of something like `git push`'s progress output is watching it happen.

## The bare-filename gotcha

**Always write a local publish script with an explicit path** (`tools/publish.cmd`, `./publish.cmd`), never a bare `publish.cmd` sitting in the site root. On Windows machines with the `NoDefaultCurrentDirectoryInExePath` security setting enabled — a real, sometimes IT-policy-enforced setting, not unusual dev-environment weirdness — a bare filename with no path separator silently fails to resolve. A path with a separator bypasses that lookup entirely and is unaffected. This applies to toolchain tool commands too, not just `publish`.
