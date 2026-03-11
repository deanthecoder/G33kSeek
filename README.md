[![Twitter URL](https://img.shields.io/twitter/url/https/twitter.com/deanthecoder.svg?style=social&label=Follow%20%40deanthecoder)](https://twitter.com/deanthecoder)

# G33kSeek
**G33kSeek** is a cross-platform lightweight keyboard launcher inspired by macOS Spotlight. Type a query, launch an app, open a file or folder, run a command, do a quick calculation, or copy a useful value without leaving the keyboard.

![Main window placeholder](img/G33kSeek.png)

## What it can do
- Search installed applications on macOS and Windows.
- Search indexed files and folders from common user locations.
- Open direct file and folder paths without needing them in the index first.
- Evaluate calculator expressions with trig support.
- Open URLs directly, or run quoted web searches from `?`.
- Run built-in commands such as `>desktop`, `>guid`, `>log`, `>refresh`, `>shutdown`, and `>lock`.
- Return quick utility values such as date, time, and unit/base conversions.

## Quick examples
```text
rider
=sin(pi / 2)
10mb in bytes
255 in binary
?"avalonia docs"
>downloads
/Users/dean/Documents/Source/Repos/ReviewG33k/README.md
```

## Query modes
| Prefix | Mode | Example |
| --- | --- | --- |
| none | App, file, folder, URL, and utility search | `rider` |
| `=` | Calculator | `=2+2` |
| `?` | Help, help filtering, and quoted web search | `?"avalonia docs"` |
| `>` | Built-in commands | `>guid` |

## How it works
1. Press the launcher hotkey.
   Windows currently uses `Win+Space`. Other platforms currently use `Ctrl+Space`.
2. Start typing.
3. Use the arrow keys to move through results.
4. Press `Enter` to run the selected result.
5. Press `Esc` to dismiss the launcher.

## Build and run
Prereqs: .NET 8 SDK.

```bash
dotnet build G33kSeek.sln
dotnet run --project G33kSeek.csproj
```

## Current status
G33kSeek is already useful as a personal launcher, but it is still evolving. The current focus is fast local search, reliable keyboard interaction, and a clean cross-platform Avalonia shell.

Implemented today:
- MacOS and Windows application discovery
- cached file and folder indexing
- direct path opening
- calculator and conversion queries
- help and web search support
- command-driven launcher utilities

## License
Licensed under the MIT License. See [LICENSE](LICENSE) for details.
