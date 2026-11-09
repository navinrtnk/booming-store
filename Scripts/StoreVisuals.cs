// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;
using static BoomingStore.Geometry;

namespace BoomingStore;

/// <summary>Original furniture geometry and a shared PBR palette. All dimensions are metres.</summary>
public sealed class StoreVisuals
{
    public readonly StandardMaterial3D Oak = Texture("white_oak_veneer", "c9ac83");
    public readonly StandardMaterial3D Floor = Texture("wood_floor", "c9b38d");
    public readonly StandardMaterial3D Stone = Texture("marble_01", "e2dfd4");
    public readonly StandardMaterial3D Plaster = Texture("white_plaster_02", "e4ddcd");
    public readonly StandardMaterial3D Brass = Mat("b89a61", .28f, .72f);
    public readonly StandardMaterial3D Dark = Mat("293d36", .7f);
    public readonly StandardMaterial3D Linen = Mat("ede1c7", .92f);
    public readonly StandardMaterial3D Soil = Mat("3b3026");
    public readonly StandardMaterial3D Leaf = Mat("426348", .7f);
    public readonly StandardMaterial3D Glass = new() { AlbedoColor = new Color(.7f, .85f, .85f, .16f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = .12f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
    private readonly Dictionary<int, StandardMaterial3D> _colors = [];
    public static StandardMaterial3D Mat(string hex, float roughness = .65f, float metallic = 0) =>
        new() { AlbedoColor = new Color(hex), Roughness = roughness, Metallic = metallic };
    private static StandardMaterial3D Texture(string folder, string color) => new()
    {
        AlbedoColor = new(color), AlbedoTexture = GD.Load<Texture2D>($"res://Assets/Materials/{folder}/albedo.jpg"),
        NormalEnabled = true, NormalTexture = GD.Load<Texture2D>($"res://Assets/Materials/{folder}/normal.jpg"), NormalScale = .6f,
        RoughnessTexture = GD.Load<Texture2D>($"res://Assets/Materials/{folder}/roughness.jpg"),
        RoughnessTextureChannel = BaseMaterial3D.TextureChannel.Red, Roughness = .85f,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic
    };
    public StandardMaterial3D Color(int index)
    {
        if (!_colors.TryGetValue(index, out var m)) _colors[index] = m = Mat(Catalog.Colors[index], .6f);
        return m;
    }

    public void BuildSurroundings(Node3D root)
    {
        Box(root, "Garden", new(60, .18f, 60), new(0, -.3f, 0), Mat("71806a"));
        RoundedBox(root, "Foundation", new(12.4f, .3f, 10.4f), new(0, -.18f, 0), Mat("d5cdb9"), .06f);
        Box(root, "Sidewalk", new(22, .12f, 2.5f), new(0, -.16f, 6.5f), Mat("c9c5b9"));
        Box(root, "Street", new(60, .08f, 5), new(0, -.22f, 10), Mat("686e6c"));
        for (var x = -16; x <= 16; x += 4)
            Box(root, "RoadMarking", new(1.8f, .01f, .09f), new(x, -.17f, 10), Linen);
        for (var x = -9; x <= 9; x += 18)
        {
            RoundedBox(root, "OutdoorPlanter", new(1.3f, .65f, 1.3f), new(x, .16f, 5.6f), Mat("c4b59d"), .05f);
            for (var n = 0; n < 9; n++)
                Sphere(root, "Shrub", new(.7f, .7f, .7f), new(x + Mathf.Sin(n * 2.4f) * .35f, .65f + n % 2 * .2f, 5.6f + Mathf.Cos(n * 2.4f) * .35f), Leaf);
        }
        var env = new WorldEnvironment { Environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new("b5c6c5"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new("d5e1dc"), AmbientLightEnergy = .65f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Disabled,
            TonemapMode = Godot.Environment.ToneMapper.Filmic, TonemapExposure = 1,
            SsaoEnabled = true, SsaoRadius = .65f, SsaoIntensity = 1.5f,
            SsilEnabled = false, GlowEnabled = true, GlowIntensity = .15f
        }};
        root.AddChild(env);
        root.AddChild(new DirectionalLight3D { Name = "AfternoonSun", RotationDegrees = new(-55, -28, 0),
            LightColor = new("fff1dc"), LightEnergy = 1.2f, ShadowEnabled = true,
            DirectionalShadowMaxDistance = 50, ShadowBias = .04f });
        for (var x = -4; x <= 4; x += 4)
            root.AddChild(new OmniLight3D { Position = new(x, 3.2f, -1), LightColor = new("ffe5ba"),
                LightEnergy = .65f, OmniRange = 6, ShadowEnabled = false });
    }
    public void Floors(Node3D root, StoreState state, bool grid)
    {
        for (var z = 0; z < 10; z++) for (var x = 0; x < 12; x++)
        {
            var type = state.Floors[z * 12 + x];
            Box(root, "Floor", new(grid ? .985f : 1, .07f, grid ? .985f : 1), new(x - 5.5f, -.035f, z - 4.5f), type == 0 ? Floor : type == 1 ? Stone : Dark);
        }
    }
    public Node3D Wall(Node3D root, StoreWall w, bool cut)
    {
        var n = Group(root, "Wall", new(w.X + (w.AlongX ? .5f : 0), 0, w.Z + (w.AlongX ? 0 : .5f)));
        if (!w.AlongX) n.RotationDegrees = new(0, 90, 0);
        var material = w.Color == 0 ? Plaster : Color(w.Color);
        if (cut) { Box(n, "CutEdge", new(1, .12f, .13f), new(0, .06f, 0), material); return n; }
        if (w.Kind == "wall") Box(n, "Plaster", new(1, 2.8f, .13f), new(0, 1.4f, 0), material);
        else
        {
            Box(n, "Lintel", new(1, .36f, .16f), new(0, 2.62f, 0), material);
            foreach (var x in new[] { -.46f, .46f }) Box(n, "Frame", new(.065f, 2.45f, .19f), new(x, 1.22f, 0), Oak);
            Box(n, "Glazing", new(.88f, w.Kind == "door" ? 2.35f : 1.65f, .025f), new(0, w.Kind == "door" ? 1.2f : 1.58f, 0), Glass);
            if (w.Kind == "window")
            {
                Box(n, "SillWall", new(1, .72f, .14f), new(0, .36f, 0), material);
                RoundedBox(n, "Sill", new(1.04f, .06f, .24f), new(0, .74f, 0), Oak);
            }
            else Rod(n, "DoorHandle", new(.32f, .98f, .12f), new(.32f, 1.22f, .12f), .014f, Brass);
        }
        Box(n, "Skirting", new(1, .1f, .17f), new(0, .05f, 0), Oak);
        return n;
    }
    public Node3D Item(Node3D parent, StoreItem item, bool preview = false)
    {
        var n = Group(parent, "Object" + item.Id, item.Position);
        n.RotationDegrees = new(0, item.Rotation, 0);
        if (!preview && item.Stock == 0)
        {
            Selection(n, new StoreItem { Kind = item.Kind, X = 0, Y = 0, Z = 0, Rotation = 0 }, "ba9160");
            var vacant = Text(n, "EmptySpot", "EMPTY · CLICK TO STOCK", new(0, .13f, 0), .0025f, new("986541"), 24);
            vacant.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            vacant.OutlineSize = 5; vacant.OutlineModulate = new("fff4df");
            return n;
        }
        var c = Color(item.Color);
        if (!ElectronicsVisuals.Build(n, item)) switch (item.Kind)
        {
            case "table":
                RoundedBox(n, "OakTop", new(2, .11f, 1), new(0, .745f, 0), Oak, .035f);
                foreach (var x in new[] { -.84f, .84f }) foreach (var z in new[] { -.34f, .34f })
                    RoundedBox(n, "Leg", new(.085f, .7f, .085f), new(x, .35f, z), Oak);
                Box(n, "Apron", new(1.8f, .15f, .68f), new(0, .65f, 0), c);
                break;
            case "shelf":
                foreach (var y in new[] { .14f, .64f, 1.12f, 1.65f, 2.15f })
                    RoundedBox(n, "Shelf", new(1.5f, .045f, .6f), new(0, y, 0), Oak);
                foreach (var x in new[] { -.7f, .7f }) foreach (var z in new[] { -.25f, .25f })
                    Box(n, "Upright", new(.035f, 2.2f, .035f), new(x, 1.1f, z), Brass);
                Box(n, "Backing", new(1.45f, 2.1f, .025f), new(0, 1.1f, -.28f), c);
                break;
            case "plinth": RoundedBox(n, "Stone", new(.8f, .95f, .8f), new(0, .475f, 0), Stone, .018f); break;
            case "vase":
                Vessel(n, "Stoneware", [new(.13f, 0), new(.18f, .035f), new(.21f, .16f), new(.19f, .32f),
                    new(.10f, .43f), new(.09f, .51f), new(.075f, .51f), new(.08f, .43f), new(.16f, .31f), new(.17f, .14f), new(.11f, .03f)], Vector3.Zero, c);
                break;
            case "bowl":
                Vessel(n, "Ceramic", [new(.09f, 0), new(.13f, .025f), new(.23f, .14f), new(.25f, .18f),
                    new(.23f, .19f), new(.21f, .14f), new(.11f, .035f), new(0, .03f)], Vector3.Zero, c);
                break;
            case "candle":
                Cylinder(n, "AmberGlass", .12f, .12f, .21f, new(0, .105f, 0), Mat("9d6541", .25f));
                Cylinder(n, "Wax", .106f, .106f, .012f, new(0, .21f, 0), Linen);
                Box(n, "Label", new(.14f, .075f, .007f), new(0, .12f, .12f), c);
                Text(n, "Lettering", "A / M", new(0, .12f, .126f), .00065f, Dark.AlbedoColor, 30);
                break;
            case "books":
                for (var i = 0; i < 3; i++)
                {
                    var book = Group(n, "Book", new(0, .022f + i * .065f, 0)); book.RotationDegrees = new(0, i * 8 - 8, 0);
                    RoundedBox(book, "Pages", new(.4f, .048f, .29f), Vector3.Zero, Linen, .002f);
                    foreach (var y in new[] { -.026f, .026f }) Box(book, "Cover", new(.43f, .007f, .32f), new(0, y, 0), i == 1 ? Dark : c);
                    Box(book, "Spine", new(.015f, .06f, .32f), new(-.21f, 0, 0), c);
                }
                break;
            case "chair":
                foreach (var x in new[] { -.32f, .32f }) foreach (var z in new[] { -.31f, .31f })
                    Rod(n, "OakLeg", new(x, 0, z), new(x * .9f, .46f, z * .9f), .028f, Oak);
                RoundedBox(n, "Cushion", new(.75f, .17f, .7f), new(0, .48f, 0), c, .06f);
                var back = RoundedBox(n, "Back", new(.75f, .58f, .15f), new(0, .77f, -.3f), c, .05f); back.RotationDegrees = new(-8, 0, 0);
                foreach (var x in new[] { -.38f, .38f }) RoundedBox(n, "Arm", new(.07f, .1f, .7f), new(x, .68f, 0), Oak, .025f);
                break;
            case "lamp":
                Cylinder(n, "Foot", .24f, .24f, .035f, new(0, .02f, 0), Brass);
                Cylinder(n, "Stem", .018f, .018f, 1.47f, new(0, .76f, 0), Brass);
                Vessel(n, "LinenShade", [new(.32f, 1.35f), new(.22f, 1.8f), new(.21f, 1.8f), new(.31f, 1.35f)], Vector3.Zero, Linen);
                if (!preview) n.AddChild(new OmniLight3D { Position = new(0, 1.4f, 0), LightColor = new("ffd8a1"), LightEnergy = .4f, OmniRange = 2.3f });
                break;
            case "plant":
                Cylinder(n, "Planter", .23f, .3f, .46f, new(0, .23f, 0), c);
                Cylinder(n, "Soil", .28f, .28f, .02f, new(0, .46f, 0), Soil);
                Rod(n, "Trunk", new(0, .4f, 0), new(.04f, 1.75f, 0), .025f, Oak);
                for (var i = 0; i < 13; i++)
                {
                    var angle = i * 2.4f; var center = new Vector3(Mathf.Sin(angle) * .25f, .85f + i * .075f, Mathf.Cos(angle) * .25f);
                    Rod(n, "Branch", new(0, center.Y - .13f, 0), center, .008f, Leaf);
                    var leaf = Sphere(n, "Leaf", new(.3f, .035f, .43f), center, Leaf);
                    leaf.RotationDegrees = new(i % 2 * 25 - 20, Mathf.RadToDeg(angle), 15);
                }
                break;
            case "rug":
                RoundedBox(n, "Wool", new(3, .018f, 2), new(0, .012f, 0), c, .006f);
                for (var i = -13; i <= 13; i++)
                    foreach (var z in new[] { -.9f, .9f }) Box(n, "WovenStripe", new(.022f, .003f, .11f), new(i * .1f, .024f, z), Linen);
                break;
            case "stockroom":
                RoundedBox(n, "Cabinet", new(1, 1.75f, .7f), new(0, .875f, 0), c, .02f);
                RoundedBox(n, "Top", new(1.04f, .055f, .74f), new(0, 1.78f, 0), Oak);
                foreach (var x in new[] { -.24f, .24f })
                {
                    RoundedBox(n, "Door", new(.45f, 1.62f, .035f), new(x, .87f, .36f), Oak);
                    Rod(n, "Handle", new(x > 0 ? .05f : -.05f, .8f, .40f), new(x > 0 ? .05f : -.05f, 1.05f, .40f), .012f, Brass);
                }
                Text(n, "SupplySign", "SUPPLIES", new(0, 1.92f, 0), .004f, Dark.AlbedoColor, 30).Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
                break;
            case "checkout":
                RoundedBox(n, "Counter", new(1.8f, .94f, .9f), new(0, .47f, 0), c, .025f);
                RoundedBox(n, "MarbleTop", new(1.86f, .065f, .96f), new(0, .97f, 0), Stone);
                for (var x = -.8f; x < .85f; x += .09f) Box(n, "Fluting", new(.025f, .78f, .02f), new(x, .48f, .46f), Oak);
                Cylinder(n, "TabletStand", .08f, .045f, .14f, new(.4f, 1.08f, -.12f), Brass);
                var tablet = RoundedBox(n, "Register", new(.3f, .21f, .025f), new(.4f, 1.23f, -.12f), Dark); tablet.RotationDegrees = new(-22, 0, 0);
                Text(n, "RegisterLabel", "BOOMING", new(-.24f, 1.007f, .12f), .001f, Dark.AlbedoColor, 24).RotationDegrees = new(-90, 0, 0);
                break;
        }
        if (!preview && item.Kind == "checkout")
        {
            var sign = Text(n, "RegisterSign", item.CheckoutOpen ? "OPEN" : "CLOSED", new(-.35f, 1.24f, .25f), .0035f,
                new(item.CheckoutOpen ? "4eaf7d" : "bf745e"), 36); sign.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            sign.OutlineSize = 6; sign.OutlineModulate = new("fff5e9");
        }
        if (!preview && item.ForSale)
        {
            var y = item.Kind switch { "chair" => .98f, "plant" => 1.95f, "lamp" => 1.95f, "vase" => .62f, "shelf" => 2.3f, "checkout" => 1.5f, _ => .4f };
            if (item.Product.Electronics) y = item.Product.Height + .12f;
            var tag = Text(n, "Price", $"${item.Price:0}" + (item.Used ? " · USED" : ""), new(0, y, 0), .004f,
                new("224b3c"), 28);
            tag.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            tag.OutlineSize = 5; tag.OutlineModulate = new("f5f1e7");
        }
        return n;
    }
    public void Selection(Node3D parent, StoreItem i, string color)
    {
        var size = i.Footprint; var m = Mat(color);
        foreach (var x in new[] { -size.X / 2 - .05f, size.X / 2 + .05f })
            Box(parent, "Selection", new(.025f, .018f, size.Y + .12f), new(i.X + x, i.Y + .03f, i.Z), m);
        foreach (var z in new[] { -size.Y / 2 - .05f, size.Y / 2 + .05f })
            Box(parent, "Selection", new(size.X + .12f, .018f, .025f), new(i.X, i.Y + .03f, i.Z + z), m);
    }
}
