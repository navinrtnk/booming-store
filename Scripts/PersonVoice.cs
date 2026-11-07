// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using Godot;

namespace BoomingStore;

/// <summary>One character's vocal timing, spatial playback and smoothed mouth movement.</summary>
public sealed class PersonVoice
{
    private readonly StoreAudio _audio;
    private readonly AudioStreamPlayer3D _player;
    private readonly bool _feminine;
    private readonly float _pitch;
    private VoiceEnvelope? _envelope;
    private VoiceLine? _next;
    private float _delay, _cooldown;
    public bool IsSpeaking => _envelope != null;
    public float MouthAmount { get; private set; }

    public PersonVoice(PersonActor actor, StoreAudio audio, int profile, bool shopkeeper)
    {
        _audio = audio;
        _feminine = shopkeeper || profile % 5 is 1 or 3 or 4;
        _pitch = shopkeeper ? 1.035f : .96f + profile % 4 * .025f;
        _player = new AudioStreamPlayer3D { Name = "CharacterVoice", Bus = "Voices", UnitSize = 10, MaxDistance = 40, PanningStrength = .65f, VolumeDb = -2 };
        actor.AddChild(_player);
    }
    public void Request(VoiceLine? line, float delay)
    {
        if (line == null || _cooldown > 0 && line != VoiceLine.Thanks) return;
        _next = line; _delay = delay;
    }
    public void Advance(float realDelta)
    {
        _cooldown = Mathf.Max(0, _cooldown - realDelta);
        if (_envelope != null && !_player.Playing) _envelope = null;
        if (_next is { } line)
        {
            _delay -= realDelta;
            if (_delay <= 0 && !IsSpeaking)
            {
                _envelope = _audio.Speak(_player, line, _feminine, _pitch);
                _next = null; _cooldown = 5.5f;
            }
        }
        var desiredMouth = _envelope?.At((float)_player.GetPlaybackPosition()) ?? 0;
        MouthAmount = Mathf.Lerp(MouthAmount, desiredMouth, 1 - Mathf.Exp(-25 * realDelta));
    }
    public void SetPaused(bool paused) => _player.StreamPaused = paused;
    public void Stop()
    {
        if (GodotObject.IsInstanceValid(_player)) { _player.StreamPaused = false; _player.Stop(); }
        _envelope = null; _next = null; MouthAmount = 0;
    }
    public void Release()
    {
        Stop();
        if (GodotObject.IsInstanceValid(_player)) _player.Stream = null;
    }
}

public sealed class VoiceEnvelope
{
    public float Seconds { get; set; }
    public float[] Mouth { get; set; } = [];
    public float At(float seconds)
    {
        if (Seconds <= 0 || Mouth.Length == 0 || seconds < 0 || seconds >= Seconds) return 0;
        var position = seconds / Seconds * (Mouth.Length - 1);
        var first = (int)position; var next = Math.Min(first + 1, Mouth.Length - 1);
        return Math.Clamp(Mouth[first] + (Mouth[next] - Mouth[first]) * (position - first), 0, 1);
    }
}
