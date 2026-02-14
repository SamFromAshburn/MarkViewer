# Welcome to MarkViewer

This is a sample markdown file to test the viewer.

## Features

- **Folder navigation** with collapsible tree
- **Mermaid diagram** rendering
- **Live reload** when files change
- Dark theme optimized for reading

## Task List

- [x] Create WPF application
- [x] Add WebView2 for rendering
- [x] Integrate Markdig for markdown parsing
- [x] Add Mermaid.js support
- [ ] More features coming soon!

## Code Example

```csharp
public class HelloWorld
{
    public static void Main(string[] args)
    {
        Console.WriteLine("Hello from MarkViewer!");
    }
}
```

## Mermaid Flowchart

```mermaid
graph TD
    A[Open Folder] --> B{Select File}
    B --> C[.md file]
    C --> D[Parse with Markdig]
    D --> E[Render in WebView2]
    E --> F[Display to User]
    B --> G[Folder]
    G --> B
```

## Mermaid Sequence Diagram

```mermaid
sequenceDiagram
    participant User
    participant App
    participant Markdig
    participant WebView2
    participant Mermaid

    User->>App: Opens .md file
    App->>Markdig: Convert to HTML
    Markdig-->>App: HTML content
    App->>WebView2: Inject HTML
    WebView2->>Mermaid: Render diagrams
    Mermaid-->>WebView2: SVG output
    WebView2-->>User: Rendered page
```

## Table Example

| Feature | Status | Notes |
|---------|--------|-------|
| Markdown Rendering | ✅ | Via Markdig |
| Mermaid Diagrams | ✅ | Via Mermaid.js |
| Folder Navigation | ✅ | Collapsible tree |
| File Watching | ✅ | Auto-reload |
| Dark Theme | ✅ | VS Code inspired |

## Blockquote

> "The best way to predict the future is to invent it."
> — Alan Kay

---

*Enjoy using MarkViewer!*
