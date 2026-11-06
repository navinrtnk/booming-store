// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>A half-metre grid, independent of render meshes, rebuilt after every layout edit.</summary>
public sealed class ShopNavigation
{
    public const int Width = 24, Depth = 20;
    private static readonly Vector2I[] Directions = [Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down];
    private readonly int[] _parents = new int[Width * Depth];
    private readonly int[] _frontier = new int[Width * Depth];
    private int _searchOrigin = -1;
    private readonly bool[,] _blocked = new bool[Width, Depth];
    private readonly HashSet<(int, int, int, int)> _edges = [];
    public Vector3 Entrance { get; private set; } = new(0, 0, 4.65f);
    public bool HasEntrance { get; private set; }
    public static Vector2I Cell(Vector3 p) => new(Mathf.Clamp((int)Mathf.Floor((p.X + 6) * 2), 0, Width - 1),
        Mathf.Clamp((int)Mathf.Floor((p.Z + 5) * 2), 0, Depth - 1));
    public static Vector3 Point(Vector2I c) => new(c.X * .5f - 5.75f, 0, c.Y * .5f - 4.75f);
    public void Rebuild(StoreState state)
    {
        Array.Clear(_blocked); _edges.Clear(); _searchOrigin = -1;
        foreach (var i in state.Items.Where(i => i.Blocks))
        {
            var size = i.Footprint / 2 + Vector2.One * .16f;
            for (var x = 0; x < Width; x++) for (var z = 0; z < Depth; z++)
            { var p = Point(new(x, z)); if (Mathf.Abs(p.X - i.X) < size.X && Mathf.Abs(p.Z - i.Z) < size.Y) _blocked[x, z] = true; }
        }
        foreach (var w in state.Walls.Where(w => w.Kind != "door"))
            for (var n = 0; n < 2; n++)
            {
                var x = (w.X + 6) * 2; var z = (w.Z + 5) * 2;
                if (w.AlongX) AddEdge(x + n, z - 1, x + n, z);
                else AddEdge(x - 1, z + n, x, z + n);
            }
        var entries = new List<Vector3>();
        for (var x = -6; x < 6; x++)
            foreach (var z in new[] { -5, 5 })
                if (!state.Walls.Any(w => w.AlongX && w.X == x && w.Z == z && w.Kind != "door")) entries.Add(new(x + .5f, 0, z > 0 ? 4.65f : -4.65f));
        for (var z = -5; z < 5; z++)
            foreach (var x in new[] { -6, 6 })
                if (!state.Walls.Any(w => !w.AlongX && w.X == x && w.Z == z && w.Kind != "door")) entries.Add(new(x > 0 ? 5.65f : -5.65f, 0, z + .5f));
        var entrance = entries.OrderBy(p => p.DistanceSquaredTo(new(0, 0, 5)))
            .Cast<Vector3?>().FirstOrDefault(p => !_blocked[Cell(p!.Value).X, Cell(p.Value).Y]);
        HasEntrance = entrance != null;
        Entrance = entrance ?? new(0, 0, 4.65f);
    }
    private void AddEdge(int a, int b, int c, int d) { _edges.Add((a, b, c, d)); _edges.Add((c, d, a, b)); }
    private static bool InLot(Vector3 p) => p.IsFinite() && p.X >= -6 && p.X < 6 && p.Z >= -5 && p.Z < 5;
    private static int Index(Vector2I cell) => cell.Y * Width + cell.X;
    private bool Search(Vector3 from)
    {
        if (!InLot(from)) return false;
        var start = Cell(from);
        if (_blocked[start.X, start.Y]) return false;
        var origin = Index(start);
        if (_searchOrigin == origin) return true;
        // One breadth-first traversal answers every display approach query from this cell.
        Array.Fill(_parents, -1);
        _searchOrigin = origin;
        var head = 0; var tail = 0;
        _frontier[tail++] = origin; _parents[origin] = origin;
        while (head < tail)
        {
            var current = _frontier[head++];
            var p = new Vector2I(current % Width, current / Width);
            foreach (var direction in Directions)
            {
                var q = p + direction;
                if (q.X < 0 || q.Y < 0 || q.X >= Width || q.Y >= Depth || _blocked[q.X, q.Y] ||
                    _parents[Index(q)] != -1 || _edges.Contains((p.X, p.Y, q.X, q.Y))) continue;
                _parents[Index(q)] = current; _frontier[tail++] = Index(q);
            }
        }
        return true;
    }
    public bool CanReach(Vector3 from, Vector3 to) => InLot(to) && Search(from) && _parents[Index(Cell(to))] != -1;
    public List<Vector3>? Path(Vector3 from, Vector3 to)
    {
        if (!CanReach(from, to)) return null;
        var path = new List<Vector3>();
        for (var cell = Index(Cell(to)); cell != _searchOrigin; cell = _parents[cell])
            path.Add(Point(new(cell % Width, cell / Width)));
        path.Reverse(); return path;
    }
    public Vector3? Approach(StoreItem item, Vector3 from)
    {
        if (!Search(from)) return null;
        var h = item.Footprint / 2;
        ReadOnlySpan<Vector3> options = item.ParentId != 0
            ? [new(item.X, 0, item.Z + .9f), new(item.X, 0, item.Z - .9f), new(item.X + 1.4f, 0, item.Z), new(item.X - 1.4f, 0, item.Z)]
            : [new(item.X, 0, item.Z + h.Y + .5f), new(item.X + h.X + .5f, 0, item.Z),
                new(item.X, 0, item.Z - h.Y - .5f), new(item.X - h.X - .5f, 0, item.Z)];
        Vector3? best = null;
        var nearest = float.PositiveInfinity;
        foreach (var option in options)
        {
            if (Mathf.Abs(option.X) >= 5.7f || Mathf.Abs(option.Z) >= 4.8f) continue;
            var point = Point(Cell(option));
            var distance = from.DistanceSquaredTo(point);
            if (_parents[Index(Cell(point))] == -1 || distance >= nearest) continue;
            best = point; nearest = distance;
        }
        return best;
    }
}
