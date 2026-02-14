namespace MarkViewer;

using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Markdig;
using Microsoft.Web.WebView2.Core;

public partial class MainWindow : Window
{
    private string? _currentFolder;
    private string? _currentFile;
    private string? _currentBaseDir;
    private string? _currentViewerUrl;
    private bool _pageReady;
    private string? _pendingMarkdownHtml;
    private readonly MarkdownPipeline _markdownPipeline;
    private FileSystemWatcher? _fileWatcher;
    private bool _navVisible = true;
    private string _templateHtml = "";
    private const string VirtualHostName = "markviewer.local";
    private const string ViewerFileName = "__markviewer__.html";

    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarkViewer", "settings.json");

    private static readonly HashSet<string> MarkdownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".mdown", ".mkd", ".mkdn", ".mdwn", ".mdtxt", ".mdtext"
    };

    public MainWindow()
    {
        InitializeComponent();

        // Set window icon from the embedded EXE icon
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                {
                    Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                }
            }
        }
        catch { }

        _markdownPipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseEmojiAndSmiley()
            .UseTaskLists()
            .UseAutoLinks()
            .Build();

        Loaded += MainWindow_Loaded;

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => OpenFolder()),
            new KeyGesture(Key.O, ModifierKeys.Control)));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => ToggleNav()),
            new KeyGesture(Key.B, ModifierKeys.Control)));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => ReloadCurrentFile()),
            new KeyGesture(Key.R, ModifierKeys.Control)));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => ShowFindBar()),
            new KeyGesture(Key.F, ModifierKeys.Control)));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => ShowSearchPanel()),
            new KeyGesture(Key.F, ModifierKeys.Control | ModifierKeys.Shift)));

        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => CloseOverlays()),
            new KeyGesture(Key.Escape)));
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var env = await CoreWebView2Environment.CreateAsync(
                userDataFolder: Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MarkViewer", "WebView2"));

            await MarkdownViewer.EnsureCoreWebView2Async(env);

            MarkdownViewer.CoreWebView2.Settings.IsScriptEnabled = true;
            MarkdownViewer.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            MarkdownViewer.CoreWebView2.Settings.IsStatusBarEnabled = false;
            MarkdownViewer.CoreWebView2.Settings.AreDevToolsEnabled = false;

            // Load the embedded HTML template once
            _templateHtml = LoadEmbeddedTemplate();

            // Intercept ALL requests to our virtual host — we serve everything ourselves
            MarkdownViewer.CoreWebView2.AddWebResourceRequestedFilter(
                $"https://{VirtualHostName}/*",
                CoreWebView2WebResourceContext.All);

            MarkdownViewer.CoreWebView2.WebResourceRequested += (s, args) =>
            {
                var uri = new Uri(args.Request.Uri);

                // Serve the viewer template HTML
                if (uri.AbsolutePath.EndsWith(ViewerFileName, StringComparison.OrdinalIgnoreCase))
                {
                    var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_templateHtml));
                    args.Response = MarkdownViewer.CoreWebView2.Environment.CreateWebResourceResponse(
                        stream, 200, "OK", "Content-Type: text/html; charset=utf-8");
                    return;
                }

                // Serve local files (images, etc.) from the base directory
                if (_currentBaseDir != null)
                {
                    // uri.AbsolutePath is like /img/ADO1.png — map to base dir
                    var relativePath = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
                    var localPath = Path.Combine(_currentBaseDir, relativePath.Replace('/', '\\'));

                    if (File.Exists(localPath))
                    {
                        try
                        {
                            var bytes = File.ReadAllBytes(localPath);
                            var stream = new MemoryStream(bytes);
                            var contentType = GetContentType(localPath);
                            args.Response = MarkdownViewer.CoreWebView2.Environment.CreateWebResourceResponse(
                                stream, 200, "OK", $"Content-Type: {contentType}");
                        }
                        catch
                        {
                            args.Response = MarkdownViewer.CoreWebView2.Environment.CreateWebResourceResponse(
                                null, 404, "Not Found", "");
                        }
                    }
                    else
                    {
                        args.Response = MarkdownViewer.CoreWebView2.Environment.CreateWebResourceResponse(
                            null, 404, "Not Found", "");
                    }
                }
            };

            // External link handling: open in default browser
            MarkdownViewer.CoreWebView2.NewWindowRequested += (s, args) =>
            {
                args.Handled = true;
                if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            };

            MarkdownViewer.CoreWebView2.NavigationCompleted += (s, args) =>
            {
                _pageReady = true;
                if (_pendingMarkdownHtml != null)
                {
                    InjectContent(_pendingMarkdownHtml);
                    _pendingMarkdownHtml = null;
                }
            };

            // Navigate to the virtual host so mermaid.js loads and gets cached immediately
            _currentBaseDir = Path.GetTempPath();
            _currentViewerUrl = $"https://{VirtualHostName}/{ViewerFileName}";
            MarkdownViewer.CoreWebView2.Navigate(_currentViewerUrl);

            // Restore last opened folder
            RestoreLastFolder();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize WebView2. Please ensure the WebView2 Runtime is installed.\n\n{ex.Message}",
                "MarkViewer - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string LoadEmbeddedTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "MarkViewer.Assets.template.html";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // --- Folder Navigation ---

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e) => OpenFolder();

    private void OpenFolder()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder containing markdown files",
            ShowNewFolderButton = false,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _currentFolder = dialog.SelectedPath;
            Title = $"MarkViewer — {Path.GetFileName(_currentFolder)}";
            TxtStatus.Text = _currentFolder;
            PopulateTree(_currentFolder);
            SaveLastFolder(_currentFolder);

            // Show nav if hidden
            if (!_navVisible) ToggleNav();
        }
    }

    private void PopulateTree(string rootPath)
    {
        FolderTree.Items.Clear();
        var rootItem = CreateDirectoryNode(new DirectoryInfo(rootPath), isRoot: true);
        if (rootItem != null)
        {
            rootItem.IsExpanded = true;
            FolderTree.Items.Add(rootItem);
        }
    }

    private TreeViewItem? CreateDirectoryNode(DirectoryInfo dir, bool isRoot = false)
    {
        TreeViewItem item;
        try
        {
            var subDirs = dir.GetDirectories()
                .Where(d => !d.Name.StartsWith('.'))
                .OrderBy(d => d.Name);

            var files = dir.GetFiles()
                .Where(f => MarkdownExtensions.Contains(f.Extension))
                .OrderBy(f => f.Name);

            // Skip empty directories (no markdown files anywhere below)
            if (!isRoot && !files.Any() && !subDirs.Any(d => HasMarkdownFiles(d)))
                return null;

            item = new TreeViewItem
            {
                Header = CreateTreeItemHeader(isRoot ? $"📁 {dir.Name}" : $"📁 {dir.Name}"),
                Tag = dir.FullName,
                IsExpanded = isRoot,
                FontWeight = isRoot ? FontWeights.SemiBold : FontWeights.Normal
            };

            // Add subdirectories
            foreach (var subDir in subDirs)
            {
                var childNode = CreateDirectoryNode(subDir);
                if (childNode != null)
                    item.Items.Add(childNode);
            }

            // Add markdown files
            foreach (var file in files)
            {
                var fileItem = new TreeViewItem
                {
                    Header = CreateTreeItemHeader($"📄 {file.Name}"),
                    Tag = file.FullName
                };
                item.Items.Add(fileItem);
            }
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        return item;
    }

    private static bool HasMarkdownFiles(DirectoryInfo dir)
    {
        try
        {
            if (dir.Name.StartsWith('.')) return false;

            if (dir.GetFiles().Any(f => MarkdownExtensions.Contains(f.Extension)))
                return true;

            return dir.GetDirectories()
                .Where(d => !d.Name.StartsWith('.'))
                .Any(HasMarkdownFiles);
        }
        catch
        {
            return false;
        }
    }

    private static TextBlock CreateTreeItemHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 300
        };
    }

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem selectedItem && selectedItem.Tag is string path)
        {
            if (File.Exists(path) && MarkdownExtensions.Contains(Path.GetExtension(path)))
            {
                LoadMarkdownFile(path);
            }
        }
    }

    // --- Markdown Rendering ---

    private void LoadMarkdownFile(string filePath)
    {
        try
        {
            _currentFile = filePath;
            var markdown = File.ReadAllText(filePath);
            var html = Markdig.Markdown.ToHtml(markdown, _markdownPipeline);

            // Determine the base folder and relative directory for the file
            var fileDir = Path.GetDirectoryName(filePath);
            var baseDir = _currentFolder ?? fileDir;

            if (baseDir != null)
            {
                _currentBaseDir = baseDir;

                // Compute relative path from base folder to the file's directory
                var relativeDir = fileDir != null && _currentFolder != null
                    ? Path.GetRelativePath(_currentFolder, fileDir).Replace('\\', '/')
                    : "";
                if (relativeDir == ".") relativeDir = "";

                var viewerUrl = string.IsNullOrEmpty(relativeDir)
                    ? $"https://{VirtualHostName}/{ViewerFileName}"
                    : $"https://{VirtualHostName}/{relativeDir}/{ViewerFileName}";

                // Only do a full navigation if the subdirectory changed.
                // Otherwise just inject the new content — much faster.
                if (_pageReady && viewerUrl == _currentViewerUrl)
                {
                    InjectContent(html);
                }
                else
                {
                    _pendingMarkdownHtml = html;
                    _pageReady = false;
                    _currentViewerUrl = viewerUrl;
                    MarkdownViewer.CoreWebView2.Navigate(viewerUrl);
                }
            }

            // Update UI
            var relativePath = _currentFolder != null
                ? Path.GetRelativePath(_currentFolder, filePath)
                : Path.GetFileName(filePath);

            TxtCurrentFile.Text = relativePath;
            Title = $"MarkViewer — {relativePath}";

            // Word count
            var wordCount = Regex.Matches(markdown, @"\b\w+\b").Count;
            TxtWordCount.Text = $"{wordCount:N0} words";
            TxtStatus.Text = filePath;

            SetupFileWatcher(filePath);
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Error: {ex.Message}";
            var errorHtml = $"<div style='color:#f44747;padding:20px;'><h2>Error loading file</h2><p>{EscapeHtml(ex.Message)}</p></div>";
            _pendingMarkdownHtml = errorHtml;
        }
    }

    private async void InjectContent(string html)
    {
        try
        {
            var escaped = html.Replace("\\", "\\\\")
                              .Replace("`", "\\`")
                              .Replace("$", "\\$");

            await MarkdownViewer.CoreWebView2.ExecuteScriptAsync(
                $"window.renderContent(`{escaped}`);");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Render error: {ex.Message}");
        }
    }

    // --- File Watching (Auto-reload) ---

    private void SetupFileWatcher(string filePath)
    {
        _fileWatcher?.Dispose();

        var dir = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (dir == null) return;

        _fileWatcher = new FileSystemWatcher(dir, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _fileWatcher.Changed += (s, e) =>
        {
            // Debounce - wait a bit for the file to finish writing
            Thread.Sleep(200);
            Dispatcher.Invoke(() =>
            {
                if (_currentFile == filePath)
                    LoadMarkdownFile(filePath);
            });
        };
    }

    // --- Toggle Navigation ---

    private void BtnToggleNav_Click(object sender, RoutedEventArgs e) => ToggleNav();

    private void ToggleNav()
    {
        _navVisible = !_navVisible;

        if (_navVisible)
        {
            NavColumn.Width = new GridLength(280);
            NavColumn.MinWidth = 180;
            NavPanel.Visibility = Visibility.Visible;
        }
        else
        {
            NavColumn.Width = new GridLength(0);
            NavColumn.MinWidth = 0;
            NavPanel.Visibility = Visibility.Collapsed;
        }
    }

    // --- Reload ---

    private void ReloadCurrentFile()
    {
        if (_currentFile != null && File.Exists(_currentFile))
            LoadMarkdownFile(_currentFile);
    }

    // --- Find in Page (Ctrl+F) ---

    private void ShowFindBar()
    {
        FindBar.Visibility = Visibility.Visible;
        TxtFind.Focus();
        TxtFind.SelectAll();
    }

    private void CloseFindBar()
    {
        FindBar.Visibility = Visibility.Collapsed;
        TxtFindCount.Text = "";
        ClearFindHighlights();
    }

    private void BtnCloseFindBar_Click(object sender, RoutedEventArgs e) => CloseFindBar();

    private void TxtFind_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = TxtFind.Text;
        if (string.IsNullOrEmpty(query))
        {
            TxtFindCount.Text = "";
            ClearFindHighlights();
            return;
        }
        FindInPage(query);
    }

    private void TxtFind_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                FindPrev();
            else
                FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFindBar();
            e.Handled = true;
        }
    }

    private void BtnFindNext_Click(object sender, RoutedEventArgs e) => FindNext();
    private void BtnFindPrev_Click(object sender, RoutedEventArgs e) => FindPrev();

    private async void FindInPage(string query)
    {
        if (!_pageReady) return;
        try
        {
            var escapedQuery = query.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", " ");
            var script = $@"
                (function() {{
                    // Remove old highlights
                    document.querySelectorAll('mark.mv-find').forEach(m => {{
                        const parent = m.parentNode;
                        parent.replaceChild(document.createTextNode(m.textContent), m);
                        parent.normalize();
                    }});

                    const query = '{escapedQuery}'.toLowerCase();
                    if (!query) return JSON.stringify({{count: 0}});

                    const walker = document.createTreeWalker(
                        document.getElementById('content'),
                        NodeFilter.SHOW_TEXT, null
                    );

                    const matches = [];
                    let node;
                    while (node = walker.nextNode()) {{
                        const text = node.textContent.toLowerCase();
                        let idx = text.indexOf(query);
                        while (idx !== -1) {{
                            matches.push({{node, idx, len: query.length}});
                            idx = text.indexOf(query, idx + 1);
                        }}
                    }}

                    // Highlight in reverse to preserve offsets
                    for (let i = matches.length - 1; i >= 0; i--) {{
                        const m = matches[i];
                        const range = document.createRange();
                        range.setStart(m.node, m.idx);
                        range.setEnd(m.node, m.idx + m.len);
                        const mark = document.createElement('mark');
                        mark.className = 'mv-find';
                        mark.style.cssText = 'background:#614d00;color:#fff;padding:1px 2px;border-radius:2px;';
                        range.surroundContents(mark);
                    }}

                    // Scroll to first
                    const first = document.querySelector('mark.mv-find');
                    if (first) {{
                        first.style.background = '#e8a315';
                        first.scrollIntoView({{block:'center'}});
                        first.dataset.current = 'true';
                    }}

                    return JSON.stringify({{count: matches.length}});
                }})()
            ";
            var result = await MarkdownViewer.CoreWebView2.ExecuteScriptAsync(script);
            var json = System.Text.Json.JsonSerializer.Deserialize<FindResult>(
                result.Trim('"').Replace("\\\"", "\"").Replace("\\n", "\n"));

            // Parse result: it's a JSON string inside a JSON string
            if (result != null && result != "null")
            {
                var clean = result.Trim('"').Replace("\\\"", "\"");
                var parsed = System.Text.Json.JsonSerializer.Deserialize<FindResult>(clean);
                TxtFindCount.Text = parsed?.Count > 0 ? $"{parsed.Count} match(es)" : "No matches";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Find error: {ex.Message}");
        }
    }

    private async void FindNext()
    {
        if (!_pageReady) return;
        try
        {
            await MarkdownViewer.CoreWebView2.ExecuteScriptAsync(@"
                (function() {
                    const marks = Array.from(document.querySelectorAll('mark.mv-find'));
                    if (!marks.length) return;
                    const cur = marks.findIndex(m => m.dataset.current === 'true');
                    if (cur >= 0) { marks[cur].style.background = '#614d00'; delete marks[cur].dataset.current; }
                    const next = (cur + 1) % marks.length;
                    marks[next].style.background = '#e8a315';
                    marks[next].dataset.current = 'true';
                    marks[next].scrollIntoView({block:'center'});
                })()
            ");
        }
        catch { }
    }

    private async void FindPrev()
    {
        if (!_pageReady) return;
        try
        {
            await MarkdownViewer.CoreWebView2.ExecuteScriptAsync(@"
                (function() {
                    const marks = Array.from(document.querySelectorAll('mark.mv-find'));
                    if (!marks.length) return;
                    const cur = marks.findIndex(m => m.dataset.current === 'true');
                    if (cur >= 0) { marks[cur].style.background = '#614d00'; delete marks[cur].dataset.current; }
                    const prev = (cur - 1 + marks.length) % marks.length;
                    marks[prev].style.background = '#e8a315';
                    marks[prev].dataset.current = 'true';
                    marks[prev].scrollIntoView({block:'center'});
                })()
            ");
        }
        catch { }
    }

    private async void ClearFindHighlights()
    {
        if (!_pageReady) return;
        try
        {
            await MarkdownViewer.CoreWebView2.ExecuteScriptAsync(@"
                document.querySelectorAll('mark.mv-find').forEach(m => {
                    const parent = m.parentNode;
                    parent.replaceChild(document.createTextNode(m.textContent), m);
                    parent.normalize();
                });
            ");
        }
        catch { }
    }

    // --- Search Across Files (Ctrl+Shift+F) ---

    private void ShowSearchPanel()
    {
        if (!_navVisible) ToggleNav();
        SearchPanel.Visibility = Visibility.Visible;
        SearchResults.Visibility = Visibility.Visible;
        TxtSearch.Focus();
        TxtSearch.SelectAll();
    }

    private void CloseSearchPanel()
    {
        SearchPanel.Visibility = Visibility.Collapsed;
        SearchResults.Visibility = Visibility.Collapsed;
        SearchResults.ItemsSource = null;
    }

    private void BtnCloseSearch_Click(object sender, RoutedEventArgs e) => CloseSearchPanel();

    private void CloseOverlays()
    {
        if (FindBar.Visibility == Visibility.Visible)
            CloseFindBar();
        else if (SearchPanel.Visibility == Visibility.Visible)
            CloseSearchPanel();
    }

    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SearchInFiles(TxtSearch.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseSearchPanel();
            e.Handled = true;
        }
    }

    private void SearchInFiles(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || _currentFolder == null)
        {
            SearchResults.ItemsSource = null;
            return;
        }

        TxtStatus.Text = "Searching...";
        var results = new List<SearchResultItem>();

        try
        {
            var files = Directory.EnumerateFiles(_currentFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => MarkdownExtensions.Contains(Path.GetExtension(f)));

            foreach (var file in files)
            {
                try
                {
                    var lines = File.ReadLines(file);
                    int lineNum = 0;
                    foreach (var line in lines)
                    {
                        lineNum++;
                        if (line.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            var preview = line.Trim();
                            if (preview.Length > 120) preview = preview[..120] + "...";

                            results.Add(new SearchResultItem
                            {
                                FileName = Path.GetFileName(file),
                                RelativePath = Path.GetRelativePath(_currentFolder, file),
                                FullPath = file,
                                LineNumber = lineNum,
                                MatchPreview = $"  L{lineNum}: {preview}"
                            });

                            if (results.Count >= 200) break; // Cap results
                        }
                    }
                }
                catch { }

                if (results.Count >= 200) break;
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Search error: {ex.Message}";
            return;
        }

        SearchResults.ItemsSource = results;
        TxtStatus.Text = $"Found {results.Count} result(s) for \"{query}\"";
    }

    private void SearchResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResults.SelectedItem is SearchResultItem item)
        {
            LoadMarkdownFile(item.FullPath);
        }
    }

    // --- Helpers ---

    private static string GetContentType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".ico" => "image/x-icon",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".pdf" => "application/pdf",
            ".html" or ".htm" => "text/html",
            _ => "application/octet-stream"
        };
    }

    private static string EscapeHtml(string text)
    {
        return text.Replace("&", "&amp;")
                   .Replace("<", "&lt;")
                   .Replace(">", "&gt;")
                   .Replace("\"", "&quot;");
    }

    protected override void OnClosed(EventArgs e)
    {
        _fileWatcher?.Dispose();
        base.OnClosed(e);
    }

    // --- Settings Persistence ---

    private void SaveLastFolder(string folderPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (dir != null) Directory.CreateDirectory(dir);

            var json = System.Text.Json.JsonSerializer.Serialize(new AppSettings
            {
                LastFolder = folderPath
            });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }

    private void RestoreLastFolder()
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return;

            var json = File.ReadAllText(SettingsFilePath);
            var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

            if (settings?.LastFolder != null && Directory.Exists(settings.LastFolder))
            {
                _currentFolder = settings.LastFolder;
                Title = $"MarkViewer — {Path.GetFileName(_currentFolder)}";
                TxtStatus.Text = _currentFolder;
                PopulateTree(_currentFolder);
            }
        }
        catch { }
    }
}

/// <summary>
/// Simple ICommand implementation for keyboard shortcuts.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
}

public class FindResult
{
    public int Count { get; set; }
}

public class SearchResultItem
{
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string FullPath { get; set; } = "";
    public int LineNumber { get; set; }
    public string MatchPreview { get; set; } = "";
}

public class AppSettings
{
    public string? LastFolder { get; set; }
}
