namespace Marker.Client.Networking;

public sealed class ResilienceOptions
{
    public TimeSpan InitialDelay { get; set; }
    public TimeSpan MaxDelay { get; set; }
    public double Multiplier { get; set; }
    public int? MaxAttempts { get; set; }

    public TimeSpan? GetDelay(int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        if (attempt > MaxAttempts) return null;

        var ms = InitialDelay.TotalMilliseconds * Math.Pow(Multiplier, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(ms, MaxDelay.TotalMilliseconds));
    }

    internal void Validate()
    {
        if (InitialDelay <= TimeSpan.Zero)
            throw new InvalidOperationException($"{nameof(InitialDelay)} must be positive.");
        if (MaxDelay < InitialDelay)
            throw new InvalidOperationException($"{nameof(MaxDelay)} must not be less than {nameof(InitialDelay)}.");
        if (Multiplier < 1)
            throw new InvalidOperationException($"{nameof(Multiplier)} must be at least 1.");
        if (MaxAttempts < 0)
            throw new InvalidOperationException($"{nameof(MaxAttempts)} must not be negative.");
    }
}