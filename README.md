# MarkViewer

A lightweight, dark-themed Markdown viewer for Windows built with WPF and WebView2.

![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)

## Features

- **Folder Explorer** — Open a folder and browse all Markdown files in a VS Code-style tree view
- **Live Preview** — Renders Markdown to styled HTML with a dark theme using [Markdig](https://github.com/xoofx/markdig)
- **Mermaid Diagrams** — Renders fenced `mermaid` code blocks as diagrams via [Mermaid.js](https://mermaid.js.org/)
- **Auto-Reload** — Watches the active file for changes and re-renders automatically
- **Find in Page** (`Ctrl+F`) — Search within the rendered preview with match highlighting and navigation
- **Search Across Files** (`Ctrl+Shift+F`) — Full-text search across all Markdown files in the open folder
- **Image & Asset Support** — Displays relative images and assets referenced from Markdown files
- **Keyboard Shortcuts** — `Ctrl+O` open folder, `Ctrl+B` toggle sidebar, `Ctrl+R` reload, and more
- **Session Persistence** — Remembers the last opened folder between sessions
- **Word Count** — Displays the word count of the active document in the status bar

## Screenshots

*Open a folder, pick a Markdown file, and view it instantly:*

<!-- Add a screenshot here, e.g.: ![Screenshot](docs/screenshot.png) -->

## Prerequisites

- **Windows 10/11**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (to build) or .NET 8 Desktop Runtime (to run)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) — ships with modern Windows and Edge

## Getting Started

### Clone the repository

```bash
git clone https://github.com/<your-username>/MarkViewer.git
cd MarkViewer
```

### Build and run

```bash
dotnet build
dotnet run
```

Or open `MarkViewer.sln` in Visual Studio 2022+ and press **F5**.

## Usage

1. Click **📁 Open Folder** (or press `Ctrl+O`) to select a folder containing Markdown files.
2. The explorer panel lists all `.md` files in the folder tree.
3. Click a file to render it in the preview pane.
4. Edit the file in any editor — MarkViewer will auto-reload on save.

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+O` | Open folder |
| `Ctrl+B` | Toggle explorer panel |
| `Ctrl+R` | Reload current file |
| `Ctrl+F` | Find in page |
| `Ctrl+Shift+F` | Search across files |
| `Escape` | Close find bar / search panel |

## Project Structure

```
MarkViewer/
├── App.xaml / App.xaml.cs        # Application entry point
├── MainWindow.xaml               # UI layout (WPF XAML)
├── MainWindow.xaml.cs            # Application logic
├── Assets/
│   └── template.html             # Embedded HTML template for the preview
├── MarkViewer.csproj             # Project file (.NET 8, WPF)
├── MarkViewer.sln                # Solution file
├── SampleDocs/                   # Sample Markdown files for testing
└── LICENSE                       # MIT License
```

## Technology

| Component | Technology |
|---|---|
| UI Framework | WPF (.NET 8) |
| Markdown Parsing | [Markdig](https://github.com/xoofx/markdig) |
| Rendered Preview | [WebView2](https://learn.microsoft.com/en-us/microsoft-edge/webview2/) |
| Diagram Rendering | [Mermaid.js](https://mermaid.js.org/) (loaded from CDN) |

## License

This project is licensed under the [MIT License](LICENSE).
