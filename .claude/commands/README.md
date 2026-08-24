# Slash commands

One markdown file per command — `build.md` becomes `/build`. The file body is the prompt.

```markdown
---
description: Build the app and report only the errors
allowed-tools: Bash(dotnet build:*)
---

Build with `dotnet build src/InternalsViewer.UI.App -p:Platform=x64 -p:EnableMsixTooling=false`
and summarise any errors, ignoring the known environmental MSIX/PDB failures.
```

`$ARGUMENTS` (or `$1`, `$2`) interpolates whatever the user typed after the command name.
Subdirectories namespace the command: `commands/db/reset.md` becomes `/db:reset`.
