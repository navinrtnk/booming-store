// Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace BoomingStore.Tests;

public class PresentationTests
{
    [Fact]
    public void PreferencesRoundTripSeparatelyFromStoreData()
    {
        var path = Path.Combine(Path.GetTempPath(), "booming-preferences-" + Guid.NewGuid() + ".json");
        try
        {
            var settings = new GamePreferences { Fullscreen = true, Muted = true, Music = .2f, Voices = .55f };
            settings.Save(path); var loaded = GamePreferences.Load(path);
            Assert.True(loaded.Fullscreen && loaded.Muted); Assert.Equal(.55f, loaded.Voices); Assert.Equal(.2f, loaded.Music);
            File.WriteAllText(path, "broken"); Assert.False(GamePreferences.Load(path).Fullscreen);
            Assert.DoesNotContain("Fullscreen", new StoreState().Serialize());
        }
        finally { File.Delete(path); File.Delete(path + ".tmp"); }
    }

    [Theory]
    [InlineData("{\"Music\":-1}")] [InlineData("{\"Voices\":1.1}")] [InlineData("null")]
    [InlineData("{\"Master\":1e100}")]
    public void InvalidVolumesCannotEnterPresentationSettings(string json) =>
        Assert.Throws<InvalidDataException>(() => GamePreferences.Parse(json));

    [Fact]
    public void LegacyAutomaticCheckoutCannotBeRestoredFromAnOldSave()
    {
        var oldSave = Fixtures.Shop(Fixtures.Item()).Serialize().Insert(1, "\"AutoCheckout\":true,");
        Assert.DoesNotContain("AutoCheckout", StoreState.Parse(oldSave).Serialize());
    }

    [Fact]
    public void MouthEnvelopeIsBoundedAndClosesOutsideTheSpokenClip()
    {
        var envelope = new VoiceEnvelope { Seconds = 2, Mouth = [0, 1, .3f, 0] };
        Assert.Equal(0, envelope.At(-1)); Assert.Equal(0, envelope.At(2));
        Assert.InRange(envelope.At(.5f), .7f, .8f);
        for (var sample = 0; sample < 200; sample++) Assert.InRange(envelope.At(sample / 100f), 0, 1);
    }

    [Theory]
    [InlineData(PersonBeat.Greeting)] [InlineData(PersonBeat.Admire)] [InlineData(PersonBeat.Chat)]
    [InlineData(PersonBeat.Thanks)] [InlineData(PersonBeat.Work)]
    public void SocialGesturesEaseInStayRestrainedAndSettle(PersonBeat beat)
    {
        var expression = new PersonExpression(); expression.Start(beat, null);
        Assert.Equal(0, expression.Sample(true).Weight);
        var greatest = 0f;
        for (var frame = 0; frame < 240; frame++)
        {
            expression.Advance(1 / 60f); var pose = expression.Sample(true); greatest = Math.Max(greatest, pose.Weight);
            Assert.InRange(pose.Body.Length(), 0, .02f); Assert.InRange(pose.Head.Length(), 0, .07f);
            Assert.InRange(pose.Torso.Length(), 0, .06f); Assert.InRange(pose.Smile, 0, .221f);
            Assert.InRange(pose.Wave, 0, 1); Assert.InRange(pose.Explain, 0, 1);
        }
        Assert.Equal(1, greatest); Assert.Equal(0, expression.Sample(true).Weight);
    }

    [Fact]
    public void RuntimeAudioMatchesItsManifestAndHasNoClippedSamples()
    {
        var root = FindRoot();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Assets/Audio/sources.json")));
        var clips = manifest.RootElement.GetProperty("outputs").EnumerateArray().ToArray(); Assert.True(clips.Length >= 28);
        foreach (var clip in clips)
        {
            var bytes = File.ReadAllBytes(Path.Combine(root, "Assets/Audio", clip.GetProperty("file").GetString()!));
            Assert.Equal(clip.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
            using var reader = new BinaryReader(new MemoryStream(bytes));
            Assert.Equal("RIFF", new string(reader.ReadChars(4))); reader.ReadInt32(); Assert.Equal("WAVE", new string(reader.ReadChars(4)));
            var channels = 0; var dataBytes = 0; var squareSum = 0d; var count = 0;
            while (reader.BaseStream.Position + 8 <= bytes.Length)
            {
                var name = new string(reader.ReadChars(4)); var size = reader.ReadInt32(); var end = reader.BaseStream.Position + size;
                if (name == "fmt ")
                {
                    Assert.Equal(1, reader.ReadInt16()); channels = reader.ReadInt16(); Assert.InRange(channels, 1, 2);
                    Assert.Equal(24000, reader.ReadInt32()); reader.ReadInt32(); reader.ReadInt16(); Assert.Equal(16, reader.ReadInt16());
                }
                if (name == "data")
                {
                    dataBytes = size;
                    for (var i = 0; i < size / 2; i++)
                    {
                        var sample = reader.ReadInt16() / 32768d;
                        Assert.InRange(sample, -.79, .79); squareSum += sample * sample; count++;
                    }
                }
                reader.BaseStream.Position = end + (size % 2);
            }
            Assert.True(dataBytes > 100 && channels > 0); Assert.InRange(Math.Sqrt(squareSum / count), .003, .4);
            Assert.InRange(Math.Abs(dataBytes / (24000d * channels * 2) - clip.GetProperty("seconds").GetDouble()), 0, .0001);
        }
        var envelopes = JsonSerializer.Deserialize<Dictionary<string, VoiceEnvelope>>(
            File.ReadAllText(Path.Combine(root, "Assets/Audio/voices.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.Equal(20, envelopes.Count);
        Assert.All(envelopes.Values, e => { Assert.InRange(e.Seconds, .4f, 4); Assert.Contains(e.Mouth, value => value > .5f); Assert.All(e.Mouth, value => Assert.InRange(value, 0, 1)); });
    }
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(directory.FullName, "project.godot"))) directory = directory.Parent ?? throw new DirectoryNotFoundException();
        return directory.FullName;
    }
}
