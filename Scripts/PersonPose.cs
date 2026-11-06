// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

public static class PersonPose
{
    public readonly record struct Idle(Vector3 Body, Vector3 Torso, Vector3 LeftHand, Vector3 RightHand, Vector3 Head, float Smile);
    public readonly record struct Arm(Quaternion Upper, Quaternion Lower, Quaternion Wrist);
    public static Idle Relaxed(float time, int personality)
    {
        var t = time + personality * 2.37f;
        var side = personality % 2 == 0 ? 1 : -1;
        var shift = side * .007f + Mathf.Sin(t * .31f) * .007f;
        var breath = Mathf.Sin(t * (1.05f + personality % 3 * .07f));
        return new(new(shift, breath * .0015f, 0), new(.016f + breath * .003f, Mathf.Sin(t * .23f) * .018f, -shift * 1.1f),
            new(-.005f, .002f + Mathf.Sin(t * .57f) * .003f, .008f),
            new(.004f, .006f + Mathf.Sin(t * .43f + 1) * .003f, .012f),
            new(.015f + Mathf.Sin(t * .41f) * .018f, Mathf.Sin(t * .27f) * .10f, side * .022f + Mathf.Sin(t * .19f) * .015f),
            .12f + personality % 3 * .025f + Mathf.Sin(t * .17f) * .025f);
    }
    public static Arm SolveArm(Vector3 shoulder, Vector3 elbow, Vector3 wrist, Vector3 target)
    {
        var ab = elbow - shoulder; var bc = wrist - elbow;
        var offset = target - shoulder;
        var direction = offset.LengthSquared() > .00001f ? offset.Normalized() : Vector3.Down;
        var distance = Mathf.Clamp(offset.Length(), Mathf.Abs(ab.Length() - bc.Length()) + .002f, ab.Length() + bc.Length() - .002f);
        var elbowHint = new Vector3(Mathf.Sign(shoulder.X) * .3f, -1, -.15f);
        var pole = elbowHint - direction * elbowHint.Dot(direction);
        if (pole.LengthSquared() < .001f) pole = Vector3.Back - direction * Vector3.Back.Dot(direction);
        var along = (ab.LengthSquared() - bc.LengthSquared() + distance * distance) / (2 * distance);
        var joint = shoulder + direction * along + pole.Normalized() * Mathf.Sqrt(Mathf.Max(0, ab.LengthSquared() - along * along));
        var end = shoulder + direction * distance;
        var upper = new Quaternion(ab.Normalized(), (joint - shoulder).Normalized()).Normalized();
        var lower = new Quaternion(bc.Normalized(), (end - joint).Normalized()).Normalized();
        return new(upper, (upper.Inverse() * lower).Normalized(), lower.Inverse().Normalized());
    }
}
