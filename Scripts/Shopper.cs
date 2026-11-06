// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Skinned, anatomically proportioned customer with procedural gait and facial joints.</summary>
public partial class Shopper : Node3D
{
    public enum Activity { Arriving, Walking, Browsing, Queueing, Paying, Leaving }
    public Activity State { get; private set; }
    public StoreItem? Choice { get; private set; }
    public bool Finished { get; private set; }
    public int QueueNumber { get; set; }
    public bool Staff { get; set; }
    public Vector3 LookTarget { get; set; }
    private Skeleton3D _rig = null!;
    private Node3D _visual = null!, _bag = null!;
    private Label3D _thought = null!;
    private StoreGame _game = null!;
    private readonly Queue<Vector3> _path = new();
    private readonly Dictionary<string, int> _bones = [];
    private readonly Dictionary<int, Dictionary<string, Quaternion>> _expressions = [];
    private float _clock, _timer, _walk, _gait;
    private int _visited;
    private bool _bought;
    private static readonly string[] Models = ["gentleman_a", "woman_a", "gentleman_b", "woman_b", "woman_c"];
    private static readonly PackedScene[] Scenes = Models.Select(m => GD.Load<PackedScene>($"res://Assets/Customer/Avatars/{m}.glb")).ToArray();
    public void Build(StoreGame game, int profile, bool staff = false)
    {
        _game = game; Staff = staff;
        _clock = profile * 1.73f;
        _visual = Scenes[profile % Scenes.Length].Instantiate<Node3D>(); AddChild(_visual);
        _rig = _visual.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().Single();
        for (var i = 0; i < _rig.GetBoneCount(); i++) _bones[_rig.GetBoneName(i)] = i;
        var data = Json.ParseString(Godot.FileAccess.GetFileAsString("res://Assets/Customer/expression-poses.json")).AsGodotDictionary();
        foreach (var group in data) foreach (var entry in group.Value.AsGodotDictionary())
        {
            if (!_bones.TryGetValue(entry.Key.AsString(), out var id)) continue;
            if (!_expressions.TryGetValue(id, out var poses)) _expressions[id] = poses = [];
            var q = entry.Value.AsGodotArray(); poses[group.Key.AsString()] = new Quaternion((float)q[0], (float)q[1], (float)q[2], (float)q[3]).Normalized();
        }
        foreach (var mesh in _visual.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            for (var i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(i) is not StandardMaterial3D source) continue;
                var m = (StandardMaterial3D)source.Duplicate();
                m.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic;
                if (mesh.Name == "Clothing") m.AlbedoColor = new Color(staff ? "aab8ad" : Catalog.Colors[(profile + 1) % 6]);
                if (mesh.Name == "Skin") { m.Roughness = .58f; m.SubsurfScatterEnabled = RenderingServer.GetCurrentRenderingMethod() == "forward_plus"; m.SubsurfScatterStrength = .08f; m.SubsurfScatterSkinMode = true; }
                if (mesh.Name == "Eyes") m.Roughness = .18f;
                mesh.SetSurfaceOverrideMaterial(i, m);
            }
        // Shoes follow the anatomical foot bones rather than sliding under the legs.
        foreach (var side in new[] { "L", "R" })
        {
            var bone = _bones["foot_" + side];
            var attach = new BoneAttachment3D { BoneIdx = bone }; _rig.AddChild(attach);
            Geometry.RoundedBox(attach, "Shoe", new(.11f, .08f, .25f), new(0, -.035f, .065f), StoreVisuals.Mat("343431"), .025f);
        }
        _bag = Geometry.Group(this, "ShoppingBag", new(-.26f, .73f, .04f));
        Geometry.RoundedBox(_bag, "Paper", new(.23f, .3f, .12f), Vector3.Zero, StoreVisuals.Mat("bd9c72"), .008f);
        var ring = Geometry.Ring(_bag, "Handle", .065f, .005f, new(0, .17f, 0), StoreVisuals.Mat("765b3e")); ring.RotationDegrees = new(90, 0, 0);
        _bag.Visible = false;
        _thought = Geometry.Text(this, "Thought", "", new(0, 2.02f, 0), .0035f, new("204b3c"), 28);
        _thought.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _thought.OutlineSize = 6; _thought.OutlineModulate = new("fff7e8");
        State = Activity.Arriving;
        if (!staff) { Position = _game.Navigation.Entrance; _timer = .8f; }
    }
    public override void _Process(double delta)
    {
        var dt = Mathf.Min((float)delta, .06f) * _game.Speed;
        _clock += dt;
        if (Staff) { Animate(dt, 0); return; }
        if (Finished || dt == 0) return;
        _timer -= dt;
        var moving = _path.Count > 0;
        if (moving)
        {
            var target = _path.Peek(); var offset = target - Position; offset.Y = 0;
            if (offset.Length() < .07f) _path.Dequeue();
            else
            {
                var direction = offset.Normalized();
                // A little separation keeps shoppers from occupying the same point in an aisle.
                var separation = Vector3.Zero;
                foreach (var other in _game.Shoppers.Where(o => o != this && !o.Finished))
                { var d = Position - other.Position; d.Y = 0; if (d.Length() is > .01f and < .5f) separation += d.Normalized() * .3f; }
                var next = Position.MoveToward(target, dt * 1.12f);
                // Never leave the planned cell corridor for separation.
                if (ShopNavigation.Cell(next + separation * dt) == ShopNavigation.Cell(next)) next += separation * dt;
                Position = next;
                Rotation = new(0, Mathf.LerpAngle(Rotation.Y, Mathf.Atan2(direction.X, direction.Z), 1 - Mathf.Exp(-10 * dt)), 0);
            }
        }
        if (_path.Count == 0)
            switch (State)
            {
                case Activity.Arriving: if (_timer <= 0) BrowseNext(); break;
                case Activity.Walking: State = Activity.Browsing; _timer = 3.5f; _thought.Text = "A closer look…"; break;
                case Activity.Browsing:
                    if (Choice != null) Face(Choice.Position, dt);
                    if (_timer <= 0) Decide(); break;
                case Activity.Queueing:
                    var index = _game.Queue.IndexOf(this);
                    var spot = _game.QueueSpot(Math.Max(0, index));
                    if (Position.DistanceTo(spot) > .2f) GoTo(spot);
                    else if (index == 0) { State = Activity.Paying; _timer = 2.5f; _thought.Text = "Checking out"; }
                    break;
                case Activity.Paying:
                    Face(_game.Checkout!.Position, dt);
                    if (_timer <= 0)
                    {
                        _bought = _game.CompleteSale(this);
                        _bag.Visible = _bought; _thought.Text = _bought ? "Lovely. Thank you!" : "Maybe next time";
                        _game.LeaveQueue(this); Leave();
                    }
                    break;
                case Activity.Leaving: Finished = true; _game.LeaveQueue(this); QueueFree(); break;
            }
        Animate(dt, moving ? 1 : 0);
    }
    private void BrowseNext()
    {
        var candidates = _game.State.Items.Where(i => i.ForSale && i.Stock > _game.Reserved(i) && i != Choice).OrderBy(_ => GD.Randf());
        foreach (var i in candidates)
        {
            var point = _game.Navigation.Approach(i, Position);
            if (point == null) continue;
            Choice = i; _visited++; GoTo(point.Value); State = Activity.Walking; _thought.Text = "Browsing"; return;
        }
        _thought.Text = "Nothing available today"; Leave();
    }
    private void Decide()
    {
        if (Choice == null || Choice.Stock <= _game.Reserved(Choice)) { BrowseNext(); return; }
        var ratio = (float)(Choice.Price / Choice.Product.Price);
        var chance = Mathf.Clamp(.94f - Math.Max(0, ratio - .8f) * .7f, .04f, .98f);
        if (GD.Randf() < chance)
        {
            if (!_game.JoinQueue(this)) { BrowseNext(); return; }
            State = Activity.Queueing; _thought.Text = "I'll take it";
            if (!GoTo(_game.QueueSpot(_game.Queue.Count - 1))) { _game.LeaveQueue(this); Leave(); }
        }
        else if (_visited < 3) { _thought.Text = "A little expensive…"; BrowseNext(); }
        else { _thought.Text = "Maybe another day"; Leave(); }
    }
    private bool GoTo(Vector3 target)
    {
        _path.Clear(); var path = _game.Navigation.Path(Position, target);
        if (path == null) return false;
        foreach (var p in path) _path.Enqueue(p);
        return true;
    }
    private void Leave()
    {
        State = Activity.Leaving;
        if (!GoTo(_game.Navigation.Entrance)) { Finished = true; QueueFree(); }
    }
    private void Face(Vector3 point, float dt)
    { var d = point - Position; Rotation = new(0, Mathf.LerpAngle(Rotation.Y, Mathf.Atan2(d.X, d.Z), dt * 4), 0); }
    private void Pose(string name, Vector3 euler) { if (_bones.TryGetValue(name, out var b)) _rig.SetBonePoseRotation(b, Basis.FromEuler(euler).GetRotationQuaternion()); }
    private void Animate(float dt, float walking)
    {
        _walk = Mathf.MoveToward(_walk, walking, dt * 5);
        _gait += dt * 7.8f * _walk; var stride = Mathf.Sin(_gait) * _walk;
        _visual.Position = new(0, Mathf.Abs(Mathf.Sin(_gait)) * .024f * _walk + Mathf.Sin(_clock * 1.8f) * .002f, 0);
        Pose("upperleg01_L", new(stride * .35f, 0, 0)); Pose("upperleg01_R", new(-stride * .35f, 0, 0));
        Pose("lowerleg01_L", new(-Mathf.Max(0, -stride) * .62f, 0, 0)); Pose("lowerleg01_R", new(-Mathf.Max(0, stride) * .62f, 0, 0));
        Pose("foot_L", new(Mathf.Max(0, -stride) * .2f, 0, 0)); Pose("foot_R", new(Mathf.Max(0, stride) * .2f, 0, 0));
        var browse = State == Activity.Browsing && _timer < 2.7f && _timer > .6f ? Mathf.Sin((_timer - .6f) / 2.1f * Mathf.Pi) : 0;
        Pose("upperarm01_L", new(-stride * .22f, 0, .015f));
        Pose("upperarm01_R", new(stride * .22f - browse * .5f, 0, -.015f));
        Pose("lowerarm01_R", new(-.08f - browse * .6f, 0, 0));
        Pose("spine05", new(0, stride * .035f, Mathf.Sin(_clock) * .007f));
        var nod = State == Activity.Paying || Staff ? Mathf.Sin(_clock * 2) * .035f : browse * .12f;
        Pose("head", new(nod, Mathf.Sin(_clock * .7f) * .07f, 0));
        var blink = _clock % 4.6f; var amount = blink < .18f ? Mathf.Sin(blink / .18f * Mathf.Pi) : 0;
        foreach (var (bone, expressions) in _expressions)
        {
            var q = Quaternion.Identity;
            foreach (var (name, value) in expressions)
                q *= Quaternion.Identity.Slerp(value, name switch { "BlinkL" or "BlinkR" => amount, "Smile" => _bought ? .5f : .13f, _ => 0 });
            _rig.SetBonePoseRotation(bone, q.Normalized());
        }
        _bag.Position = new(-.26f, .73f + Mathf.Sin(_gait) * .02f * _walk, .04f);
    }
}
