// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
namespace BoomingStore;
public enum CustomerMood { Happy, Neutral, Disappointed }
/// <summary>Unscaled active queue time. Pausing the game stops this clock.</summary>
public sealed class CustomerMorale
{
    public double WaitSeconds { get; private set; }
    public double Value => Math.Clamp(1 - Math.Max(0, WaitSeconds - 60) / 30, 0, 1);
    public CustomerMood Mood => Value >= 2d / 3 ? CustomerMood.Happy : Value >= 1d / 3 ? CustomerMood.Neutral : CustomerMood.Disappointed;
    public void Advance(double realSeconds, bool queueing)
    { if (queueing && double.IsFinite(realSeconds) && realSeconds > 0) WaitSeconds += realSeconds; }
}
