# Skills

One folder per skill, each containing a `SKILL.md`:

```
.claude/skills/
  columnstore-segments/
    SKILL.md
    reference.md        # optional supporting files, loaded only when the skill asks for them
```

`SKILL.md` starts with frontmatter:

```markdown
---
name: columnstore-segments
description: Decoding columnstore segment and dictionary blobs. Use when working on segment headers, RLE entries, bitpack arrays, or the segment viewer UI.
---

# Columnstore segments

Instructions go here.
```

The `description` is the only part loaded up front — it decides whether the skill gets pulled in, so
write it as "what this covers + when to use it". The body is loaded on demand, so it can be long.

Good candidates in this repo: deep formats (segment blobs, backup file container, page header flag bits),
multi-step workflows (adding an iterator, adding a page parser), and anything that would otherwise bloat
CLAUDE.md.
