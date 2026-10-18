using System;

namespace HighlightBoxPlaces;

internal enum FillState { Empty, Partial, Full }

internal static class StockRules
{
    internal static int Capacity(float length, float width, float height, float x, float y, float z, bool stackable)
    {
        if (!Valid(length) || !Valid(width) || !Valid(height) || !Valid(x) || !Valid(y) || !Valid(z)) return 0;
        return Axis(length, x) * Axis(width, z) * (stackable ? Axis(height, y) : 1);
    }

    private static bool Valid(float value) { return value > 0f && !float.IsInfinity(value) && !float.IsNaN(value); }
    private static int Axis(float size, float product) { return Math.Max(1, Math.Min(100, (int)Math.Floor(size / (product * 1.1f)))); }
    internal static FillState State(int quantity, int capacity)
    {
        return quantity <= 0 ? FillState.Empty : quantity >= capacity ? FillState.Full : FillState.Partial;
    }
}
