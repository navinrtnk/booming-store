// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

public partial class StoreCamera : Camera3D
{
    public Vector3 Focus { get; set; } = new(.6f, .4f, 0);
    public float Yaw { get; private set; } = .56f;
    public float Pitch => _pitch;
    public float Distance => _distance;
    public bool Following { get; private set; }
    private float _pitch = .75f, _distance = 19;
    public bool OverUi { get; set; }
    public override void _Ready() { Fov = 43; Near = .1f; Far = 150; UpdatePose(); }
    public override void _Process(double delta)
    {
        if (GetViewport().GuiGetFocusOwner() is LineEdit or Godot.Range) return;
        var pan = new Vector3((Input.IsPhysicalKeyPressed(Key.D) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.A) ? 1 : 0), 0,
            (Input.IsPhysicalKeyPressed(Key.S) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.W) ? 1 : 0));
        if (!pan.IsZeroApprox()) Following = false;
        if (Following && GetParent() is StoreGame game && game.Keeper != null)
            Focus = Focus.Lerp(game.Keeper.Actor.Position + Vector3.Up * .75f, 1 - Mathf.Exp(-(float)delta * 5));
        Focus += pan.Rotated(Vector3.Up, Yaw) * (float)delta * _distance * .35f;
        if (Input.IsPhysicalKeyPressed(Key.Q)) Yaw += (float)delta;
        if (Input.IsPhysicalKeyPressed(Key.E)) Yaw -= (float)delta;
        AdjustAngles(((Input.IsPhysicalKeyPressed(Key.Left) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Right) ? 1 : 0)) * (float)delta,
            ((Input.IsPhysicalKeyPressed(Key.Up) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Down) ? 1 : 0)) * (float)delta * .65f,
            ((Input.IsPhysicalKeyPressed(Key.Minus) ? 1 : 0) - (Input.IsPhysicalKeyPressed(Key.Equal) ? 1 : 0)) * (float)delta * 8);
        Focus = new(Mathf.Clamp(Focus.X, -8, 8), Focus.Y, Mathf.Clamp(Focus.Z, -7, 7)); UpdatePose();
    }
    public override void _Input(InputEvent e)
    {
        if (GetParent() is StoreGame game) OverUi = game.Hud?.IsOverUi() ?? false;
        if (e is InputEventMouseMotion m)
        {
            if (Input.IsMouseButtonPressed(MouseButton.Right)) { Yaw -= m.Relative.X * .006f; _pitch = Mathf.Clamp(_pitch + m.Relative.Y * .004f, .25f, 1.3f); }
            if (Input.IsMouseButtonPressed(MouseButton.Middle)) Focus += new Vector3(-m.Relative.X, 0, -m.Relative.Y).Rotated(Vector3.Up, Yaw) * _distance * .0018f;
        }
        if (!OverUi && e is InputEventMouseButton { Pressed: true } b)
        {
            if (b.ButtonIndex == MouseButton.WheelUp) _distance = Mathf.Max(4, _distance * .9f);
            if (b.ButtonIndex == MouseButton.WheelDown) _distance = Mathf.Min(32, _distance * 1.1f);
        }
    }
    public void Home() { Following = false; Yaw = .56f; _pitch = .75f; _distance = 19; Focus = new(.6f, .4f, 0); UpdatePose(); }
    public void ZoomTo(Vector3 point) { Following = false; Focus = point + Vector3.Up * .65f; _distance = 6; _pitch = .45f; UpdatePose(); }
    public void AdjustAngles(float yaw, float pitch, float zoom)
    { Yaw += yaw; _pitch = Mathf.Clamp(_pitch + pitch, .22f, 1.35f); _distance = Mathf.Clamp(_distance + zoom, 4, 32); }
    public void ToggleFollow() { Following = !Following; if (Following) { _distance = 7; _pitch = .4f; } }
    public void SetView(int view)
    {
        Home();
        if (view == 2) { _pitch = .3f; _distance = 7; if (GetParent() is StoreGame game && game.Keeper != null) Focus = game.Keeper.Actor.Position + Vector3.Up * .8f; }
        if (view == 3) { _pitch = 1.3f; _distance = 19; Yaw = 0; }
        UpdatePose();
    }
    private void UpdatePose()
    { Position = Focus + new Vector3(Mathf.Sin(Yaw) * Mathf.Cos(_pitch), Mathf.Sin(_pitch), Mathf.Cos(Yaw) * Mathf.Cos(_pitch)) * _distance; LookAt(Focus); }
    public Vector3? Ground(Vector2 screen, float height = 0)
    {
        var from = ProjectRayOrigin(screen); var ray = ProjectRayNormal(screen);
        if (Mathf.Abs(ray.Y) < .001f) return null;
        var t = (height - from.Y) / ray.Y; return t > 0 ? from + ray * t : null;
    }
}
