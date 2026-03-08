# G33kSeek

**G33kSeek** is a fast, keyboard‑driven launcher inspired by macOS Spotlight.  
It provides instant access to applications, files, calculations, web search, and AI prompts from a single minimal interface.

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
- **Developer‑friendly** – supports quick utilities and AI prompts.

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
- a first-cut global hotkey using **SharpHook** (`Ctrl+Space`)

This is intentionally just the starting point before real providers and indexing are connected.

---

## Query Modes

The first characters of the query determine the **mode**.

| Prefix | Mode | Example | Behaviour |
|------|------|------|------|
| *(none)* | App / File search | `rider` | Search installed apps and indexed files, or open direct URLs |
| `=` | Calculator | `=2+2` | Evaluate math expressions |
| `?` | Help / examples | `?` | Show available modes and examples |
| `??` | File content search | `??TODO` | Search file contents |
| `@` | AI prompt | `@summarise this text` | Send prompt to an AI provider |
| `>` | Commands | `>shutdown` | Execute built‑in commands |

---

## Example Queries

```
rider
https://avaloniaui.net
=4*8
?
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

- cached from `/Applications` and `~/Applications`
- matched by `.app` bundle name
- refreshed from a fast top-level scan
- cached from the Windows Start Menu `Programs` roots
- matched by shortcut file name
- refreshed from a lightweight shortcut scan

Today the first real `>` commands are:

- `>guid`
- `>ip`
- `>shutdown`
- `>restart`
- `>logoff`

---

## AI Integration

AI prompting should initially support **ChatGPT via API**.

Example query:

```
@explain this C# code
```

Future support may include:

- local LLMs
- Ollama
- other AI providers

AI should be implemented as a **provider**.

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
