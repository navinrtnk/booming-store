// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Any-angle routes with body clearance, connected regions and Euclidean travel costs.</summary>
public sealed class ShopNavigation
{
    public int Revision { get; private set; }
    public const int Width = 24, Depth = 20;
    public const float Clearance = .20f;
    private const int Count = Width * Depth;
    private static readonly Vector2I[] Directions = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1), new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)];
    private readonly List<Rect2> _obstacles = [];
    private readonly int[] _links = new int[Count], _regions = new int[Count];
    private readonly bool[] _blocked = new bool[Count];
    private Vector3? _searchFrom;
    private readonly HashSet<int> _sourceRegions = [];
    public Vector3 Entrance { get; private set; } = new(0, 0, 4.65f);
    public bool HasEntrance { get; private set; }
    public static Vector2I Cell(Vector3 p) => new(Mathf.Clamp((int)Mathf.Floor((p.X + 6) * 2), 0, Width - 1),
        Mathf.Clamp((int)Mathf.Floor((p.Z + 5) * 2), 0, Depth - 1));
    public static Vector3 Point(Vector2I c) => new(c.X * .5f - 5.75f, 0, c.Y * .5f - 4.75f);
    private static int Index(Vector2I c) => c.Y * Width + c.X;
    private static Vector3 Point(int index) => Point(new Vector2I(index % Width, index / Width));
    private static bool InLot(Vector3 p) => p.IsFinite() && p.X >= -6 && p.X < 6 && p.Z >= -5 && p.Z < 5;

    public void Rebuild(StoreState state)
    {
        Revision++; _searchFrom = null; _obstacles.Clear(); Array.Clear(_links); Array.Fill(_regions, -1);
        foreach (var item in state.Items.Where(i => i.Blocks))
        {
            var half = item.Footprint / 2 + Vector2.One * Clearance;
            _obstacles.Add(new(new Vector2(item.X, item.Z) - half, half * 2));
        }
        foreach (var wall in state.Walls.Where(w => w.Kind != "door"))
        {
            var padding = Vector2.One * (Clearance + .045f);
            _obstacles.Add(new(new Vector2(wall.X, wall.Z) - padding,
                (wall.AlongX ? Vector2.Right : Vector2.Down) + padding * 2));
        }
        for (var i = 0; i < Count; i++) _blocked[i] = !CanStand(Point(i));
        for (var i = 0; i < Count; i++)
        {
            if (_blocked[i]) continue;
            var cell = new Vector2I(i % Width, i / Width);
            for (var d = 0; d < Directions.Length; d++)
            {
                var q = cell + Directions[d];
                if (q.X < 0 || q.X >= Width || q.Y < 0 || q.Y >= Depth || _blocked[Index(q)]) continue;
                if (IsSegmentClear(Point(i), Point(q))) _links[i] |= 1 << d;
            }
        }
        var frontier = new Queue<int>(); var region = 0;
        for (var i = 0; i < Count; i++)
        {
            if (_blocked[i] || _regions[i] >= 0) continue;
            frontier.Enqueue(i); _regions[i] = region++;
            while (frontier.TryDequeue(out var current))
                foreach (var next in Neighbors(current))
                    if (_regions[next] < 0) { _regions[next] = _regions[current]; frontier.Enqueue(next); }
        }
        var entries = new List<Vector3>();
        for (var x = -6; x < 6; x++) foreach (var z in new[] { -5, 5 })
            if (!state.Walls.Any(w => w.AlongX && w.X == x && w.Z == z && w.Kind != "door")) entries.Add(new(x + .5f, 0, z > 0 ? 4.65f : -4.65f));
        for (var z = -5; z < 5; z++) foreach (var x in new[] { -6, 6 })
            if (!state.Walls.Any(w => !w.AlongX && w.X == x && w.Z == z && w.Kind != "door")) entries.Add(new(x > 0 ? 5.65f : -5.65f, 0, z + .5f));
        var entrance = entries.OrderBy(p => p.DistanceSquaredTo(new(0, 0, 5))).Cast<Vector3?>().FirstOrDefault(p => CanStand(p!.Value));
        HasEntrance = entrance != null; Entrance = entrance ?? new(0, 0, 4.65f);
    }

    public bool CanStand(Vector3 point) => InLot(point) && !_obstacles.Any(r =>
        point.X > r.Position.X + .0001f && point.X < r.End.X - .0001f && point.Z > r.Position.Y + .0001f && point.Z < r.End.Y - .0001f);

    /// <summary>Sweep the person's footprint along an entire segment, including walls and furniture.</summary>
    public bool IsSegmentClear(Vector3 from, Vector3 to)
    {
        if (!InLot(from) || !InLot(to)) return false;
        var a = new Vector2(from.X, from.Z); var delta = new Vector2(to.X - from.X, to.Z - from.Z);
        foreach (var box in _obstacles)
        {
            var near = 0f; var far = 1f;
            for (var axis = 0; axis < 2; axis++)
            {
                var min = box.Position[axis] + .0001f; var max = box.End[axis] - .0001f;
                if (Mathf.Abs(delta[axis]) < .00001f)
                { if (a[axis] <= min || a[axis] >= max) { far = -1; break; } continue; }
                var first = (min - a[axis]) / delta[axis]; var last = (max - a[axis]) / delta[axis];
                near = Mathf.Max(near, Mathf.Min(first, last)); far = Mathf.Min(far, Mathf.Max(first, last));
                if (near > far) break;
            }
            if (near <= far) return false;
        }
        return true;
    }
    public bool CanStep(Vector3 from, Vector3 to) => IsSegmentClear(from, to);
    private IEnumerable<int> Neighbors(int index)
    {
        var cell = new Vector2I(index % Width, index / Width);
        for (var d = 0; d < Directions.Length; d++) if ((_links[index] & (1 << d)) != 0) yield return Index(cell + Directions[d]);
    }
    private IEnumerable<int> Connections(Vector3 point)
    {
        var cell = Cell(point);
        for (var z = Math.Max(0, cell.Y - 2); z <= Math.Min(Depth - 1, cell.Y + 2); z++)
            for (var x = Math.Max(0, cell.X - 2); x <= Math.Min(Width - 1, cell.X + 2); x++)
            {
                var i = z * Width + x;
                if (!_blocked[i] && IsSegmentClear(point, Point(i))) yield return i;
            }
    }
    public bool CanReach(Vector3 from, Vector3 to)
    {
        if (!CanStand(from) || !CanStand(to)) return false;
        if (IsSegmentClear(from, to)) return true;
        if (_searchFrom != from)
        { _searchFrom = from; _sourceRegions.Clear(); foreach (var i in Connections(from)) _sourceRegions.Add(_regions[i]); }
        return Connections(to).Any(i => _sourceRegions.Contains(_regions[i]));
    }

    /// <summary>Theta* uses visible ancestors and Euclidean costs, then removes redundant turns.</summary>
    public List<Vector3>? Path(Vector3 from, Vector3 to)
    {
        if (!CanStand(from) || !CanStand(to)) return null;
        if (IsSegmentClear(from, to)) return from.DistanceTo(to) < .001f ? [] : [to];
        if (!CanReach(from, to)) return null;
        var costs = Enumerable.Repeat(float.PositiveInfinity, Count).ToArray();
        var parents = Enumerable.Repeat(-1, Count).ToArray(); var closed = new bool[Count];
        var open = new PriorityQueue<int, float>();
        foreach (var seed in Connections(from))
        { costs[seed] = from.DistanceTo(Point(seed)); open.Enqueue(seed, costs[seed] + Point(seed).DistanceTo(to)); }
        var best = float.PositiveInfinity; var end = -1;
        while (open.TryDequeue(out var current, out var priority))
        {
            if (priority >= best) break;
            if (closed[current]) continue; closed[current] = true;
            var p = Point(current);
            if (IsSegmentClear(p, to)) { best = costs[current] + p.DistanceTo(to); end = current; }
            foreach (var next in Neighbors(current))
            {
                if (closed[next]) continue;
                var ancestor = parents[current]; var origin = ancestor < 0 ? from : Point(ancestor);
                var nextPoint = Point(next); var visible = IsSegmentClear(origin, nextPoint);
                var parent = visible ? ancestor : current;
                var cost = visible ? (ancestor < 0 ? 0 : costs[ancestor]) + origin.DistanceTo(nextPoint) : costs[current] + p.DistanceTo(nextPoint);
                if (cost >= costs[next]) continue;
                parents[next] = parent; costs[next] = cost; open.Enqueue(next, cost + nextPoint.DistanceTo(to));
            }
        }
        if (end < 0) return null;
        var route = new List<Vector3> { to };
        for (var i = end; i >= 0; i = parents[i]) route.Add(Point(i));
        route.Reverse();
        var result = new List<Vector3>(); var anchor = from; var index = 0;
        while (index < route.Count)
        {
            var farthest = route.Count - 1;
            while (farthest > index && !IsSegmentClear(anchor, route[farthest])) farthest--;
            anchor = route[farthest]; result.Add(anchor); index = farthest + 1;
        }
        return result;
    }

    public Vector3? Approach(StoreItem item, Vector3 from)
    {
        var h = item.Footprint / 2;
        ReadOnlySpan<Vector3> options = item.ParentId != 0
            ? [new(item.X, 0, item.Z + .9f), new(item.X, 0, item.Z - .9f), new(item.X + 1.4f, 0, item.Z), new(item.X - 1.4f, 0, item.Z)]
            : [new(item.X, 0, item.Z + h.Y + .5f), new(item.X + h.X + .5f, 0, item.Z), new(item.X, 0, item.Z - h.Y - .5f), new(item.X - h.X - .5f, 0, item.Z)];
        Vector3? best = null; var shortest = float.PositiveInfinity;
        foreach (var point in options)
        {
            var path = Path(from, point); if (path == null) continue;
            var length = 0f; var previous = from;
            foreach (var step in path) { length += previous.DistanceTo(step); previous = step; }
            if (length >= shortest) continue;
            best = point; shortest = length;
        }
        return best;
    }
}
