using System;

namespace Ydb.Showcase;

public class DelayProvider
{
    private readonly Func<double> _getDelayMs;

    public DelayProvider(double minDelayMs, double maxDelayMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minDelayMs);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDelayMs);

        _getDelayMs = Math.Abs(minDelayMs - maxDelayMs) < 0.01D
            ? () => minDelayMs
            : () => minDelayMs + Random.Shared.NextDouble() * (maxDelayMs - minDelayMs);
    }

    public TimeSpan GetDelay() => TimeSpan.FromMilliseconds(_getDelayMs());
}