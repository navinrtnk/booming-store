// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using System.Text.Json.Serialization;
using Godot;

namespace BoomingStore;

public enum KeeperCommandKind { Walk, Restock, Checkout, Stop }

/// <summary>The newest player instruction, held until the current customer or delivery finishes.</summary>
public sealed record KeeperCommand(KeeperCommandKind Kind, float X = 0, float Z = 0, int ItemId = 0)
{
    [JsonIgnore] public Vector3 Position => new(X, 0, Z);
    [JsonIgnore] public string Description => Kind switch
    { KeeperCommandKind.Walk => "walk to your chosen spot", KeeperCommandKind.Restock => "restock your chosen display", KeeperCommandKind.Checkout => "work at checkout", _ => "stop" };
}
