// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using System.Text.Json;

namespace BoomingStore;

public readonly record struct StoreLoadResult(StoreState? State, bool Recovered);

/// <summary>Validated, replace-on-success saves with a previous-good-state backup.</summary>
public static class StorePersistence
{
    public static void Save(string path, StoreState state)
    {
        var json = state.Serialize();
        StoreState.Parse(json); // Never replace a good save with an invalid in-memory state.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, json);
            if (TryRead(path) != null) File.Copy(path, path + ".bak", true);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static StoreLoadResult Load(string path)
    {
        var state = TryRead(path);
        return state != null ? new(state, false) : new(TryRead(path + ".bak"), true);
    }
    private static StoreState? TryRead(string path)
    {
        try { return StoreState.Parse(File.ReadAllText(path)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { return null; }
    }
}
