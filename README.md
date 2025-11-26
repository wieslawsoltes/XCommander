# XCommander

A cross-platform file manager inspired by Total Commander, built with Avalonia UI and C#.

![XCommander Screenshot](docs/screenshot.png)

## Features

### Current Features (v0.3)

- **Dual Panel Interface**: Two side-by-side file panels for efficient file management
- **Tabbed Interface**: Multiple tabs per panel with tab locking
- **Cross-Platform**: Works on Windows, macOS, and Linux
- **File Navigation**: 
  - Browse directories
  - Navigate with back/forward history
  - Breadcrumb path bar
  - Drive/volume selector
  - Go to parent directory
- **File Operations**:
  - Copy files/folders (F5)
  - Move files/folders (F6)
  - Delete files/folders (F8) with confirmation
  - Create new folder (F7)
  - Create new file (Shift+F4)
  - Rename files (F2)
- **Selection**:
  - Single and multiple file selection
  - Select all (Ctrl+A)
  - Invert selection (Ctrl+I)
  - Quick filter
- **View Options**:
  - Show/hide hidden files (Ctrl+H)
  - Sortable columns (Name, Size, Date)
  - File icons based on type
  - Quick View Panel (Ctrl+Q)
- **Built-in File Viewer (F3)**:
  - Text mode with encoding selection
  - Hex mode for binary files
  - Image preview
  - Text search with navigation
  - Word wrap toggle
- **Search Tool (Alt+F7)**:
  - Search by filename pattern
  - Search in file content
  - Date and size filters
  - Regular expression support
  - Hidden files option
- **Multi-Rename Tool (Ctrl+M)**:
  - Search and replace with regex
  - Case transformation
  - Counter placeholders
  - Date/time placeholders
  - Preview before renaming
- **FTP Client (Ctrl+F)**:
  - Connect to FTP servers
  - Browse remote directories
  - Upload and download files
  - Create and delete remote files/folders
- **Directory Comparison (Shift+F6)**:
  - Compare two directories side-by-side
  - Compare by content, date, or size
  - Include subdirectories
  - Copy to left/right
  - Select by status (left only, right only, different)
- **File Comparison (Ctrl+D)**:
  - Side-by-side diff view
  - Text comparison with syntax highlighting
  - Binary/hex comparison mode
  - Ignore whitespace/case/empty lines
  - Navigate between differences
- **Checksum Calculator (Ctrl+Shift+C)**:
  - MD5, SHA1, SHA256, SHA512, CRC32
  - Verify checksums against input
  - Copy to clipboard
  - Export results
- **Settings (Ctrl+,)**:
  - Appearance (theme, fonts)
  - File display options
  - Behavior settings
  - External programs configuration
  - Tab settings
  - Quick view settings
  - File operations settings
  - Search settings
- **Keyboard Shortcuts**: Comprehensive keyboard shortcuts for power users
- **Function Key Bar**: Quick access to common operations
- **Command Line**: Built-in command line for executing commands

### Planned Features

- [ ] Archive support (ZIP, 7z, RAR, TAR)
- [ ] SFTP support
- [ ] Directory synchronization
- [ ] Plugin system
- [ ] Customizable themes
- [ ] Localization

## Requirements

- .NET 8.0 or later
- Windows 10/11, macOS 10.15+, or Linux

## Building from Source

```bash
# Clone the repository
git clone https://github.com/yourusername/XCommander.git
cd XCommander

# Build the project
dotnet build

# Run the application
dotnet run --project src/XCommander
```

## Keyboard Shortcuts

### Navigation
| Key | Action |
|-----|--------|
| Tab | Switch between panels |
| Enter | Open folder/execute file |
| Backspace | Go to parent directory |
| Alt+Left | Go back in history |
| Alt+Right | Go forward in history |

### Selection
| Key | Action |
|-----|--------|
| Space | Select/deselect item |
| Ctrl+A | Select all |
| Ctrl+Shift+A | Deselect all |
| Ctrl+I | Invert selection |

### File Operations
| Key | Action |
|-----|--------|
| F2 | Rename |
| F3 | View file |
| F4 | Edit file |
| F5 | Copy to other panel |
| F6 | Move to other panel |
| F7 | Create new folder |
| F8 / Delete | Delete |
| Shift+F4 | Create new file |

### View
| Key | Action |
|-----|--------|
| Ctrl+H | Toggle hidden files |
| Ctrl+R | Refresh |
| Ctrl+Q | Toggle Quick View panel |

### Tools
| Key | Action |
|-----|--------|
| Alt+F7 | Search |
| Ctrl+M | Multi-Rename Tool |
| Shift+F6 | Compare Directories |
| Ctrl+D | Compare Files |
| Ctrl+Shift+C | Checksum Calculator |
| Ctrl+F | FTP Connect |
| Ctrl+, | Settings |

### Tabs
| Key | Action |
|-----|--------|
| Ctrl+T | New tab |
| Ctrl+W | Close tab |
| Ctrl+Tab | Next tab |
| Ctrl+Shift+Tab | Previous tab |

## Project Structure

```
XCommander/
├── src/
│   └── XCommander/           # Main application
│       ├── ViewModels/       # MVVM ViewModels
│       ├── Views/            # Avalonia XAML Views
│       ├── Models/           # Data Models
│       ├── Services/         # Business Logic
│       ├── Controls/         # Custom Controls
│       └── Converters/       # Value Converters
├── tests/                    # Unit tests
├── docs/                     # Documentation
├── DESIGN.md                 # Design document
├── README.md                 # This file
└── XCommander.sln           # Solution file
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Inspired by [Total Commander](https://www.ghisler.com/)
- Built with [Avalonia UI](https://avaloniaui.net/)
- Icons from various open-source icon sets

## Screenshots

*Coming soon*
