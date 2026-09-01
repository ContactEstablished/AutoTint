using System;

namespace AutoTint.Services;

/// <summary>
/// How bright a captured region is. Built from a 256-bin histogram, so the pixels
/// themselves are needed only for the instant it takes to count them.
/// </summary>
internal readonly struct LuminanceStats
{
    private readonly int[] _histogram;
    private readonly int _total;

    private LuminanceStats(int[] histogram, int total, double mean)
    {
        _histogram = histogram;
        _total = total;
        Mean = mean;
    }

    /// <summary>Average brightness across the region, 0–255.</summary>
    internal double Mean { get; }

    internal bool IsEmpty => _total == 0;

    /// <summary>
    /// Counts BGRA pixels into a histogram of perceived brightness.
    ///
    /// Luma is the gamma-space 0.299/0.587/0.114 weighting rather than a linear-light one.
    /// That is deliberate: alpha compositing happens in encoded sRGB, so measuring in the
    /// same space is what makes a predicted result match the one that actually renders.
    /// </summary>
    internal static LuminanceStats From(ReadOnlySpan<byte> bgra)
    {
        var histogram = new int[256];
        int count = 0;
        long sum = 0;

        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            int luma = ((299 * bgra[i + 2]) + (587 * bgra[i + 1]) + (114 * bgra[i])) / 1000;
            histogram[luma]++;
            sum += luma;
            count++;
        }

        double mean = count == 0 ? 0 : (double)sum / count;
        return new LuminanceStats(histogram, count, mean);
    }

    /// <summary>
    /// The brightness level that the given fraction of the area falls below. The 90th
    /// percentile is "how bright are the bright parts" -- the same idea as measuring how
    /// much of the area is white, expressed so it can drive the compositing arithmetic.
    /// A mostly-dark window holding one blazing document reads as bright here, where a
    /// plain average would wash it out.
    /// </summary>
    internal int Percentile(double fraction)
    {
        if (_total == 0 || _histogram is null) return 0;

        int wanted = (int)Math.Ceiling(_total * Math.Clamp(fraction, 0, 1));
        if (wanted <= 0) wanted = 1;

        int running = 0;
        for (int level = 0; level < _histogram.Length; level++)
        {
            running += _histogram[level];
            if (running >= wanted) return level;
        }

        return 255;
    }

    /// <summary>Share of the area brighter than <paramref name="threshold"/>, 0–1.</summary>
    internal double FractionAbove(int threshold)
    {
        if (_total == 0 || _histogram is null) return 0;

        int above = 0;
        for (int level = Math.Clamp(threshold + 1, 0, 255); level < _histogram.Length; level++)
        {
            above += _histogram[level];
        }

        return (double)above / _total;
    }
}
