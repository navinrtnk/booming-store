// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using System.Text.Json;
using Godot;

namespace BoomingStore;

public enum ShopSound { Door, Checkout, Pickup, Place, Step }
public enum VoiceLine { Greeting, Browse, Agree, Thanks, Work }

/// <summary>Bounded spatial effects, character voices and a quiet original shop theme.</summary>
public partial class StoreAudio : Node
{
    private const int StoreChannels = 4, TotalChannels = 10, VoiceLimit = 2;
    private static readonly string[] StoreClips = ["door", "checkout", "pickup", "place"];
    private static readonly string[] StepClips = ["step_0", "step_1", "step_2"];
    private StoreGame _game = null!;
    private readonly Dictionary<string, AudioStream> _streams = [];
    private readonly List<AudioStreamPlayer3D> _effects = [];
    private readonly Dictionary<ShopSound, int> _eventCounts = [];
    private Dictionary<string, VoiceEnvelope> _envelopes = [];
    private AudioStreamPlayer _music = null!;
    private int _voiceBus, _effectsBus, _musicBus;
    private readonly Random _random = new(5821);
    private bool _paused;
    private bool _shuttingDown;
    private float _duck = 1;
    public IReadOnlyDictionary<ShopSound, int> EventCounts => _eventCounts;
    public int SpokenLines { get; private set; }
    public int EnvelopeCount => _envelopes.Count;
    public bool IsPaused => _paused;
    public void Build(StoreGame game)
    {
        _game = game;
        _voiceBus = EnsureBus("Voices"); _effectsBus = EnsureBus("Effects"); _musicBus = EnsureBus("Music");
        AudioServer.AddBusEffect(0, new AudioEffectHardLimiter { CeilingDb = -1.5f });
        _envelopes = JsonSerializer.Deserialize<Dictionary<string, VoiceEnvelope>>(
            Godot.FileAccess.GetFileAsString("res://Assets/Audio/voices.json"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        // Load short cues before trading so first arrivals and purchases need no resource load.
        foreach (var name in StoreClips) Stream(name);
        foreach (var name in StepClips) Stream(name);
        foreach (var name in _envelopes.Keys) Stream(name);
        for (var i = 0; i < TotalChannels; i++)
        {
            var player = new AudioStreamPlayer3D { Name = "Effect" + i, Bus = "Effects", UnitSize = 9, MaxDistance = 40, PanningStrength = .65f };
            AddChild(player); _effects.Add(player);
        }
        var theme = (AudioStreamWav)Stream("boutique_theme");
        theme.LoopMode = AudioStreamWav.LoopModeEnum.Forward; theme.LoopBegin = 0; theme.LoopEnd = (int)(theme.GetLength() * theme.MixRate);
        _music = new AudioStreamPlayer { Name = "BoutiqueTheme", Bus = "Music", Stream = theme, VolumeDb = -6 };
        AddChild(_music); ApplyPreferences(); _music.Play();
    }
    private static int EnsureBus(string name)
    {
        var index = AudioServer.GetBusIndex(name);
        if (index >= 0) return index;
        AudioServer.AddBus(); index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, name); AudioServer.SetBusSend(index, "Master");
        return index;
    }
    private AudioStream Stream(string name)
    {
        if (!_streams.TryGetValue(name, out var stream)) _streams[name] = stream = GD.Load<AudioStream>("res://Assets/Audio/" + name + ".wav");
        return stream;
    }
    public void Play(ShopSound sound, Vector3 position)
    {
        if (_paused || _shuttingDown) return;
        var footstep = sound == ShopSound.Step;
        var player = FindEffectPlayer(footstep);
        if (player == null) return;
        var name = footstep ? StepClips[_random.Next(StepClips.Length)] : StoreClips[(int)sound];
        player.Stream = Stream(name); player.Position = position; player.PitchScale = .96f + (float)_random.NextDouble() * .08f;
        player.VolumeDb = footstep ? -10 : -1; player.Play();
        _eventCounts[sound] = _eventCounts.GetValueOrDefault(sound) + 1;
    }
    private AudioStreamPlayer3D? FindEffectPlayer(bool footstep)
    {
        // Store events have reserved channels and always play, even during many footsteps.
        var first = footstep ? StoreChannels : 0;
        var end = footstep ? _effects.Count : StoreChannels;
        for (var i = first; i < end; i++) if (!_effects[i].Playing) return _effects[i];
        if (footstep) return null;
        _effects[0].Stop();
        return _effects[0];
    }
    private int SpeakingCount(int limit)
    {
        var count = _game.Keeper?.Actor.IsSpeaking == true ? 1 : 0;
        foreach (var shopper in _game.Shoppers)
            if (shopper.IsSpeaking && ++count >= limit) break;
        return count;
    }
    public VoiceEnvelope? Speak(AudioStreamPlayer3D player, VoiceLine line, bool feminine, float pitch)
    {
        if (_paused || _shuttingDown || SpeakingCount(VoiceLimit) >= VoiceLimit) return null;
        var name = (feminine ? "female_" : "male_") + line.ToString().ToLowerInvariant() + "_" + _random.Next(2);
        player.Stream = Stream(name); player.PitchScale = pitch; player.Play(); SpokenLines++;
        return _envelopes[name];
    }
    public void SetPaused(bool paused)
    {
        _paused = paused; _music.StreamPaused = paused;
        foreach (var player in _effects) player.StreamPaused = paused;
        foreach (var actor in _game.People) actor.PauseVoice(paused);
    }
    public void ApplyPreferences()
    {
        var p = _game.Preferences;
        AudioServer.SetBusMute(0, p.Muted);
        SetVolume(0, p.Master); SetVolume(_voiceBus, p.Voices); SetVolume(_effectsBus, p.Effects); SetVolume(_musicBus, p.Music * _duck);
    }
    private static void SetVolume(int bus, float volume) => AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(.0001f, volume)));
    public override void _Process(double delta)
    {
        if (_shuttingDown) return;
        var speaking = SpeakingCount(1) > 0;
        var next = Mathf.Lerp(_duck, speaking ? .40f : 1, 1 - Mathf.Exp(-(float)delta * 4));
        if (Mathf.Abs(next - _duck) > .001f) { _duck = next; SetVolume(_musicBus, _game.Preferences.Music * _duck); }
    }
    public void StopAll()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;
        foreach (var actor in _game.People) if (GodotObject.IsInstanceValid(actor)) actor.StopVoice();
        foreach (var player in _effects) { player.StreamPaused = false; player.Stop(); player.Stream = null; }
        if (_music != null) { _music.StreamPaused = false; _music.Stop(); _music.Stream = null; }
        _streams.Clear();
    }
    public override void _ExitTree() => StopAll();
}
