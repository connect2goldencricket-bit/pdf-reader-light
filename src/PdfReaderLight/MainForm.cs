using PdfReaderCore;

namespace PdfReaderLight;

public sealed class MainForm : Form
{
    private enum ZoomMode { Percent, FitWidth, FitPage }

    private const int MaxThumbnails = 300;
    private const int ThumbWidth = 110;

    private readonly string? _pendingOpen;
    private readonly AppSettings _settings = AppSettings.Load();

    private PdfEngine? _engine;
    private string? _filePath;
    private int _page;
    private double _zoom;
    private ZoomMode _zoomMode = ZoomMode.Percent;
    private List<SearchHit> _hits = new();
    private int _hitIndex = -1;
    private RectD[] _pageHighlights = Array.Empty<RectD>();
    private CancellationTokenSource? _renderCts;
    private int _renderSeq;
    private bool _syncing;
    private Point? _pendingScroll;

    private MenuStrip _menu = null!;
    private ToolStrip _tool = null!;
    private SplitContainer _split = null!;
    private FlowLayoutPanel _thumbs = null!;
    private Panel _scroll = null!;
    private PictureBox _pic = null!;
    private StatusStrip _status = null!;
    private ToolStripStatusLabel _lblFile = null!;
    private ToolStripStatusLabel _lblPage = null!;
    private ToolStripStatusLabel _lblZoom = null!;
    private ToolStripButton _btnOpen = null!;
    private ToolStripButton _btnPrev = null!;
    private ToolStripButton _btnNext = null!;
    private ToolStripButton _btnThumbs = null!;
    private NumericUpDown _numPage = null!;
    private ToolStripLabel _lblOf = null!;
    private ToolStripComboBox _cmbZoom = null!;
    private ToolStripTextBox _txtSearch = null!;
    private ToolStripButton _btnHitPrev = null!;
    private ToolStripButton _btnHitNext = null!;
    private ToolStripLabel _lblHits = null!;
    private ToolStripMenuItem _recentMenu = null!;
    private System.Windows.Forms.Timer _searchTimer = null!;
    private System.Windows.Forms.Timer _saveTimer = null!;
    private System.Windows.Forms.Timer _resizeTimer = null!;

    public MainForm(string? pendingOpen)
    {
        _pendingOpen = pendingOpen;
        Text = "PdfReaderLight";
        ClientSize = new Size(1100, 700);
        MinimumSize = new Size(640, 400);
        AllowDrop = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;

        _zoom = Math.Clamp(_settings.ZoomPercent / 100.0, 0.1, 8.0);

        BuildUi();
        UpdateUi();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (_pendingOpen != null && File.Exists(_pendingOpen))
            OpenFile(_pendingOpen);
    }

    private void BuildUi()
    {
        _menu = new MenuStrip();
        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Open…", null, (s, e) => OpenDialog()) { ShortcutKeys = Keys.Control | Keys.O });
        _recentMenu = new ToolStripMenuItem("&Recent");
        fileMenu.DropDownItems.Add(_recentMenu);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (s, e) => Close()) { ShortcutKeys = Keys.Alt | Keys.F4 });

        var viewMenu = new ToolStripMenuItem("&View");
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("Zoom &in", null, (s, e) => ZoomBy(1.25)) { ShortcutKeys = Keys.Control | Keys.Oemplus });
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("Zoom &out", null, (s, e) => ZoomBy(0.8)) { ShortcutKeys = Keys.Control | Keys.OemMinus });
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("Fit &width", null, (s, e) => SetZoomMode(ZoomMode.FitWidth)) { ShortcutKeys = Keys.Control | Keys.D1 });
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("Fit &page", null, (s, e) => SetZoomMode(ZoomMode.FitPage)) { ShortcutKeys = Keys.Control | Keys.D2 });
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("&100%", null, (s, e) => SetZoomPercent(100)) { ShortcutKeys = Keys.Control | Keys.D0 });
        viewMenu.DropDownItems.Add(new ToolStripSeparator());
        viewMenu.DropDownItems.Add(new ToolStripMenuItem("&Thumbnails", null, (s, e) => ToggleThumbnails()) { ShortcutKeys = Keys.F8 });

        var helpMenu = new ToolStripMenuItem("&Help");
        helpMenu.DropDownItems.Add(new ToolStripMenuItem("&About", null, (s, e) =>
            MessageBox.Show(this, "PdfReaderLight 1.0\nLightweight PDF reader for Windows (PDFium + WinForms).",
                "About PdfReaderLight", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        _menu.Items.AddRange(new ToolStripItem[] { fileMenu, viewMenu, helpMenu });

        _tool = new ToolStrip();
        _btnOpen = new ToolStripButton("Open") { ToolTipText = "Open PDF (Ctrl+O)" };
        _btnOpen.Click += (s, e) => OpenDialog();
        _btnPrev = new ToolStripButton("◀") { ToolTipText = "Previous page (PageUp)" };
        _btnPrev.Click += (s, e) => GoToPage(_page - 1);
        _numPage = new NumericUpDown { Name = "numPage", Minimum = 1, Maximum = 1, Width = 64 };
        _numPage.ValueChanged += (s, e) => { if (!_syncing && _engine != null) GoToPage((int)_numPage.Value - 1); };
        _lblOf = new ToolStripLabel("/ 0");
        _btnNext = new ToolStripButton("▶") { ToolTipText = "Next page (PageDown)" };
        _btnNext.Click += (s, e) => GoToPage(_page + 1);
        _cmbZoom = new ToolStripComboBox { Name = "cmbZoom", DropDownStyle = ComboBoxStyle.DropDown, Width = 90 };
        _cmbZoom.Items.AddRange(new object[] { "50%", "75%", "100%", "125%", "150%", "200%", "300%", "400%", "Fit width", "Fit page" });
        _cmbZoom.Text = $"{(int)Math.Round(_zoom * 100)}%";
        _cmbZoom.SelectedIndexChanged += (s, e) => ApplyZoomSelection();
        _cmbZoom.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) ApplyZoomSelection(); };
        _txtSearch = new ToolStripTextBox("txtSearch") { Width = 180, ToolTipText = "Search in document (Ctrl+F)" };
        _txtSearch.TextChanged += (s, e) => { _searchTimer.Stop(); _searchTimer.Start(); };
        _btnHitPrev = new ToolStripButton("▲") { ToolTipText = "Previous result (Shift+F3)" };
        _btnHitPrev.Click += (s, e) => GoToHit(_hitIndex - 1);
        _btnHitNext = new ToolStripButton("▼") { ToolTipText = "Next result (F3)" };
        _btnHitNext.Click += (s, e) => GoToHit(_hitIndex + 1);
        _lblHits = new ToolStripLabel("");
        _btnThumbs = new ToolStripButton("☰") { ToolTipText = "Toggle thumbnails (F8)", Checked = _settings.ShowThumbnails, CheckOnClick = true };
        _btnThumbs.Click += (s, e) => { _settings.ShowThumbnails = _btnThumbs.Checked; ApplyThumbnailVisibility(); };

        _tool.Items.AddRange(new ToolStripItem[]
        {
            _btnOpen, new ToolStripSeparator(), _btnPrev, new ToolStripControlHost(_numPage) { ToolTipText = "Page number" }, _lblOf, _btnNext, new ToolStripSeparator(),
            _cmbZoom, new ToolStripSeparator(), _txtSearch, _btnHitPrev, _btnHitNext, _lblHits,
            new ToolStripSeparator(), _btnThumbs,
        });

        _split = new SplitContainer { Dock = DockStyle.Fill, Panel1MinSize = 120, SplitterDistance = 150 };
        _thumbs = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        _split.Panel1.Controls.Add(_thumbs);
        _scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(64, 64, 64) };
        _pic = new PictureBox { SizeMode = PictureBoxSizeMode.Normal, BackColor = Color.White };
        _scroll.Controls.Add(_pic);
        _scroll.Resize += (s, e) => { CenterCanvas(); ScheduleFitRender(); };
        _split.Panel2.Controls.Add(_scroll);

        _status = new StatusStrip();
        _lblFile = new ToolStripStatusLabel("No file open") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _lblPage = new ToolStripStatusLabel("");
        _lblZoom = new ToolStripStatusLabel("");
        _status.Items.AddRange(new ToolStripItem[] { _lblFile, _lblPage, _lblZoom });

        Controls.Add(_split);
        Controls.Add(_tool);
        Controls.Add(_status);
        Controls.Add(_menu);
        MainMenuStrip = _menu;

        _searchTimer = new System.Windows.Forms.Timer { Interval = 400 };
        _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); RunSearch(); };
        _saveTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); _settings.Save(); };
        _resizeTimer = new System.Windows.Forms.Timer { Interval = 250 };
        _resizeTimer.Tick += (s, e) => { _resizeTimer.Stop(); if (_zoomMode != ZoomMode.Percent) RenderCurrent(); };

        DragEnter += (s, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        DragDrop += (s, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                OpenFile(files[0]);
        };
    }

    public void OpenFile(string path)
    {
        PdfEngine engine;
        try
        {
            engine = PdfEngine.Open(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Cannot open PDF", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        _engine?.Dispose();
        _engine = engine;
        _filePath = path;
        _page = _settings.LastPage.TryGetValue(path, out int last) ? Math.Clamp(last, 0, engine.PageCount - 1) : 0;
        _hits = new List<SearchHit>();
        _hitIndex = -1;
        _pageHighlights = Array.Empty<RectD>();
        _pendingScroll = null;
        if (_txtSearch.Text.Length > 0) _txtSearch.Clear();
        _lblHits.Text = "";

        Text = $"{Path.GetFileName(path)} - PdfReaderLight";
        _lblFile.Text = path;
        _numPage.Maximum = engine.PageCount;
        _syncing = true;
        _numPage.Value = _page + 1;
        _syncing = false;
        _lblOf.Text = $"/ {engine.PageCount}";
        _lblPage.Text = $"Page {_page + 1} of {engine.PageCount}";

        _settings.AddRecent(path);
        RebuildRecentMenu();
        BuildThumbnails();
        GoToPage(_page);
    }

    private void OpenDialog()
    {
        using var dlg = new OpenFileDialog { Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*", Title = "Open PDF" };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            OpenFile(dlg.FileName);
    }

    private void GoToPage(int page)
    {
        var engine = _engine;
        if (engine == null)
            return;
        page = Math.Clamp(page, 0, engine.PageCount - 1);
        _page = page;
        var hit = _hitIndex >= 0 && _hitIndex < _hits.Count ? _hits[_hitIndex] : null;
        _pageHighlights = hit != null && hit.PageIndex == page ? hit.Rects.ToArray() : Array.Empty<RectD>();
        _syncing = true;
        _numPage.Value = page + 1;
        _syncing = false;
        _lblPage.Text = $"Page {page + 1} of {engine.PageCount}";
        SelectThumb(page);
        if (_filePath != null)
        {
            _settings.LastPage[_filePath] = page;
            _saveTimer.Stop();
            _saveTimer.Start();
        }
        RenderCurrent();
    }

    private double CurrentScale()
    {
        var engine = _engine;
        if (engine == null)
            return _zoom;
        var info = engine.GetPageInfo(_page);
        return CurrentScaleFor(info);
    }

    private double CurrentScaleFor(PageInfo info)
    {
        var s = _scroll.ClientSize;
        const int margin = 28;
        return _zoomMode switch
        {
            ZoomMode.FitWidth => Math.Max(0.05, (s.Width - margin) / info.DisplayWidthPoints),
            ZoomMode.FitPage => Math.Max(0.05, Math.Min((s.Width - margin) / info.DisplayWidthPoints, (s.Height - margin) / info.DisplayHeightPoints)),
            _ => _zoom,
        };
    }

    private void RenderCurrent()
    {
        var engine = _engine;
        if (engine == null)
        {
            _pic.Image = null;
            return;
        }
        _renderCts?.Cancel();
        var cts = _renderCts = new CancellationTokenSource();
        int seq = ++_renderSeq;
        int page = _page;
        PageInfo info = engine.GetPageInfo(page);
        double scale = CurrentScaleFor(info);
        RectD[] highlights = _pageHighlights;

        Task.Run(() =>
        {
            RenderedPage rendered = engine.RenderPage(page, scale);
            if (cts.IsCancellationRequested)
                return;
            BeginInvoke(() =>
            {
                if (cts.IsCancellationRequested || seq != _renderSeq || !ReferenceEquals(_engine, engine))
                    return;
                var bmp = RenderUtil.ToBitmap(rendered);
                if (highlights.Length > 0)
                    RenderUtil.DrawHighlights(bmp, scale, info.DisplayHeightPoints, highlights);
                var old = _pic.Image;
                _pic.Image = bmp;
                _pic.Size = bmp.Size;
                CenterCanvas();
                old?.Dispose();
                _lblZoom.Text = $"{(int)Math.Round(scale * 100)}%";
                if (_pendingScroll is Point pt)
                {
                    _scroll.AutoScrollPosition = new Point(Math.Max(0, pt.X), Math.Max(0, pt.Y));
                    _pendingScroll = null;
                }
            });
        });
    }

    private void CenterCanvas()
    {
        var s = _scroll.ClientSize;
        _pic.Location = new Point(
            Math.Max(12, (s.Width - _pic.Width) / 2),
            Math.Max(12, (s.Height - _pic.Height) / 2));
    }

    private void ScheduleFitRender()
    {
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private void ZoomBy(double factor)
    {
        _zoomMode = ZoomMode.Percent;
        _zoom = Math.Clamp(_zoom * factor, 0.1, 8.0);
        _settings.ZoomPercent = _zoom * 100;
        _cmbZoom.Text = $"{(int)Math.Round(_zoom * 100)}%";
        RenderCurrent();
    }

    private void SetZoomPercent(double pct)
    {
        _zoomMode = ZoomMode.Percent;
        _zoom = pct / 100.0;
        _settings.ZoomPercent = pct;
        _cmbZoom.Text = $"{(int)pct}%";
        RenderCurrent();
    }

    private void SetZoomMode(ZoomMode mode)
    {
        _zoomMode = mode;
        _cmbZoom.Text = mode == ZoomMode.FitWidth ? "Fit width" : "Fit page";
        RenderCurrent();
    }

    private void ApplyZoomSelection()
    {
        var text = _cmbZoom.Text.Trim();
        if (text == "Fit width") SetZoomMode(ZoomMode.FitWidth);
        else if (text == "Fit page") SetZoomMode(ZoomMode.FitPage);
        else if (text.EndsWith('%') && double.TryParse(text.TrimEnd('%'), out double pct)) SetZoomPercent(pct);
    }

    private void RunSearch()
    {
        var engine = _engine;
        if (engine == null)
            return;
        var query = _txtSearch.Text.Trim();
        _hits = new List<SearchHit>();
        _hitIndex = -1;
        _pageHighlights = Array.Empty<RectD>();
        if (query.Length == 0)
        {
            _lblHits.Text = "";
            RenderCurrent();
            return;
        }
        _lblHits.Text = "searching…";
        Task.Run(() =>
        {
            List<SearchHit> hits;
            try { hits = engine.FindAll(query, matchCase: false); }
            catch { return; }
            BeginInvoke(() =>
            {
                if (!ReferenceEquals(_engine, engine))
                    return;
                _hits = hits;
                _lblHits.Text = hits.Count == 0 ? "no results" : "";
                if (hits.Count > 0) GoToHit(0);
                else RenderCurrent();
            });
        });
    }

    private void GoToHit(int index)
    {
        if (_hits.Count == 0 || _engine == null)
            return;
        _hitIndex = ((index % _hits.Count) + _hits.Count) % _hits.Count;
        var hit = _hits[_hitIndex];
        _pageHighlights = hit.Rects.ToArray();
        _lblHits.Text = $"{_hitIndex + 1}/{_hits.Count}";
        if (hit.Rects.Count > 0)
        {
            var info = _engine.GetPageInfo(hit.PageIndex);
            double scale = CurrentScaleFor(info);
            var r = hit.Rects[0];
            _pendingScroll = new Point((int)(r.Left * scale) - 60, (int)((info.DisplayHeightPoints - r.Top) * scale) - 60);
        }
        GoToPage(hit.PageIndex);
    }

    private void BuildThumbnails()
    {
        _thumbs.SuspendLayout();
        foreach (Control c in _thumbs.Controls)
        {
            if (c is Panel p && p.Controls[0] is PictureBox pic)
                pic.Image?.Dispose();
            c.Dispose();
        }
        _thumbs.Controls.Clear();
        _thumbs.ResumeLayout();

        var engine = _engine;
        if (engine == null)
            return;
        int count = engine.PageCount;
        int thumbCount = Math.Min(count, MaxThumbnails);
        Task.Run(() =>
        {
            for (int i = 0; i < thumbCount; i++)
            {
                int page = i;
                PageInfo info = engine.GetPageInfo(page);
                double scale = ThumbWidth / info.DisplayWidthPoints;
                RenderedPage rendered = engine.RenderPage(page, scale);
                BeginInvoke(() =>
                {
                    if (!ReferenceEquals(_engine, engine))
                        return;
                    var bmp = RenderUtil.ToBitmap(rendered);
                    var pic = new PictureBox
                    {
                        Dock = DockStyle.Fill,
                        SizeMode = PictureBoxSizeMode.StretchImage,
                        Image = bmp,
                        Cursor = Cursors.Hand,
                    };
                    var lbl = new Label
                    {
                        Dock = DockStyle.Bottom,
                        Height = 18,
                        Text = (page + 1).ToString(),
                        TextAlign = ContentAlignment.MiddleCenter,
                        Cursor = Cursors.Hand,
                    };
                    var panel = new Panel { Width = ThumbWidth + 10, Height = bmp.Height + 24, Tag = page, Padding = new Padding(4) };
                    panel.Controls.Add(pic);
                    panel.Controls.Add(lbl);
                    pic.Click += (s, e) => GoToPage(page);
                    lbl.Click += (s, e) => GoToPage(page);
                    panel.Click += (s, e) => GoToPage(page);
                    _thumbs.Controls.Add(panel);
                    if (page == _page)
                        SelectThumb(page);
                });
            }
            if (thumbCount < count)
            {
                BeginInvoke(() =>
                {
                    if (ReferenceEquals(_engine, engine))
                        _thumbs.Controls.Add(new Label { Text = $"… {count - thumbCount} more pages", AutoSize = true, Padding = new Padding(6) });
                });
            }
        });
    }

    private void SelectThumb(int page)
    {
        foreach (Control c in _thumbs.Controls)
        {
            if (c.Tag is int p)
            {
                c.BackColor = p == page ? Color.LightSkyBlue : SystemColors.Control;
                if (p == page)
                    _thumbs.ScrollControlIntoView(c);
            }
        }
    }

    private void ToggleThumbnails()
    {
        _settings.ShowThumbnails = !_settings.ShowThumbnails;
        _btnThumbs.Checked = _settings.ShowThumbnails;
        ApplyThumbnailVisibility();
    }

    private void ApplyThumbnailVisibility()
    {
        _split.Panel1Collapsed = !_settings.ShowThumbnails;
    }

    private void RebuildRecentMenu()
    {
        _recentMenu.DropDownItems.Clear();
        var items = _settings.Recent.Where(File.Exists).ToList();
        if (items.Count == 0)
        {
            _recentMenu.DropDownItems.Add(new ToolStripMenuItem("(empty)") { Enabled = false });
            return;
        }
        foreach (var path in items)
        {
            var item = new ToolStripMenuItem(path) { ToolTipText = path };
            item.Click += (s, e) => OpenFile(path);
            _recentMenu.DropDownItems.Add(item);
        }
    }

    private void UpdateUi()
    {
        ApplyThumbnailVisibility();
        RebuildRecentMenu();
        _lblPage.Text = "";
        _lblZoom.Text = $"{(int)Math.Round(_zoom * 100)}%";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F)) { _txtSearch.Focus(); _txtSearch.SelectAll(); return true; }
        if (keyData == Keys.F3) { GoToHit(_hitIndex + 1); return true; }
        if (keyData == (Keys.Shift | Keys.F3)) { GoToHit(_hitIndex - 1); return true; }
        if (_engine != null)
        {
            if (keyData == Keys.PageDown) { GoToPage(_page + 1); return true; }
            if (keyData == Keys.PageUp) { GoToPage(_page - 1); return true; }
            if (keyData == Keys.Home) { GoToPage(0); return true; }
            if (keyData == Keys.End) { GoToPage(_engine.PageCount - 1); return true; }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _renderCts?.Cancel();
        if (_filePath != null)
            _settings.LastPage[_filePath] = _page;
        _settings.Save();
        _engine?.Dispose();
        base.OnFormClosing(e);
    }
}
