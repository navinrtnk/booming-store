// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using System.Text.Json;

namespace BoomingStore;

/// <summary>Local presentation preferences, independent of store finances and save history.</summary>
public sealed class GamePreferences
{
    public bool Fullscreen { get; set; }
    public bool Muted { get; set; }
    public float Master { get; set; } = .8f;
    public float Voices { get; set; } = .8f;
    public float Effects { get; set; } = .7f;
    public float Music { get; set; } = .35f;
    public static GamePreferences Parse(string json)
    {
        var value = JsonSerializer.Deserialize<GamePreferences>(json) ?? throw new InvalidDataException("Empty preferences.");
        foreach (var volume in new[] { value.Master, value.Voices, value.Effects, value.Music })
            if (!float.IsFinite(volume) || volume < 0 || volume > 1) throw new InvalidDataException("Invalid volume.");
        return value;
    }
    public static GamePreferences Load(string path)
    {
        try { return Parse(File.ReadAllText(path)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException) { return new(); }
    }
    public void Save(string path)
    {
        var json = JsonSerializer.Serialize(this); Parse(json);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", json); File.Move(path + ".tmp", path, true);
    }
}
