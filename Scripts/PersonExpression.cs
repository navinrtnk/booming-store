// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

public enum PersonBeat { Greeting, Admire, Chat, Thanks, Work }

/// <summary>Small, smoothly blended social gestures, separate from travel and task state.</summary>
public sealed class PersonExpression
{
    public readonly record struct Pose(Vector3 Body, Vector3 Torso, Vector3 Head, float Smile, float Wave, float Explain, float Weight);
    public PersonBeat Beat { get; private set; }
    public Vector3? LookAt { get; private set; }
    public float Age { get; private set; } = 10;
    public float Duration { get; private set; } = 1;
    public void Start(PersonBeat beat, Vector3? target, float duration = 2.6f)
    { Beat = beat; LookAt = target; Duration = Math.Max(.2f, duration); Age = 0; }
    public void Advance(float delta) => Age = Mathf.Min(Duration + 1, Age + Math.Max(0, delta));
    private static float Ease(float value) { var t = Mathf.Clamp(value, 0, 1); return t * t * (3 - 2 * t); }
    public Pose Sample(bool shopkeeper)
    {
        var fade = Ease(Age / .25f) * Ease((Duration - Age) / .5f);
        var nod = Mathf.Sin(Age * 5.5f) * .045f;
        var wave = Beat == PersonBeat.Greeting ? fade : 0;
        var explain = Beat is PersonBeat.Chat or PersonBeat.Admire or PersonBeat.Thanks ? fade : 0;
        var warmth = shopkeeper ? 1 : .75f;
        return new(new Vector3(Mathf.Sin(Age * 2) * .008f, 0, .008f) * fade,
            new Vector3(.025f, Mathf.Sin(Age * 2.4f) * .025f, -.02f) * fade,
            new Vector3(nod, 0, .035f * warmth) * fade, fade * .22f * warmth, wave, explain, fade);
    }
}
