// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Shared skinned person, relaxed posture and distance-driven movement for shoppers and the owner.</summary>
public partial class PersonActor : Node3D
{
    protected StoreGame _game = null!;
    public bool Finished { get; protected set; }
    public bool IsShopkeeper { get; private set; }
    public bool IsWalking => _path.Count > 0;
    public bool RouteFailed { get; private set; }
    public Vector3? Destination => _destination;
    public Vector3? AttentionTarget { get; set; }
    public Vector3? ReachTarget { get; set; }
    public float GestureAmount { get; set; }
    public bool CarryingStock { get; private set; }
    protected bool _bought;
    private Skeleton3D _rig = null!;
    private Node3D _visual = null!, _bag = null!, _cargo = null!;
    private MeshInstance3D? _flowingHair;
    private readonly PersonExpression _expression = new();
    private PersonExpression.Pose _social;
    private PersonVoice _voice = null!;
    private float _stepCooldown, _lastStepTravel;
    public bool IsSpeaking => _voice?.IsSpeaking == true;
    public float MouthAmount => _voice?.MouthAmount ?? 0;
    public int ExpressionsStarted { get; private set; }
    private float _previousYaw, _hairSway;
    private Label3D _thought = null!, _cargoText = null!;
    private readonly Queue<Vector3> _path = new();
    private readonly Dictionary<string, int> _bones = [];
    private readonly Dictionary<int, Dictionary<string, Quaternion>> _expressions = [];
    private float _clock, _walk, _travel, _moveSpeed, _remainingDistance, _groundOffset, _walkSpeed, _strideScale, _carryBlend;
    private int _personality, _routeRevision;
    private Vector3 _routePosition, _laneOffset, _headPose;
    private Vector3? _destination;
    private Leg _leftLeg = null!, _rightLeg = null!;
    private ArmRig _leftArm = null!, _rightArm = null!;
    private sealed record Leg(int HipBone, int KneeBone, int FootBone, Vector3 Hip, Vector3 Knee, Vector3 Foot);
    private sealed record ArmRig(int Upper, int Lower, int Wrist, Vector3 Shoulder, Vector3 Elbow, Vector3 Hand);
    private static readonly string[] Models = ["gentleman_a", "woman_a", "gentleman_b", "woman_b", "woman_c"];
    private static readonly PackedScene[] Scenes = Models.Select(m => GD.Load<PackedScene>($"res://Assets/Customer/Avatars/{m}.glb")).ToArray();
    public void Build(StoreGame game, int profile, bool shopkeeper = false)
    {
        _game = game; IsShopkeeper = shopkeeper; _personality = profile;
        _walkSpeed = .78f + profile % 5 * .025f;
        _clock = profile * 1.73f;
        _visual = (shopkeeper ? GD.Load<PackedScene>("res://Assets/Customer/Avatars/shopkeeper.glb") : Scenes[profile % Scenes.Length]).Instantiate<Node3D>(); AddChild(_visual);
        _flowingHair = _visual.FindChild("FlowingHair", true, false) as MeshInstance3D;
        _rig = _visual.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().Single();
        for (var i = 0; i < _rig.GetBoneCount(); i++) _bones[_rig.GetBoneName(i)] = i;
        _leftLeg = MakeLeg("L"); _rightLeg = MakeLeg("R");
        _strideScale = ShopperMotion.LegScale(_leftLeg.Hip, _leftLeg.Foot); _walkSpeed *= Mathf.Sqrt(_strideScale);
        _groundOffset = Math.Min(_leftLeg.Foot.Y, _rightLeg.Foot.Y) - .075f;
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
                if (mesh.Name == "Clothing") m.AlbedoColor = new Color(shopkeeper ? "a0b79c" : Catalog.Colors[(profile + 1) % 6]);
                if (mesh.Name == "Skin") { m.Roughness = .58f; m.SubsurfScatterEnabled = RenderingServer.GetCurrentRenderingMethod() == "forward_plus"; m.SubsurfScatterStrength = .08f; m.SubsurfScatterSkinMode = true; }
                if (mesh.Name == "Eyes") m.Roughness = .18f;
                mesh.SetSurfaceOverrideMaterial(i, m);
            }
        // Shoes follow the anatomical foot bones rather than sliding under the legs.
        foreach (var side in new[] { "L", "R" })
        {
            var bone = _bones["foot_" + side];
            var attach = new BoneAttachment3D { BoneIdx = bone }; _rig.AddChild(attach);
            Geometry.RoundedBox(attach, "Shoe", shopkeeper ? new(.10f, .055f, .225f) : new(.11f, .08f, .25f),
                new(0, -.035f, .065f), StoreVisuals.Mat(shopkeeper ? "785d49" : "343431"), .025f);
        }
        var hand = new BoneAttachment3D { BoneIdx = _bones["wrist_L"] }; _rig.AddChild(hand);
        _bag = Geometry.Group(hand, "ShoppingBag", new(0, -.22f, .01f));
        Geometry.RoundedBox(_bag, "Paper", new(.23f, .3f, .12f), Vector3.Zero, StoreVisuals.Mat("bd9c72"), .008f);
        var ring = Geometry.Ring(_bag, "Handle", .065f, .005f, new(0, .17f, 0), StoreVisuals.Mat("765b3e")); ring.RotationDegrees = new(90, 0, 0);
        _bag.Visible = false;
        _thought = Geometry.Text(this, "Thought", "", new(0, 2.02f, 0), .0035f, new("204b3c"), 28);
        _thought.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _thought.OutlineSize = 6; _thought.OutlineModulate = new("fff7e8");
        Position = _game.Navigation.Entrance; _routePosition = Position;
        Rotation = new(0, Mathf.Atan2(-Position.X, -Position.Z), 0); _previousYaw = Rotation.Y;
        _leftArm = MakeArm("L"); _rightArm = MakeArm("R");
        foreach (var (name, bone) in _bones)
        {
            if (!name.StartsWith("finger")) continue;
            var children = _rig.GetBoneChildren(bone);
            if (children.Length == 0) continue;
            var forward = (_rig.GetBoneGlobalRest(children[0]).Origin - _rig.GetBoneGlobalRest(bone).Origin).Normalized();
            var inward = name.EndsWith("_L") ? Vector3.Left : Vector3.Right;
            var axis = forward.Cross(inward);
            if (axis.LengthSquared() > .001f) _rig.SetBonePoseRotation(bone, new Quaternion(axis.Normalized(), name.StartsWith("finger1") ? .10f : .22f));
        }
        _cargo = Geometry.Group(_visual, "ReplacementBox", new(0, 1.04f * _strideScale, .34f));
        Geometry.RoundedBox(_cargo, "Carton", new(.39f, .26f, .3f), Vector3.Zero, StoreVisuals.Mat("bc9f77"), .008f);
        Geometry.Box(_cargo, "Tape", new(.04f, .004f, .3f), new(0, .132f, 0), StoreVisuals.Mat("e5d8b8"));
        _cargoText = Geometry.Text(_cargo, "Contents", "", new(0, 0, .153f), .0015f, new("354c3f"), 22);
        _cargo.Visible = false;
        _voice = new PersonVoice(this, game.Audio, profile, shopkeeper);
        Animate(0, 0);
    }
    public void React(PersonBeat beat, Vector3? target = null, VoiceLine? line = null, float delay = 0)
    {
        _expression.Start(beat, target); ExpressionsStarted++;
        _voice.Request(line, delay);
    }
    public void PauseVoice(bool paused) => _voice?.SetPaused(paused);
    public void StopVoice() => _voice?.Stop();
    public override void _ExitTree() => _voice?.Release();
    private void AdvanceExpression(float realDelta)
    {
        _expression.Advance(realDelta); _social = _expression.Sample(IsShopkeeper);
        _stepCooldown = Mathf.Max(0, _stepCooldown - realDelta);
        _voice.Advance(realDelta);
    }
    public override void _Process(double delta)
    {
        var dt = Mathf.Min((float)delta, .1f) * _game.Speed;
        Animate(dt, StepPerson(dt), _game.Speed == 0 ? 0 : Mathf.Min((float)delta, .1f));
    }
    protected float StepPerson(float dt)
    {
        _clock += dt;
        if (dt <= 0) return 0;
        if (IsWalking && _routeRevision != _game.Navigation.Revision && _destination is { } goal) WalkTo(goal);
        var previous = Position;
        if (IsWalking) Move(dt);
        else
        {
            _moveSpeed = Mathf.MoveToward(_moveSpeed, 0, dt * 1.7f);
            _laneOffset = _laneOffset.MoveToward(Vector3.Zero, dt * .2f);
            var settled = _routePosition + _laneOffset;
            if (_game.Navigation.CanStep(Position, settled)) Position = settled;
        }
        var distance = Position.DistanceTo(previous); _travel += distance;
        if (_travel - _lastStepTravel >= ShopperMotion.StrideLength * _strideScale * .5f && _stepCooldown <= 0)
        { _game.Audio.Play(ShopSound.Step, Position); _lastStepTravel = _travel; _stepCooldown = .13f; }
        return Mathf.Clamp(distance / (dt * _walkSpeed), 0, 1);
    }
    public void PlaceAt(Vector3 position)
    { Position = position; _routePosition = position; _laneOffset = Vector3.Zero; _path.Clear(); _destination = null; _moveSpeed = 0; RouteFailed = false; }
    public bool ConsumeRouteFailure() { var failed = RouteFailed; RouteFailed = false; return failed; }
    public void Stop() => PlaceAt(Position);
    public void Say(string text) => _thought.Text = text;
    public void SetCargo(string? contents)
    { CarryingStock = contents != null; _cargoText.Text = contents ?? ""; }
    private ArmRig MakeArm(string side)
    {
        var a = _bones["upperarm01_" + side]; var b = _bones["lowerarm01_" + side]; var c = _bones["wrist_" + side];
        return new(a, b, c, _rig.GetBoneGlobalRest(a).Origin, _rig.GetBoneGlobalRest(b).Origin, _rig.GetBoneGlobalRest(c).Origin);
    }
    private void PoseArm(ArmRig arm, bool right, Vector3 relaxed)
    {
        var angle = ShopperMotion.ArmAngle(_travel, right, _bought, _walk, _strideScale);
        var length = arm.Shoulder.DistanceTo(arm.Elbow) + arm.Elbow.DistanceTo(arm.Hand);
        var hanging = new Vector3(Mathf.Sign(arm.Shoulder.X) * .035f, -length * .94f, .04f);
        var hand = arm.Shoulder + new Quaternion(Vector3.Right, angle) * hanging + relaxed * (1 - _walk * .6f);
        var socialWeight = (1 - _walk * .8f) * (1 - _carryBlend);
        var wave = right ? _social.Wave * socialWeight : 0;
        var waveTarget = arm.Shoulder + new Vector3(-.24f, .13f + Mathf.Sin(_expression.Age * 8) * .025f, .15f);
        hand = hand.Lerp(waveTarget, wave);
        var explain = _social.Explain * socialWeight;
        hand += new Vector3((right ? -.02f : .02f), .13f + Mathf.Sin(_expression.Age * 5 + (right ? 0 : 1.6f)) * .035f, .16f) * explain;
        hand = hand.Lerp(new Vector3((right ? -.19f : .19f) * _strideScale, 1.04f * _strideScale, .34f), _carryBlend);
        if (right && GestureAmount > 0)
        {
            var target = ReachTarget is { } world ? ToLocal(world) + Vector3.Up * _groundOffset : arm.Hand + new Vector3(-.03f, .12f, .18f);
            hand = hand.Lerp(target, GestureAmount);
        }
        var pose = PersonPose.SolveArm(arm.Shoulder, arm.Elbow, arm.Hand, hand);
        _rig.SetBonePoseRotation(arm.Upper, pose.Upper);
        _rig.SetBonePoseRotation(arm.Lower, pose.Lower);
        var handRotation = Quaternion.Identity.Slerp(new Quaternion(Vector3.Right, 2.5f), wave);
        handRotation *= Quaternion.Identity.Slerp(new Quaternion(Vector3.Right, .6f), explain * .5f);
        _rig.SetBonePoseRotation(arm.Wrist, pose.Wrist * handRotation);
    }
    private void Move(float dt)
    {
        var direction = (_path.Peek() - _routePosition).Normalized();
        var desiredSpeed = Mathf.Min(_walkSpeed, Mathf.Sqrt(2 * 1.7f * _remainingDistance));
        var heading = Mathf.Atan2(direction.X, direction.Z);
        var turn = Mathf.Abs(Mathf.AngleDifference(Rotation.Y, heading));
        desiredSpeed *= Mathf.Lerp(1, .4f, Mathf.Clamp(turn / (Mathf.Pi / 2), 0, 1));
        var lane = Vector3.Zero;
        foreach (var other in _game.People)
        {
            if (other == this || other.Finished) continue;
            var offset = other.Position - Position; offset.Y = 0;
            var distance = offset.Length();
            if (distance >= .95f || distance < .01f || direction.Dot(offset / distance) < .25f) continue;
            var otherForward = new Vector3(Mathf.Sin(other.Rotation.Y), 0, Mathf.Cos(other.Rotation.Y));
            if ((!other.IsWalking && _destination is { } goal && goal.DistanceTo(other.Position) > .5f) ||
                other.IsWalking && direction.Dot(otherForward) < .2f)
            {
                var side = Vector3.Up.Cross(direction) * .45f;
                if (!_game.Navigation.CanStep(_routePosition, _routePosition + side)) side = -side;
                lane = side; desiredSpeed *= .65f;
            }
            else desiredSpeed *= Mathf.Clamp((distance - .34f) / .36f, 0, 1);
        }
        _moveSpeed = Mathf.MoveToward(_moveSpeed, desiredSpeed, dt * 1.7f);
        var budget = _moveSpeed * dt;
        while (_path.Count > 0 && budget > 0)
        {
            var target = _path.Peek(); var length = _routePosition.DistanceTo(target);
            if (length <= budget + .0001f) { _routePosition = target; _path.Dequeue(); budget = Mathf.Max(0, budget - length); _remainingDistance = Mathf.Max(0, _remainingDistance - length); }
            else { _routePosition = _routePosition.MoveToward(target, budget); _remainingDistance = Mathf.Max(0, _remainingDistance - budget); budget = 0; }
        }
        lane *= Mathf.Clamp(_remainingDistance / .7f, 0, 1);
        _laneOffset = _laneOffset.Lerp(lane, 1 - Mathf.Exp(-5 * dt));
        var candidate = _routePosition + _laneOffset;
        if (_game.Navigation.CanStep(Position, candidate)) Position = candidate;
        else if (_game.Navigation.CanStep(Position, _routePosition)) { Position = _routePosition; _laneOffset = Vector3.Zero; }
        else if (_destination is { } goal) { WalkTo(goal); _moveSpeed = 0; }
        Rotation = new(0, Mathf.LerpAngle(Rotation.Y, heading, 1 - Mathf.Exp(-4 * dt)), 0);
    }
    public bool WalkTo(Vector3 target)
    {
        _path.Clear(); RouteFailed = false; _routeRevision = _game.Navigation.Revision; var path = _game.Navigation.Path(Position, target);
        if (path == null) { RouteFailed = true; return false; }
        if (path.Count == 0 && Position.DistanceTo(target) > .01f) path.Add(target);
        _routePosition = Position; _laneOffset = Vector3.Zero; _remainingDistance = 0; _destination = target;
        var previous = Position;
        foreach (var point in ShopperMotion.RoundCorners(Position, path, _game.Navigation.IsSegmentClear))
        {
            if (previous.DistanceTo(point) < .001f) continue;
            _path.Enqueue(point); _remainingDistance += previous.DistanceTo(point); previous = point;
        }
        return true;
    }
    public void Face(Vector3 point, float dt)
    { var d = point - Position; Rotation = new(0, Mathf.LerpAngle(Rotation.Y, Mathf.Atan2(d.X, d.Z), 1 - Mathf.Exp(-3 * dt)), 0); }
    private void Pose(string name, Vector3 euler) { if (_bones.TryGetValue(name, out var b)) _rig.SetBonePoseRotation(b, Basis.FromEuler(euler).GetRotationQuaternion()); }
    private Leg MakeLeg(string side)
    {
        var hip = _bones["upperleg01_" + side]; var knee = _bones["lowerleg01_" + side]; var foot = _bones["foot_" + side];
        return new(hip, knee, foot, _rig.GetBoneGlobalRest(hip).Origin, _rig.GetBoneGlobalRest(knee).Origin, _rig.GetBoneGlobalRest(foot).Origin);
    }
    private void PoseLeg(Leg leg, bool right, Vector3 body)
    {
        var foot = ShopperMotion.Foot(_travel, right, _strideScale);
        var target = leg.Foot + new Vector3(0, foot.Lift, foot.Forward) * _walk - body;
        var width = Mathf.Lerp(.14f, .105f, _walk) * _strideScale;
        target.X += (right ? -width : width) - leg.Foot.X;
        target.Z += (right ? -.012f : .012f) * (1 - _walk);
        var pose = ShopperMotion.SolveLeg(leg.Hip, leg.Knee, leg.Foot, target, foot.Pitch * _walk);
        _rig.SetBonePoseRotation(leg.HipBone, pose.Hip);
        _rig.SetBonePoseRotation(leg.KneeBone, pose.Knee);
        _rig.SetBonePoseRotation(leg.FootBone, pose.Ankle);
    }
    protected void Animate(float dt, float walking, float realDelta = 0)
    {
        if (realDelta > 0) AdvanceExpression(realDelta);
        _walk = Mathf.Lerp(_walk, walking, 1 - Mathf.Exp(-8 * dt));
        var phase = _travel / (ShopperMotion.StrideLength * _strideScale) * Mathf.Tau;
        var stride = Mathf.Sin(phase) * _walk;
        var idle = PersonPose.Relaxed(_clock, _personality);
        if (_flowingHair != null && dt > 0)
        {
            var turn = Mathf.AngleDifference(_previousYaw, Rotation.Y) / dt; _previousYaw = Rotation.Y;
            var drift = Mathf.Sin(_clock * 1.3f) * .10f + Mathf.Sin(phase) * _walk * .30f - turn * .18f;
            _hairSway = Mathf.Lerp(_hairSway, Mathf.Clamp(drift, -.8f, .8f), 1 - Mathf.Exp(-5 * dt));
            _flowingHair.SetBlendShapeValue(0, Mathf.Max(0, _hairSway));
            _flowingHair.SetBlendShapeValue(1, Mathf.Max(0, -_hairSway));
            _flowingHair.SetBlendShapeValue(2, _walk * .25f + Mathf.Abs(_hairSway) * .18f);
        }
        _carryBlend = Mathf.Lerp(_carryBlend, CarryingStock ? 1 : 0, 1 - Mathf.Exp(-5 * dt));
        var warmth = IsShopkeeper ? new Vector3(Mathf.Sin(_clock * .6f) * .006f, 0, 0) : Vector3.Zero;
        var body = ShopperMotion.BodyOffset(_travel, _walk, _strideScale) + (idle.Body + warmth + _social.Body) * (1 - _walk);
        if (ReachTarget is { } low && low.Y < .6f) body.Y -= GestureAmount * .09f;
        _visual.Position = body + new Vector3(0, -_groundOffset, 0);
        PoseLeg(_leftLeg, false, body); PoseLeg(_rightLeg, true, body);
        var browse = GestureAmount;
        PoseArm(_leftArm, false, idle.LeftHand);
        PoseArm(_rightArm, true, idle.RightHand);
        Pose("spine04", (idle.Torso + _social.Torso) * (1 - _walk * .6f) + new Vector3(browse * .07f, 0, 0));
        Pose("spine05", new(0, stride * .018f, idle.Torso.Z * .4f));
        var head = idle.Head + _social.Head;
        if (IsShopkeeper) head.Z += .018f * (1 - _walk);
        var lookAt = _social.Weight > .05f ? _expression.LookAt ?? AttentionTarget : AttentionTarget;
        if (lookAt is { } target)
        {
            var local = ToLocal(target) - new Vector3(0, 1.55f * _strideScale, 0);
            head.X += Mathf.Clamp(-Mathf.Atan2(local.Y, new Vector2(local.X, local.Z).Length()), -.18f, .3f);
            head.Y += Mathf.Clamp(Mathf.Atan2(local.X, local.Z), -.42f, .42f);
        }
        _headPose = _headPose.Lerp(head, 1 - Mathf.Exp(-3 * dt));
        Pose("neck01", _headPose * .25f); Pose("head", _headPose * .75f);
        var blink = _clock % 4.6f; var amount = blink < .18f ? Mathf.Sin(blink / .18f * Mathf.Pi) : 0;
        foreach (var (bone, expressions) in _expressions)
        {
            var q = Quaternion.Identity;
            foreach (var (name, value) in expressions)
                q *= Quaternion.Identity.Slerp(value, name switch { "BlinkL" or "BlinkR" => amount, "Smile" => Mathf.Clamp((IsShopkeeper ? .32f + idle.Smile * .25f : _bought ? .35f : idle.Smile) + _social.Smile, 0, .62f),
                    "Talk" => MouthAmount * .65f, "BrowLift" => _social.Weight * .16f, _ => 0 });
            _rig.SetBonePoseRotation(bone, q.Normalized());
        }
        _bag.Visible = _bought && !CarryingStock;
        _bag.Rotation = new(0, 0, stride * .025f);
        _cargo.Visible = CarryingStock;
    }
}
