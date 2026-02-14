# Contributing to MarkViewer

Thanks for your interest in contributing! This guide explains how to get started.

## Prerequisites

- **Windows 10/11**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- A code editor — [Visual Studio 2022](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) with the C# extension

## Setting Up the Development Environment

1. **Fork and clone** the repository:

   ```bash
   git clone https://github.com/<your-username>/MarkViewer.git
   cd MarkViewer
   ```

2. **Restore dependencies and build**:

   ```bash
   dotnet restore
   dotnet build
   ```

3. **Run the app**:

   ```bash
   dotnet run
   ```

   You can also open `MarkViewer.sln` in Visual Studio and press **F5**.

4. **Test with sample files** — use the files in `SampleDocs/` or open any folder containing Markdown files.

## How to Contribute

### Reporting Bugs

- Open an issue with a clear title and description.
- Include steps to reproduce, expected behavior, and actual behavior.
- Mention your Windows version and .NET SDK version (`dotnet --version`).

### Suggesting Features

- Open an issue describing the feature and why it would be useful.
- If possible, include mockups or examples.

### Submitting Code Changes

1. **Create a branch** from `main`:

   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes** — keep commits focused and well-described.

3. **Build and test** to make sure everything works:

   ```bash
   dotnet build
   dotnet run
   ```

4. **Submit a pull request** targeting `main`:
   - Provide a clear description of what the PR does and why.
   - Reference any related issues (e.g., "Fixes #12").

## Code Guidelines

- **Target framework**: .NET 8 (`net8.0-windows`), WPF.
- **Nullable reference types** are enabled — avoid suppressing warnings without good reason.
- Use file-scoped namespaces and implicit usings (already configured in the project).
- Keep the dark theme consistent — follow the existing colour variables in [Assets/template.html](Assets/template.html).
- The HTML template is an **embedded resource**. After editing `Assets/template.html`, a rebuild is required for changes to take effect.
- Match the existing code style: concise, minimal comments where the code is self-explanatory, XML-doc comments for public APIs.

## Architecture Overview

| Layer | File(s) | Responsibility |
|---|---|---|
| UI Layout | `MainWindow.xaml` | WPF XAML defining the toolbar, explorer panel, WebView2 preview, find bar, and status bar |
| App Logic | `MainWindow.xaml.cs` | Folder browsing, Markdown parsing (Markdig), WebView2 content injection, file watching, search, find-in-page |
| Preview Template | `Assets/template.html` | Embedded HTML/CSS/JS template rendered inside WebView2; includes Mermaid.js for diagrams |
| Entry Point | `App.xaml` / `App.xaml.cs` | Standard WPF application bootstrap |

### Key design decisions

- **Virtual host mapping** — the app maps a virtual hostname (`markviewer.local`) so WebView2 can resolve relative image paths from Markdown files without `file://` URIs.
- **Content injection** — after the initial page load, Markdown content is injected via `ExecuteScriptAsync` rather than full page navigations, keeping transitions fast.
- **File watching** — a `FileSystemWatcher` monitors the active file and triggers a re-render on save.

## Questions?

If anything is unclear, feel free to open an issue and ask. We're happy to help!
