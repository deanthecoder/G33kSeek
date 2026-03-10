# G33kSeek

**G33kSeek** is a fast, keyboard‑driven launcher inspired by macOS Spotlight.  
It provides instant access to applications, files, calculations, utilities, and web search from a single minimal interface.

The goal is to create a **lightweight, cross‑platform launcher** powered by **Avalonia** that works on Windows, macOS, and Linux.

Users trigger G33kSeek with a global shortcut, type what they need, and results appear immediately.

---

## Goals

G33kSeek should be:

- **Fast** – results should appear instantly as the user types.
- **Keyboard‑first** – no mouse required.
- **Minimal UI** – small centered window, no clutter.
- **Extensible** – functionality implemented via pluggable providers.
- **Cross‑platform** – Windows, macOS, Linux using Avalonia.
- **Developer‑friendly** – supports quick utilities and leaves room for future AI integration.

---

## Core Behaviour

Press a **global hotkey** to open the launcher.

A small centered window appears.

The user types a query and results update in real time.

Press **Enter** to execute the selected result.

Press **Esc** to close the launcher.

### Current Prototype

The initial desktop shell now exists and includes:

- a dark Fluent Avalonia launcher window
- a centered Spotlight-like text entry surface
- a system tray icon with show / hide / exit actions
- a first-cut global hotkey using **SharpHook** (`Alt+Space` on Windows, `Ctrl+Space` elsewhere)
- no-prefix app search on macOS and Windows
- no-prefix file search backed by a cached Documents index
- direct URL opening for `http://`, `https://`, `www.`, and bare domains
- quick no-prefix utility values like `now`, `date`, and `time`
- no-prefix unit conversions for data sizes, temperature, weight, length, and decimal/hex/binary
- `?` help, filtered help, and quoted web search via Google
- `>` commands for common folders, adding file-search folders, GUID/IP utilities, log viewing, and basic system actions

This is intentionally just the starting point before real providers and indexing are connected.

---

## Query Modes

The first characters of the query determine the **mode**.

| Prefix | Mode | Example | Behaviour |
|------|------|------|------|
| *(none)* | App / File search | `rider` | Search installed apps and indexed files, or open direct URLs |
| `=` | Calculator | `=2+2` | Evaluate math expressions |
| `?` | Help / examples / web search | `?` | Show help, filter help topics, or use quoted text for web search |
| `??` | File content search | `??TODO` | Planned placeholder for future content search |
| `@` | AI prompt | `@summarise this text` | Planned placeholder for future AI prompting |
| `>` | Commands | `>shutdown` | Execute built‑in commands |

---

## Example Queries

```
rider
https://avaloniaui.net
=4*8
10mb in bytes
100c in f
68kg in stone
180cm in ft
255 in hex
255 in binary
?
?"avalonia docs"
??class Program
@explain this regex
>restart explorer
```

---

## Architecture

G33kSeek should use a **provider‑based architecture**.

Each query mode is implemented by a provider responsible for generating results.

```
QueryEngine
   ├── AppSearchProvider
   ├── FileSearchProvider
   ├── CalculatorProvider
   ├── WebSearchProvider
   ├── ContentSearchProvider
   ├── AiPromptProvider
   └── CommandProvider
```

Providers should be **loosely coupled** and easy to add.

Each provider receives the user query and returns a list of results.

---

## Core Components

### Launcher Window

Avalonia window containing:

- Search textbox
- Result list
- Optional icons
- Keyboard navigation

The window should:

- appear centered
- stay small and minimal
- disappear after executing a result

---

### Global Hotkey

Platform‑specific implementations may be required, that do not clash with other applications.

Pressing the hotkey should:

- show the launcher
- focus the input box
- clear previous query

---

### Tray Icon

A tray icon should exist for:

- enabling/disabling the launcher
- settings
- exit

Avalonia supports cross‑platform tray icons.

---

## Search System

File/app search should be **incremental and fast**.

Possible strategies:

- simple directory indexing
- background indexer
- OS integration where possible

Initial implementation can be simple and evolve later.

Today the first real no-prefix slice is application search on macOS and Windows:

- cached from `/Applications`, `/System/Applications`, and `~/Applications`
- matched by `.app` bundle name
- refreshed from a fast shallow scan, including one nested folder level for built-in utilities
- cached from the Windows Start Menu `Programs` roots
- matched by shortcut file name
- refreshed from a lightweight shortcut scan

Today no-prefix file search also includes:

- a background-cached index of the user's Documents folder
- default excludes for `.git`, `node_modules`, `bin`, `obj`, `.idea`, `.vs`, and `packages`
- persisted cache data so searches stay fast between launches

Current no-prefix utility queries also include:

- `now`, `date`, `time`
- data-size conversion such as `10mb in bytes`
- decimal/hex/binary conversion such as `255 in hex`, `255 in binary`, `0xff in binary`, or `0b11111111 in decimal`
- temperature conversion such as `100c in f`
- weight conversion such as `68kg in stone`
- length conversion such as `180cm in ft`

Today the first real `>` commands are:

- `>addfolder`
- `>desktop`
- `>documents`
- `>downloads`
- `>guid`
- `>home`
- `>ip`
- `>lock`
- `>log`
- `>refresh`
- `>shutdown`
- `>restart`
- `>logoff`

---

## AI Integration

The `??` and `@` prefixes are currently reserved and surfaced as placeholders, but they are not implemented yet.

Planned future work includes file content search and AI prompting. AI prompting would likely start with **ChatGPT via API**.

Example query:

```
@explain this C# code
```

Future AI support may include:

- local LLMs
- Ollama
- other AI providers

AI should still be implemented as a **provider** when it arrives.

---

## Command System

Commands allow simple system actions.

Examples:

```
>shutdown
>restart
>open downloads
```

Commands should be easy to register and discover.

---

## UI Principles

The interface should feel:

- fast
- responsive
- minimal
- distraction‑free

Avoid complex UI.

Keyboard interaction should feel natural.

---

## Future Ideas

Potential enhancements:

- clipboard history
- emoji picker
- unit conversions
- colour conversions
- Git commands
- quick notes
- plugin system

---

## Tech Stack

- **.NET**
- **C#**
- **Avalonia UI**
- **Cross‑platform**

---

## Project Philosophy

G33kSeek should remain:

- simple
- fast
- hackable
- extensible

The goal is to build a launcher that developers can easily extend and customize.
