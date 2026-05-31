namespace Sts2Emulator.Core;

/// <summary>
/// Wraps System.Random and counts total calls to Next() so the caller can sync
/// the Python-side shuffle RNG after mid-combat discard reshuffles.
/// </summary>
public sealed class CountingRandom : Random
{
    public int CallCount { get; private set; }

    public CountingRandom(int seed)
        : base(seed) { }

    public override int Next(int maxValue)
    {
        CallCount++;
        return base.Next(maxValue);
    }

    public override int Next()
    {
        CallCount++;
        return base.Next();
    }

    public override int Next(int minValue, int maxValue)
    {
        CallCount++;
        return base.Next(minValue, maxValue);
    }
}
