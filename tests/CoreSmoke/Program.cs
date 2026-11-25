using PdfReaderCore;

// Headless smoke tests for PdfReaderCore. Usage: CoreSmoke [path-to-sample.pdf]
// Exit code 0 = all passed, 1 = at least one failure.

var sample = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "testdata", "sample.pdf"));

int passed = 0, failed = 0;
void Check(string name, bool ok, string detail = "")
{
    Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? $"  ({detail})" : "")}");
    if (ok) passed++; else failed++;
}

Console.WriteLine($"sample: {sample}");
using var engine = PdfEngine.Open(sample);

Check("open + page count", engine.PageCount == 12, $"pages={engine.PageCount}");
Check("metadata title", engine.Title.Contains("Sample"), $"title='{engine.Title}'");

var info = engine.GetPageInfo(0);
Check("page 0 size ~A4", Math.Abs(info.WidthPoints - 595) < 1 && Math.Abs(info.HeightPoints - 842) < 1,
    $"{info.WidthPoints:0.#}x{info.HeightPoints:0.#} rot={info.Rotation}");

var page1 = engine.RenderPage(0, 1.0);
int dark = 0;
for (int i = 0; i < page1.Bgra.Length; i += 4)
    if (page1.Bgra[i] < 128 && page1.Bgra[i + 1] < 128 && page1.Bgra[i + 2] < 128)
        dark++;
Check("render page 0 @1x dims", Math.Abs(page1.Width - 595) <= 2 && Math.Abs(page1.Height - 842) <= 2,
    $"{page1.Width}x{page1.Height} stride={page1.Stride}");
Check("render page 0 has ink", dark > 5000, $"dark px={dark}");
bool alphaOpaque = true;
for (int i = 3; i < page1.Bgra.Length; i += 4)
    if (page1.Bgra[i] != 255) { alphaOpaque = false; break; }
Check("alpha forced opaque", alphaOpaque);

var half = engine.RenderPage(0, 0.5);
Check("render page 0 @0.5 dims", Math.Abs(half.Width - 298) <= 2 && Math.Abs(half.Height - 421) <= 2,
    $"{half.Width}x{half.Height}");

var hits = engine.FindAll("NEEDLE_MARKER", matchCase: false);
Check("search NEEDLE_MARKER finds 3 pages", hits.Count == 3 && hits.Select(h => h.PageIndex).SequenceEqual(new[] { 3, 7, 11 }),
    string.Join(",", hits.Select(h => $"p{h.PageIndex}:{h.Rects.Count}r")));
Check("search hit rects sane", hits.All(h => h.Rects.All(r => r.Right > r.Left && r.Top > r.Bottom && r.Left >= 0 && r.Top <= 842.5)),
    hits.Count > 0 ? $"first rect={hits[0].Rects[0]}" : "");

var caseSensitive = engine.FindAll("needle_marker", matchCase: true);
var caseInsensitive = engine.FindAll("needle_marker", matchCase: false);
Check("match-case flag works", caseSensitive.Count == 0 && caseInsensitive.Count == 3,
    $"sensitive={caseSensitive.Count} insensitive={caseInsensitive.Count}");

var text = engine.GetPageText(0);
Check("page text extraction", text.Contains("Sample Document - Page 1"), $"len={text.Length}");

var unicodePath = Path.Combine(Path.GetDirectoryName(sample)!, "ünïcødé-têst.pdf");
File.Copy(sample, unicodePath, overwrite: true);
try
{
    using var engine2 = PdfEngine.Open(unicodePath);
    Check("unicode file path opens", engine2.PageCount == 12);
}
catch (Exception ex)
{
    Check("unicode file path opens", false, ex.Message);
}
finally
{
    File.Delete(unicodePath);
}

try
{
    PdfEngine.Open(Path.Combine(Path.GetDirectoryName(sample)!, "does-not-exist.pdf"));
    Check("missing file throws", false, "no exception");
}
catch (Exception)
{
    Check("missing file throws", true);
}

Console.WriteLine($"--- {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
