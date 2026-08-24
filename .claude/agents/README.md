# Subagents

One markdown file per agent. Each runs in its own context window with its own tool allowance, so use
them for work that would otherwise flood the main conversation — broad searches, test-failure triage,
reviewing a large diff.

```markdown
---
name: page-format-explorer
description: Traces how a given page type is parsed, from reader through parser to view model
tools: Read, Grep, Glob
model: sonnet
---

You are exploring the Internals page parsing pipeline. Report the parse chain and the file:line of
each hop. Do not modify files.
```

Omit `tools` to inherit everything. Omit `model` to inherit the session's model.
