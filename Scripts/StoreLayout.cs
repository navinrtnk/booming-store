// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Placement and supported-object transforms, independent of rendering.</summary>
public static class StoreLayout
{
    public static bool CanPlace(StoreState state, StoreItem item, int ignore = 0)
    {
        if (!float.IsFinite(item.X) || !float.IsFinite(item.Y) || !float.IsFinite(item.Z) ||
            item.Rotation is < 0 or >= 360 || item.Rotation % 90 != 0 ||
            (ignore == 0 && (state.Items.Count >= 600 || state.NextId == int.MaxValue)) ||
            (item.ParentId == 0 && Math.Abs(item.Y) > .001f)) return false;
        if (item.Kind == "checkout" && state.Items.Count(i => i.Kind == "checkout" && i.Id != ignore) >= 2) return false;
        var h = item.Footprint / 2;
        if (Mathf.Abs(item.X) + h.X > 5.9f || Mathf.Abs(item.Z) + h.Y > 4.9f) return false;
        if (item.ParentId != 0)
        {
            var parent = state.Items.FirstOrDefault(i => i.Id == item.ParentId);
            if (parent == null || parent.ParentId != 0 || parent.Product.Surface <= 0 || !parent.Product.FitsLevel(item.Y, item.Product.Height)) return false;
            var bounds = parent.Footprint / 2;
            if (Mathf.Abs(item.X - parent.X) + h.X > bounds.X || Mathf.Abs(item.Z - parent.Z) + h.Y > bounds.Y) return false;
        }
        if (item.Kind == "rug") return true;
        foreach (var other in state.Items.Where(i => i.Id != ignore && (ignore == 0 || i.ParentId != ignore) && i.Kind != "rug" && i.Id != item.ParentId))
        {
            if (Mathf.Abs(item.Y - other.Y) > .1f) continue;
            var size = (item.Footprint + other.Footprint) / 2;
            if (Mathf.Abs(item.X - other.X) < size.X - .015f && Mathf.Abs(item.Z - other.Z) < size.Y - .015f) return false;
        }
        foreach (var wall in state.Walls)
            if (wall.AlongX ? Mathf.Abs(item.Z - wall.Z) < h.Y + .04f && item.X + h.X > wall.X && item.X - h.X < wall.X + 1
                : Mathf.Abs(item.X - wall.X) < h.X + .04f && item.Z + h.Y > wall.Z && item.Z - h.Y < wall.Z + 1) return false;
        return true;
    }
    public static void MoveItem(StoreState state, StoreItem item, StoreItem destination)
    {
        var old = item.Position; var angle = Mathf.DegToRad(destination.Rotation - item.Rotation);
        foreach (var child in state.Items.Where(i => i.ParentId == item.Id))
        {
            var p = (child.Position - old).Rotated(Vector3.Up, angle) + destination.Position;
            child.X = p.X; child.Z = p.Z; child.Y = p.Y; child.Rotation = (child.Rotation + destination.Rotation - item.Rotation + 360) % 360;
        }
        item.X = destination.X; item.Y = destination.Y; item.Z = destination.Z; item.Rotation = destination.Rotation; item.ParentId = destination.ParentId;
    }
}
