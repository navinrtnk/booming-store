// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
namespace BoomingStore;

public static class StoreClock
{
    public const double SecondsPerHour = 120;
    public static double Hour(StoreState state) => state.WorldHours % 24;
    public static string Time(StoreState state)
    {
        var minutes = (int)Math.Floor(Hour(state) * 60);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }
    public static double Reputation(StoreState state) => state.Departures.Count == 0 ? .5 :
        state.Departures.Average(m => m == CustomerMood.Happy ? 1 : m == CustomerMood.Neutral ? .5 : 0);
    public static void RecordDeparture(StoreState state, CustomerMood mood)
    {
        state.Departures.Add(mood);
        if (state.Departures.Count > 20) state.Departures.RemoveAt(0);
    }
    public static double Traffic(double hour, double reputation)
    {
        hour = (hour % 24 + 24) % 24;
        var time = hour < 6 || hour >= 22 ? .12 : hour < 12 ? .65 : hour < 17 ? 1.5 : 1.8;
        return time * (.5 + Math.Clamp(reputation, 0, 1));
    }
}
