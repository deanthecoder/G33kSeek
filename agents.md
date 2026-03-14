# Agents.md

## Summary

G33kSeek is a fast, cross-platform keyboard launcher built with Avalonia and C#.

The project already supports:

- no-prefix app, file, and folder search
- direct path opening
- calculator queries with copy-on-Enter
- help via `?`
- emoji lookup via `:`
- quoted web search via `?"search text"`
- built-in commands via `>`
- date, time, and conversion utilities

Keep changes simple, responsive, and keyboard-first.

---

## Key Guidance

- Prefer extending existing providers and services rather than inventing a new pattern.
- Reuse `DTC.Core` helpers wherever they fit.
- If a helper is generic and reusable, consider whether it belongs in `DTC.Core`.
- Prefer proven code from other DeanTheCoder repos before adding new local infrastructure.
- Keep the launcher feeling instant. Avoid UI-thread work, unnecessary allocations, and query-time disk access.
- Update unit tests for meaningful behaviour changes.
- If user-facing behaviour changes noticeably, update the README too.

---

## Coding Preferences

- Commit messages must start with `Feature:`, `Fix:`, or `Other:`, use sentence case, and end with a full stop.
- Prefer method names without underscores.
- Async methods should end with `Async` unless a framework contract prevents that.
- Reserve `m_` for fields only, not locals or parameters.
- Validate public method arguments appropriately.
- New public classes and public methods should usually have unit tests unless that would be disproportionate.
- Small incidental cleanups such as removing unused `using`s can be folded into nearby work.

---

## Project Structure

Prefer these folders:

- `Providers`
- `Services`
- `Views`
- `ViewModels`
- `Models`

General intent:

- `Providers` handle query routing and result generation.
- `Services` contain reusable search, indexing, execution, and infrastructure logic.
- `Views` and `ViewModels` keep the launcher UI and MVVM state clean.
- `Models` hold simple data structures and action/result types.

---

## Query Modes

Current query behaviour:

| Prefix | Mode | Example |
| --- | --- | --- |
| none | App, file, folder, URL, and utility search | `rider` |
| `=` | Calculator | `=2+2` |
| `:` | Emoji lookup and emoticons | `:smile`, `:)`, `:heart` |
| `?` | Help, help filtering, and quoted web search | `?`, `?calc`, `?"avalonia docs"` |
| `>` | Commands | `>guid` |

Notes:

- Bare `?` shows in-app help.
- `:` performs emoji lookup and also supports simple emoticons such as `:)` and `:(`.
- `?text` filters help topics.
- `?"text"` performs a web search.
- Direct URLs such as `https://...` and `www...` should open in the browser.

Reserved but not implemented:

- `??` content search
- `@` AI prompt mode

---

## Current UX Decisions

- No-prefix search should stay fast and cache-backed.
- App results should appear before file results.
- Single-value results should generally copy on Enter.
- File and app results may offer secondary actions such as reveal/copy path.
- The launcher hotkey is currently `Ctrl+Space`.
- Keep the UI minimal and responsive. Avoid heavy chrome or unnecessary interaction complexity.

---

## File Search Notes

- Default roots currently include common user folders such as Documents, Pictures, and Downloads.
- Windows also includes Public Documents.
- Default excludes include folders such as `.git`, `.hg`, `.svn`, `.idea`, `.vs`, `obj`, `node_modules`, and `packages`.
- File indexing should happen in the background, not during query execution.
- Cached file index data may be compressed when persisted.

---

## DTC.Core Usage

Prefer `FileInfo` and `DirectoryInfo` over raw strings where sensible.

Useful patterns:

- `string.ToDir()`
- `string.ToFile()`
- `dir.GetDir("a/b")`
- `dir.GetFile("x.txt")`
- `file.WriteAllText(...)`
- `file.OpenWithDefaultViewer()`
- `directory.Explore()`
- `file.Explore()`

---

## Documentation And Style

- Add class-level XML docs to reusable public types.
- Prefer readable code over clever code.
- Avoid unnecessary abstractions.
- Keep files focused.
- Prefer simple MVVM boundaries.

---

## Future Ideas

Possible later additions:

- clipboard history
- clipboard text helpers
- colour utilities
- git commands
- plugin system
- local AI model support

If a future feature makes the launcher feel heavy or slow, reconsider it.
