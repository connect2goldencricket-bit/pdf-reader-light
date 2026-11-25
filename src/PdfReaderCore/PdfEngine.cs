using System.Runtime.InteropServices;
using System.Text;
using PDFiumCore;

namespace PdfReaderCore;

/// <summary>
/// Thin, thread-safe wrapper over PDFium (via PdfiumCore). Renders pages to raw BGRA
/// buffers and runs full-text search. Contains no UI dependencies, so it runs and is
/// tested headlessly on any OS (the PdfiumCore package ships native binaries for
/// win-x64/win-x86/linux-x64/osx-x64).
/// </summary>
public sealed class PdfEngine : IDisposable
{
    private const int FPDF_ANNOT = 0x01;
    private const uint FPDF_MATCHCASE = 0x01;

    private static readonly object s_initGate = new();
    private static bool s_initialized;

    private readonly object _gate = new();
    private FpdfDocumentT _doc;
    private bool _disposed;

    public string FilePath { get; }
    public int PageCount { get; }
    public string Title { get; }

    private PdfEngine(string filePath, FpdfDocumentT doc)
    {
        FilePath = filePath;
        _doc = doc;
        PageCount = fpdfview.FPDF_GetPageCount(doc);
        Title = GetMetadata("Title");
    }

    public static PdfEngine Open(string path)
    {
        lock (s_initGate)
        {
            if (!s_initialized)
            {
                fpdfview.FPDF_InitLibrary();
                s_initialized = true;
            }
        }
        var doc = fpdfview.FPDF_LoadDocument(path, null!);
        if (doc.__Instance == IntPtr.Zero)
        {
            throw new InvalidDataException(
                $"Could not open '{path}' (FPDF error {fpdfview.FPDF_GetLastError()}). " +
                "The file may be encrypted, corrupted, or not a PDF.");
        }
        return new PdfEngine(path, doc);
    }

    public PageInfo GetPageInfo(int index)
    {
        lock (_gate)
        {
            var page = fpdfview.FPDF_LoadPage(_doc, index);
            try
            {
                return new PageInfo
                {
                    Index = index,
                    WidthPoints = fpdfview.FPDF_GetPageWidthF(page),
                    HeightPoints = fpdfview.FPDF_GetPageHeightF(page),
                    Rotation = fpdf_edit.FPDFPageGetRotation(page),
                };
            }
            finally
            {
                fpdfview.FPDF_ClosePage(page);
            }
        }
    }

    /// <summary>Renders page <paramref name="index"/> at <paramref name="scale"/> (1.0 = 72 dpi) to BGRA pixels on a white background.</summary>
    public RenderedPage RenderPage(int index, double scale)
    {
        lock (_gate)
        {
            var page = fpdfview.FPDF_LoadPage(_doc, index);
            try
            {
                int rotation = fpdf_edit.FPDFPageGetRotation(page);
                double w = fpdfview.FPDF_GetPageWidthF(page);
                double h = fpdfview.FPDF_GetPageHeightF(page);
                double dispW = rotation is 1 or 3 ? h : w;
                double dispH = rotation is 1 or 3 ? w : h;
                int bw = Math.Max(1, (int)Math.Round(dispW * scale));
                int bh = Math.Max(1, (int)Math.Round(dispH * scale));

                var bmp = fpdfview.FPDFBitmapCreate(bw, bh, 0);
                if (bmp.__Instance == IntPtr.Zero)
                    throw new OutOfMemoryException($"PDFium could not allocate a {bw}x{bh} bitmap.");
                try
                {
                    fpdfview.FPDFBitmapFillRect(bmp, 0, 0, bw, bh, 0xFFFFFFFFu);
                    fpdfview.FPDF_RenderPageBitmap(bmp, page, 0, 0, bw, bh, rotation, FPDF_ANNOT);
                    int stride = fpdfview.FPDFBitmapGetStride(bmp);
                    var data = new byte[stride * bh];
                    Marshal.Copy(fpdfview.FPDFBitmapGetBuffer(bmp), data, 0, data.Length);
                    for (int i = 3; i < data.Length; i += 4)
                        data[i] = 255;
                    return new RenderedPage { Bgra = data, Width = bw, Height = bh, Stride = stride };
                }
                finally
                {
                    fpdfview.FPDFBitmapDestroy(bmp);
                }
            }
            finally
            {
                fpdfview.FPDF_ClosePage(page);
            }
        }
    }

    /// <summary>Finds all occurrences of <paramref name="query"/>; rects are in page coordinates.</summary>
    public List<SearchHit> FindAll(string query, bool matchCase)
    {
        var hits = new List<SearchHit>();
        if (string.IsNullOrEmpty(query))
            return hits;

        var chars = new ushort[query.Length + 1];
        for (int i = 0; i < query.Length; i++)
            chars[i] = query[i];
        uint flags = matchCase ? FPDF_MATCHCASE : 0u;

        lock (_gate)
        {
            for (int p = 0; p < PageCount; p++)
            {
                var page = fpdfview.FPDF_LoadPage(_doc, p);
                try
                {
                    var tp = fpdf_text.FPDFTextLoadPage(page);
                    try
                    {
                        var rects = new List<RectD>();
                        var handle = fpdf_text.FPDFTextFindStart(tp, ref chars[0], flags, 0);
                        if (handle.__Instance != IntPtr.Zero)
                        {
                            do
                            {
                                int startIndex = fpdf_text.FPDFTextGetSchResultIndex(handle);
                                int count = fpdf_text.FPDFTextGetSchCount(handle);
                                int rectCount = fpdf_text.FPDFTextCountRects(tp, startIndex, count);
                                for (int r = 0; r < rectCount; r++)
                                {
                                    double left = 0, top = 0, right = 0, bottom = 0;
                                    fpdf_text.FPDFTextGetRect(tp, r, ref left, ref top, ref right, ref bottom);
                                    rects.Add(new RectD(left, Math.Min(top, bottom), right, Math.Max(top, bottom)));
                                }
                            }
                            while (fpdf_text.FPDFTextFindNext(handle) != 0);
                            fpdf_text.FPDFTextFindClose(handle);
                        }
                        if (rects.Count > 0)
                            hits.Add(new SearchHit { PageIndex = p, Rects = rects });
                    }
                    finally
                    {
                        fpdf_text.FPDFTextClosePage(tp);
                    }
                }
                finally
                {
                    fpdfview.FPDF_ClosePage(page);
                }
            }
        }
        return hits;
    }

    public string GetPageText(int index)
    {
        lock (_gate)
        {
            var page = fpdfview.FPDF_LoadPage(_doc, index);
            try
            {
                var tp = fpdf_text.FPDFTextLoadPage(page);
                try
                {
                    int count = fpdf_text.FPDFTextCountChars(tp);
                    if (count <= 0)
                        return string.Empty;
                    var buf = new ushort[count + 1];
                    int written = fpdf_text.FPDFTextGetText(tp, 0, count, ref buf[0]);
                    var chars = new char[written];
                    for (int i = 0; i < written; i++)
                        chars[i] = (char)buf[i];
                    return new string(chars).TrimEnd('\0');
                }
                finally
                {
                    fpdf_text.FPDFTextClosePage(tp);
                }
            }
            finally
            {
                fpdfview.FPDF_ClosePage(page);
            }
        }
    }

    public string GetMetadata(string tag)
    {
        lock (_gate)
        {
            uint len = fpdf_doc.FPDF_GetMetaText(_doc, tag, IntPtr.Zero, 0);
            if (len <= 2)
                return string.Empty;
            IntPtr buf = Marshal.AllocHGlobal((int)len);
            try
            {
                fpdf_doc.FPDF_GetMetaText(_doc, tag, buf, len);
                var bytes = new byte[len];
                Marshal.Copy(buf, bytes, 0, bytes.Length);
                return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        lock (_gate)
        {
            if (_doc.__Instance != IntPtr.Zero)
            {
                fpdfview.FPDF_CloseDocument(_doc);
                _doc = null!;
            }
        }
    }
}
