namespace PdfReaderCore;

/// <summary>Rectangle in PDF page coordinates (points, origin bottom-left, y-axis up).</summary>
public readonly record struct RectD(double Left, double Bottom, double Right, double Top);

public sealed class PageInfo
{
    public required int Index { get; init; }
    public required double WidthPoints { get; init; }
    public required double HeightPoints { get; init; }
    public required int Rotation { get; init; }

    public double DisplayWidthPoints => Rotation is 1 or 3 ? HeightPoints : WidthPoints;
    public double DisplayHeightPoints => Rotation is 1 or 3 ? WidthPoints : HeightPoints;
}

/// <summary>A rendered page as a raw BGRA pixel buffer (stride may exceed Width*4). Alpha is always 255.</summary>
public sealed class RenderedPage
{
    public required byte[] Bgra { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Stride { get; init; }
}

public sealed class SearchHit
{
    public required int PageIndex { get; init; }
    public required List<RectD> Rects { get; init; }
}
