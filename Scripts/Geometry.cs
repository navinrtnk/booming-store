// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). SPDX-License-Identifier: MIT
using Godot;

namespace BoomingStore;

/// <summary>Small mesh-building vocabulary, in meters. Nodes remain inspectable in the remote scene tree.</summary>
public static class Geometry
{
    private static readonly Dictionary<Vector3, BoxMesh> Boxes = new();
    private static readonly Dictionary<(float, float, float, int), CylinderMesh> Cylinders = new();
    private static readonly Dictionary<(float, float), TorusMesh> Rings = new();
    private static readonly Dictionary<(Vector3, float), ArrayMesh> RoundedMeshes = new();
    private static readonly SphereMesh UnitSphere = new() { Radius = .5f, Height = 1, RadialSegments = 32, Rings = 16 };
    private static readonly Vector3[] FaceNormals = [Vector3.Right, Vector3.Left, Vector3.Up, Vector3.Down, Vector3.Back, Vector3.Forward];

    public static Node3D Group(Node parent, string name, Vector3 position = default)
    {
        var node = new Node3D { Name = name, Position = position };
        parent.AddChild(node);
        return node;
    }

    public static MeshInstance3D Mesh(Node parent, string name, Godot.Mesh mesh, Vector3 position, Material material)
    {
        var node = new MeshInstance3D { Name = name, Mesh = mesh, Position = position, MaterialOverride = material };
        parent.AddChild(node);
        return node;
    }

    public static MeshInstance3D Box(Node parent, string name, Vector3 size, Vector3 position, Material material)
    {
        if (!Boxes.TryGetValue(size, out var mesh)) Boxes[size] = mesh = new BoxMesh { Size = size };
        return Mesh(parent, name, mesh, position, material);
    }

    /// <summary>Physical edge bevels give furniture, steel and ice natural highlight transitions.</summary>
    public static MeshInstance3D RoundedBox(Node parent, string name, Vector3 size, Vector3 position,
        Material material, float radius = .008f)
    {
        radius = Mathf.Min(radius, Mathf.Min(size.X, Mathf.Min(size.Y, size.Z)) * .45f);
        if (!RoundedMeshes.TryGetValue((size, radius), out var mesh))
        {
            var surface = new SurfaceTool();
            surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
            var half = size / 2;
            var inner = half - Vector3.One * radius;
            foreach (var normal in FaceNormals)
            {
                var u = Mathf.Abs(normal.Y) > .5f ? Vector3.Right : Vector3.Up;
                var v = normal.Cross(u);
                var hu = half.Dot(u.Abs());
                var hv = half.Dot(v.Abs());
                var us = Cuts(hu, radius);
                var vs = Cuts(hv, radius);
                for (var x = 0; x < us.Length - 1; x++)
                    for (var y = 0; y < vs.Length - 1; y++)
                    {
                        Add(us[x], vs[y]); Add(us[x], vs[y + 1]); Add(us[x + 1], vs[y]);
                        Add(us[x + 1], vs[y]); Add(us[x], vs[y + 1]); Add(us[x + 1], vs[y + 1]);
                    }
                void Add(float x, float y)
                {
                    var point = normal * half.Dot(normal.Abs()) + u * x + v * y;
                    var core = point.Clamp(-inner, inner);
                    var n = (point - core).Normalized();
                    surface.SetNormal(n);
                    surface.SetUV(new((x + hu) / (2 * hu), (y + hv) / (2 * hv)));
                    surface.AddVertex(core + n * radius);
                }
            }
            surface.GenerateTangents();
            surface.Index();
            mesh = surface.Commit();
            RoundedMeshes[(size, radius)] = mesh;
        }
        return Mesh(parent, name, mesh, position, material);

        static float[] Cuts(float h, float r) => [-h, -h + r * .293f, -h + r, h - r, h - r * .293f, h];
    }

    public static MeshInstance3D Cylinder(Node parent, string name, float bottom, float top, float height,
        Vector3 position, Material material, int segments = 40)
    {
        var key = (bottom, top, height, segments);
        if (!Cylinders.TryGetValue(key, out var mesh)) Cylinders[key] = mesh = new CylinderMesh
        {
            BottomRadius = bottom,
            TopRadius = top,
            Height = height,
            RadialSegments = segments
        };
        return Mesh(parent, name, mesh, position, material);
    }

    public static MeshInstance3D Sphere(Node parent, string name, Vector3 size, Vector3 position, Material material)
    {
        var node = Mesh(parent, name, UnitSphere, position, material);
        node.Scale = size;
        return node;
    }

    public static MeshInstance3D Ring(Node parent, string name, float radius, float tube, Vector3 position, Material material)
    {
        var key = (radius, tube);
        if (!Rings.TryGetValue(key, out var mesh)) Rings[key] = mesh = new TorusMesh
        {
            InnerRadius = radius - tube,
            OuterRadius = radius + tube,
            Rings = 48,
            RingSegments = 12
        };
        return Mesh(parent, name, mesh, position, material);
    }

    public static MeshInstance3D Rod(Node parent, string name, Vector3 from, Vector3 to, float radius, Material material)
    {
        var node = Cylinder(parent, name, radius, radius, from.DistanceTo(to), (from + to) / 2f, material, 16);
        var direction = (to - from).Normalized();
        var side = Mathf.Abs(direction.Dot(Vector3.Forward)) > .99f ? Vector3.Right : Vector3.Forward;
        var x = direction.Cross(side).Normalized();
        node.Basis = new Basis(x, direction, x.Cross(direction).Normalized());
        return node;
    }

    public static Label3D Text(Node parent, string name, string text, Vector3 position, float pixelSize,
        Color color, int fontSize = 48, bool faceBack = false)
    {
        var label = new Label3D
        {
            Name = name,
            Text = text,
            Position = position,
            PixelSize = pixelSize,
            FontSize = fontSize,
            Modulate = color,
            OutlineSize = 0,
            NoDepthTest = false,
            Shaded = false,
            DoubleSided = false,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
        };
        if (faceBack) label.RotationDegrees = new Vector3(0, 180, 0);
        parent.AddChild(label);
        return label;
    }

    /// <summary>A lathed, open vessel with a closed bottom, appropriate for glasses and tools.</summary>
    public static MeshInstance3D Vessel(Node parent, string name, Vector2[] profile, Vector3 position, Material material,
        float sectionExponent = 2, float depthScale = 1)
    {
        var surface = new SurfaceTool();
        surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        const int sides = 96;
        var section = new Vector2[sides + 1];
        for (var i = 0; i <= sides; i++)
        {
            var angle = Mathf.Tau * i / sides;
            var x = Mathf.Cos(angle);
            var z = Mathf.Sin(angle);
            section[i] = new(Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 2 / sectionExponent),
                Mathf.Sign(z) * Mathf.Pow(Mathf.Abs(z), 2 / sectionExponent));
        }
        for (var row = 0; row < profile.Length - 1; row++)
            for (var i = 0; i < sides; i++)
            {
                var a = Point(profile[row], i);
                var b = Point(profile[row], i + 1);
                var c = Point(profile[row + 1], i);
                var d = Point(profile[row + 1], i + 1);
                Add(a, i, row); Add(b, i + 1, row); Add(c, i, row + 1);
                Add(b, i + 1, row); Add(d, i + 1, row + 1); Add(c, i, row + 1);
            }
        surface.GenerateNormals();
        surface.GenerateTangents();
        surface.Index();
        return Mesh(parent, name, surface.Commit(), position, material);

        void Add(Vector3 point, int column, int row)
        {
            surface.SetUV(new(column / (float)sides, row / (float)(profile.Length - 1)));
            surface.AddVertex(point);
        }

        Vector3 Point(Vector2 p, int i) => new(section[i].X * p.X, p.Y, section[i].Y * p.X * depthScale);
    }

    /// <summary>Swept wire for actual spring coils, seam piping and bent hardware.</summary>
    public static MeshInstance3D Tube(Node parent, string name, Vector3[] path, float radius, Material material, int sides = 8)
    {
        var surface = new SurfaceTool();
        surface.Begin(Godot.Mesh.PrimitiveType.Triangles);
        var frames = new (Vector3 Side, Vector3 Up)[path.Length];
        for (var row = 0; row < path.Length; row++)
        {
            var tangent = (path[Math.Min(row + 1, path.Length - 1)] - path[Math.Max(row - 1, 0)]).Normalized();
            var reference = Mathf.Abs(tangent.Dot(Vector3.Up)) > .97f ? Vector3.Right : Vector3.Up;
            var side = tangent.Cross(reference).Normalized();
            frames[row] = (side, tangent.Cross(side).Normalized());
        }
        var section = new Vector2[sides + 1];
        for (var col = 0; col <= sides; col++)
        {
            var angle = col * Mathf.Tau / sides;
            section[col] = new(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        for (var i = 0; i < path.Length - 1; i++)
            for (var j = 0; j < sides; j++)
            {
                Add(i, j); Add(i + 1, j); Add(i, j + 1);
                Add(i + 1, j); Add(i + 1, j + 1); Add(i, j + 1);
            }
        surface.GenerateTangents();
        surface.Index();
        return Mesh(parent, name, surface.Commit(), Vector3.Zero, material);
        void Add(int row, int column)
        {
            var normal = frames[row].Side * section[column].X + frames[row].Up * section[column].Y;
            surface.SetNormal(normal);
            surface.SetUV(new(row / (float)path.Length, column / (float)sides));
            surface.AddVertex(path[row] + normal * radius);
        }
    }
}
