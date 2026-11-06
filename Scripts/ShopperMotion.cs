// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>Distance-driven walking: grounded stance, low swing clearance, and forward-bending knees.</summary>
public static class ShopperMotion
{
    public const float StrideLength = .88f;
    public const float StanceFraction = .62f;
    public const float FootClearance = .045f;
    public readonly record struct FootPose(float Forward, float Lift, float Pitch);
    public readonly record struct LegPose(Quaternion Hip, Quaternion Knee, Quaternion Ankle);

    public static float LegScale(Vector3 hip, Vector3 ankle) => (hip.Y - ankle.Y) / .861f;
    public static FootPose Foot(float distance, bool right, float scale = 1)
    {
        var length = StrideLength * scale;
        var phase = Mathf.PosMod(distance / length + (right ? .5f : 0), 1);
        var reach = length * StanceFraction / 2;
        if (phase < StanceFraction)
            return new(reach - length * phase, 0, 0);
        var swing = (phase - StanceFraction) / (1 - StanceFraction);
        var ease = swing * swing * (3 - 2 * swing);
        var lift = Mathf.Sin(swing * Mathf.Pi);
        return new(Mathf.Lerp(-reach, reach, ease), FootClearance * scale * lift * lift, -.08f * lift);
    }

    public static Vector3 BodyOffset(float distance, float weight, float scale = 1)
    {
        var phase = distance / (StrideLength * scale) * Mathf.Tau;
        var height = -.05f + (1 - Mathf.Cos(phase * 2)) * .02f;
        // Rise over the stance leg, but stay low enough to reach either planted foot.
        // Reference rig: hip-to-ankle height .861 m, safe leg reach .864 m.
        foreach (var right in new[] { false, true })
        {
            var foot = Foot(distance / scale, right);
            var forward = foot.Forward + .023f;
            var reachableHeight = foot.Lift - .861f + Mathf.Sqrt(.864f * .864f - forward * forward - .0001f);
            height = Mathf.Min(height, reachableHeight);
        }
        return new Vector3(Mathf.Sin(phase) * .005f * weight,
            height * weight, 0) * scale;
    }

    public static float ArmAngle(float distance, bool right, bool carrying, float weight, float scale = 1) =>
        Mathf.Cos(distance / (StrideLength * scale) * Mathf.Tau) * weight * (right ? -1 : 1) *
        (carrying && !right ? .045f : .10f);

    public static LegPose SolveLeg(Vector3 hip, Vector3 knee, Vector3 ankle, Vector3 target, float footPitch)
    {
        var upperRest = knee - hip; var lowerRest = ankle - knee;
        var upperLength = upperRest.Length(); var lowerLength = lowerRest.Length();
        var offset = target - hip;
        var direction = offset.LengthSquared() > .000001f ? offset.Normalized() : Vector3.Down;
        var distance = Mathf.Clamp(offset.Length(), Mathf.Abs(upperLength - lowerLength) + .001f, upperLength + lowerLength - .001f);
        var end = hip + direction * distance;
        // Anatomical forward is +Z in these glTF rigs; the knee must never bend backward.
        var pole = Vector3.Back - direction * Vector3.Back.Dot(direction);
        if (pole.LengthSquared() < .0001f) pole = Vector3.Up - direction * Vector3.Up.Dot(direction);
        pole = pole.Normalized();
        var along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2 * distance);
        var bend = Mathf.Sqrt(Mathf.Max(0, upperLength * upperLength - along * along));
        var joint = hip + direction * along + pole * bend;
        var upper = new Quaternion(upperRest.Normalized(), (joint - hip).Normalized()).Normalized();
        var lower = new Quaternion(lowerRest.Normalized(), (end - joint).Normalized()).Normalized();
        var foot = new Quaternion(Vector3.Right, footPitch);
        return new(upper, (upper.Inverse() * lower).Normalized(), (lower.Inverse() * foot).Normalized());
    }

    /// <summary>Round turns only where the swept curve remains clear; retain the exact destination.</summary>
    public static List<Vector3> RoundCorners(Vector3 start, IReadOnlyList<Vector3> path, Func<Vector3, Vector3, bool>? clear = null)
    {
        var result = new List<Vector3>();
        for (var i = 0; i < path.Count; i++)
        {
            var corner = path[i];
            if (i == path.Count - 1) { result.Add(corner); break; }
            var before = corner - (i == 0 ? start : path[i - 1]); var after = path[i + 1] - corner;
            if (before.Length() < .04f || after.Length() < .04f) { result.Add(corner); continue; }
            var incoming = before.Normalized(); var outgoing = after.Normalized();
            var dot = incoming.Dot(outgoing);
            if (dot > .99f) continue;
            if (dot < -.1f) { result.Add(corner); continue; }
            var radius = Mathf.Min(.12f, Mathf.Min(before.Length(), after.Length()) * .3f);
            var entry = corner - incoming * radius; var exit = corner + outgoing * radius;
            var curve = new List<Vector3> { entry };
            for (var step = 1; step <= 6; step++)
            {
                var t = step / 6f;
                curve.Add(entry * (1 - t) * (1 - t) + corner * (2 * t * (1 - t)) + exit * t * t);
            }
            var previous = result.Count > 0 ? result[^1] : start;
            var safe = true;
            foreach (var point in curve) { if (clear != null && !clear(previous, point)) { safe = false; break; } previous = point; }
            if (safe && (clear == null || clear(previous, path[i + 1]))) result.AddRange(curve);
            else result.Add(corner);
        }
        if (clear != null)
        {
            var previous = start;
            foreach (var point in result)
            {
                if (!clear(previous, point)) return [.. path];
                previous = point;
            }
        }
        return result;
    }
}
