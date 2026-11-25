using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using PdfReaderCore;

namespace PdfReaderLight;

internal static class RenderUtil
{
    public static Bitmap ToBitmap(RenderedPage page)
    {
        var bmp = new Bitmap(page.Width, page.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, page.Width, page.Height);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < page.Height; y++)
                Marshal.Copy(page.Bgra, y * page.Stride, data.Scan0 + y * data.Stride, page.Width * 4);
        }
        finally
        {
            bmp.UnlockBits(data);
        }
        return bmp;
    }

    public static void DrawHighlights(Bitmap bmp, double scale, double displayHeightPoints, IReadOnlyList<RectD> rects)
    {
        using var g = Graphics.FromImage(bmp);
        using var fill = new SolidBrush(Color.FromArgb(96, Color.Orange));
        using var pen = new Pen(Color.FromArgb(220, Color.DarkOrange), 1f);
        foreach (var r in rects)
        {
            float x = (float)(r.Left * scale);
            float y = (float)((displayHeightPoints - r.Top) * scale);
            float w = (float)((r.Right - r.Left) * scale);
            float h = (float)((r.Top - r.Bottom) * scale);
            g.FillRectangle(fill, x, y, w, h);
            g.DrawRectangle(pen, x, y, w, h);
        }
    }
}
