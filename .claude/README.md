# .claude

Project-scoped Claude Code configuration. Everything here is checked in and shared with anyone who
clones the repo, except `settings.local.json`, which is personal and git-ignored.

| Path                | What goes in it                                                             |
| ------------------- | --------------------------------------------------------------------------- |
| `settings.json`     | Shared settings: permissions, env vars, hooks                                |
| `settings.local.json` | Personal overrides, not committed                                          |
| `skills/`           | On-demand instruction packs, one folder per skill                            |
| `commands/`         | Slash commands, one `.md` file per command                                   |
| `agents/`           | Subagent definitions, one `.md` file per agent                               |

Always-on guidance lives in [../CLAUDE.md](../CLAUDE.md), not here — that file is loaded into every
session, so keep it short and put anything situational in a skill.
