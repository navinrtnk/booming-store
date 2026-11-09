// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
namespace BoomingStore;

/// <summary>A small boutique's irregular foot traffic, with breathing room at the entrance.</summary>
public sealed class CustomerArrivals(Func<float> random)
{
    public const int Capacity = 6;
    private float _remaining = 12;
    public void Reset() => _remaining = Between(12, 18);
    private float Between(float minimum, float maximum) => minimum + Math.Clamp(random(), 0, 1) * (maximum - minimum);
    public bool Tick(float elapsed, int shoppers, bool entranceClear, double hour = 14, double reputation = .5)
    {
        if (elapsed <= 0) return false;
        _remaining -= elapsed * (float)StoreClock.Traffic(hour, reputation);
        if (shoppers >= Capacity || !entranceClear)
        {
            _remaining = Math.Max(_remaining, 4);
            return false;
        }
        if (_remaining > 0) return false;
        _remaining = Between(24, 40);
        return true;
    }
}
