# XCommander

A professional, cross‑platform, dual‑pane file manager inspired by Total Commander. Built with Avalonia UI and C# to deliver a fast, keyboard‑first workflow on Windows, macOS, and Linux.


## Highlights

- Dual‑pane interface with tabbed panels and tab locking
- Keyboard‑centric workflow with TC‑style shortcuts
- Powerful file operations (copy/move/delete/rename) with confirmations
- Integrated tools: search, multi‑rename, compare, checksum, and viewer
- FTP client for remote browsing and transfers
- Quick View, quick filter, and status details for high‑density navigation

## Feature Set

### Navigation & Panels
- Dual panels with independent tabs
- Back/forward history and parent navigation
- Drive/volume selector and path bar
- Quick filter and selection tools

### File Operations
- Copy (F5), Move (F6), Delete (F8), Rename (F2)
- Create new folder (F7) and file (Shift+F4)
- Selection helpers: select all, invert, range selection

### Tools
- Search (Alt+F7): name/content, size/date filters, regex
- Multi‑Rename (Ctrl+M): regex, counters, placeholders, preview
- Compare directories (Shift+F6) and files (Ctrl+D)
- Checksum calculator (Ctrl+Shift+C)

### Viewer
- Built‑in file viewer (F3)
- Text, hex, and image modes
- Encoding selection, search, and word‑wrap

### Network
- FTP client (Ctrl+F)

### Customization
- Appearance, behavior, tabs, and tool settings
- Configurable toolbar and keyboard shortcuts

## Requirements

- .NET 8.0 or later
- Windows 10/11, macOS 10.15+, or Linux

## Build & Run

```bash
# Clone the repository
# (use your fork or the canonical repo URL)

git clone <repo-url>
cd XCommander

# Build

dotnet build

# Run

dotnet run --project src/XCommander
```

## Keyboard Shortcuts (Common)

### Navigation
| Key | Action |
|-----|--------|
| Tab | Switch panels |
| Enter | Open folder / execute file |
| Backspace | Parent directory |
| Alt+Left | Back |
| Alt+Right | Forward |

### Operations
| Key | Action |
|-----|--------|
| F2 | Rename |
| F3 | View |
| F4 | Edit |
| F5 | Copy |
| F6 | Move |
| F7 | New folder |
| F8 / Delete | Delete |
| Shift+F4 | New file |

### Tools
| Key | Action |
|-----|--------|
| Alt+F7 | Search |
| Ctrl+M | Multi‑Rename |
| Shift+F6 | Compare dirs |
| Ctrl+D | Compare files |
| Ctrl+Shift+C | Checksum |
| Ctrl+F | FTP |
| Ctrl+, | Settings |

## Contributing

Contributions are welcome. Please open an issue or submit a pull request with a clear description of changes and rationale.

## License

See `LICENSE` for details.
