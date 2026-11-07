# Copyright (c) 2026 Navin Ratnayake (@navinrtnk). All rights reserved.
"""Prepare original boutique audio; neural tools/models remain development-only.

Use an isolated venv with kokoro-onnx==0.4.7, numpy, scipy and soundfile.
Verified input URLs/hashes and generated output hashes live in Assets/Audio/sources.json.
"""
from pathlib import Path
import hashlib
import json
import math
import os
import urllib.request

os.environ.setdefault("OMP_NUM_THREADS", "2")
import numpy as np
import soundfile as sf
from scipy.signal import butter, sosfilt
from kokoro_onnx import Kokoro

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/Audio"
CACHE = ROOT / ".tools/audio-source"
RATE = 24000
RNG = np.random.default_rng(5821)
PHRASES = {
    "greeting": ["Neya! Poma leya?", "Oh! Tali meya, nava!"],
    "browse": ["Mmm... lemi tova!", "Ooh, keva lumay?"],
    "agree": ["Aya! Peli nova!", "Meya, tevi la!"],
    "thanks": ["Ah, nevi! Luma ley!", "Neya, pomi! Teya!"],
    "work": ["Mhm! Kevi naya.", "Opa! Lemi ta."],
}


def time(duration):
    return np.arange(round(duration * RATE)) / RATE


def tone(frequency, duration, decay=4):
    t = time(duration)
    return (np.sin(math.tau * frequency * t) + .20 * np.sin(math.tau * frequency * 2 * t)) * np.exp(-t * decay)


def noise(duration, low, high):
    samples = RNG.normal(0, 1, len(time(duration)))
    filtered = sosfilt(butter(2, [low, high], btype="bandpass", fs=RATE, output="sos"), samples)
    return filtered / max(.01, np.max(np.abs(filtered)))


def add(track, sample, start, gain=1, pan=0):
    offset = round(start * RATE)
    count = min(len(sample), len(track) - offset)
    if count <= 0:
        return
    if track.ndim == 1:
        track[offset:offset + count] += sample[:count] * gain
    else:
        track[offset:offset + count, 0] += sample[:count] * gain * math.sqrt((1 - pan) / 2)
        track[offset:offset + count, 1] += sample[:count] * gain * math.sqrt((1 + pan) / 2)


def save(name, samples, records, phrase=None, voice=None):
    samples = np.nan_to_num(samples).astype(np.float64)
    samples -= samples.mean(axis=0)
    peak = np.max(np.abs(samples))
    samples *= min(1, .78 / max(.001, peak))
    fade = min(240, len(samples) // 4)
    shape = np.linspace(0, 1, fade)
    if samples.ndim == 2:
        shape = shape[:, None]
    samples[:fade] *= shape
    samples[-fade:] *= shape[::-1]
    path = OUTPUT / (name + ".wav")
    sf.write(path, samples, RATE, subtype="PCM_16")
    entry = {"file": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
             "seconds": round(len(samples) / RATE, 5), "sample_rate": RATE,
             "channels": 1 if samples.ndim == 1 else 2}
    if phrase:
        entry.update(phrase=phrase, voice=voice)
    records.append(entry)
    print("Prepared", name, entry["seconds"], "seconds", flush=True)
    return samples


def effects(records):
    bell = np.zeros(len(time(.9)))
    for start, frequency in [(0, 1046.5), (.16, 1318.5)]:
        add(bell, tone(frequency, .65, 7), start, .28)
        add(bell, tone(frequency * 2.76, .32, 16), start, .035)
    add(bell, noise(.07, 300, 3000) * np.exp(-time(.07) * 55), 0, .10)
    save("door", bell, records)

    register = np.zeros(len(time(1.1)))
    for start in [0, .055, .12]:
        add(register, noise(.09, 1000, 8000) * np.exp(-time(.09) * 42), start, .20)
    for start, frequency in [(.13, 1318.5), (.22, 1568.0)]:
        add(register, tone(frequency, .70, 8), start, .30)
    add(register, noise(.45, 500, 5000) * np.sin(np.linspace(0, math.pi, len(time(.45)))) ** 2, .47, .08)
    save("checkout", register, records)

    pickup = noise(.75, 250, 6000)
    pickup *= (.4 + .6 * np.sin(time(.75) * 24) ** 2) * np.sin(np.linspace(0, math.pi, len(pickup))) ** 2
    add(pickup, tone(160, .18, 24), .42, .10)
    save("pickup", pickup * .30, records)

    place = np.zeros(len(time(.65)))
    add(place, tone(115, .22, 24), 0, .25)
    add(place, noise(.16, 350, 4500) * np.exp(-time(.16) * 28), 0, .28)
    add(place, tone(1568, .40, 13), .14, .12)
    save("place", place, records)

    for i in range(3):
        t = time(.16)
        step = noise(.16, 160, 2200) * np.exp(-t * (32 + i * 3)) * .19
        step += np.sin(math.tau * (95 - t * 95) * t) * np.exp(-t * 45) * .12
        save("step_" + str(i), step, records)


def music(records):
    # An original 16-bar, lightly swung boutique theme. No existing melody or samples.
    beat = 60 / 88
    length = beat * 64
    track = np.zeros((round(length * RATE), 2))
    chords = [[60, 64, 67, 71], [57, 60, 64, 71], [62, 65, 69, 72], [55, 59, 64, 69]]
    melodies = [[76, 79, 81, 79, 76, 74], [76, 72, 71, 72, 76, 79],
                [77, 81, 79, 77, 76, 74], [74, 71, 69, 67, 71, 74]]
    frequency = lambda midi: 440 * 2 ** ((midi - 69) / 12)
    for bar in range(16):
        start = bar * 4 * beat
        chord = chords[bar % 4]
        for offset in [0, 1.55, 2.5]:
            for note in chord:
                add(track, tone(frequency(note), beat * 1.5, 3.6), start + offset * beat, .045, -.30)
        for offset, note in [(0, chord[0] - 24), (2.5, chord[2] - 24)]:
            add(track, tone(frequency(note), beat * 1.3, 4.5), start + offset * beat, .10, .10)
        if bar % 4 != 3:
            melody = melodies[(bar // 2) % 4]
            for j, offset in enumerate([.5, 1, 1.75, 2.5, 3.25, 3.5]):
                add(track, tone(frequency(melody[j]), beat * .8, 7), start + offset * beat, .063, .35)
        for eighth in range(8):
            t = time(.10)
            shaker = noise(.10, 4500, 10000) * np.exp(-t * 55)
            add(track, shaker, start + (eighth * .5 + (.07 if eighth % 2 else 0)) * beat, .022, -.45)
        for offset in [0, 2]:
            t = time(.16)
            kick = np.sin(math.tau * (74 - t * 90) * t) * np.exp(-t * 38)
            add(track, kick, start + offset * beat, .065)
    # Gentle saturation keeps the percussive accents soft under conversation.
    track = np.tanh(track * 1.6) * .55
    save("boutique_theme", track, records)


def voices(records):
    inputs = json.loads((OUTPUT / "sources.json").read_text())["development_inputs"]
    CACHE.mkdir(parents=True, exist_ok=True)
    for source in inputs:
        target = CACHE / source["file"]
        if not target.exists():
            with urllib.request.urlopen(source["url"], timeout=90) as response:
                target.write_bytes(response.read())
        if hashlib.sha256(target.read_bytes()).hexdigest() != source["sha256"]:
            raise ValueError("Audio model input hash mismatch: " + target.name)
    engine = Kokoro(str(CACHE / "kokoro-v1.0.int8.onnx"), str(CACHE / "voices-v1.0.bin"))
    envelopes = {}
    for gender, voice in [("female", "af_heart"), ("male", "am_michael")]:
        for cue, phrases in PHRASES.items():
            for i, phrase in enumerate(phrases):
                phonemes = engine.tokenizer.phonemize(phrase, "en-us")
                tokens = engine.tokenizer.tokenize(phonemes)
                # Explicit float32 speed matches this pinned ONNX model's input contract.
                samples = engine.sess.run(None, {
                    "input_ids": np.asarray([[0, *tokens, 0]], dtype=np.int64),
                    "style": np.asarray(engine.get_voice_style(voice)[len(tokens)], dtype=np.float32),
                    "speed": np.asarray([1.12 if gender == "female" else 1.07], dtype=np.float32),
                })[0].reshape(-1)
                active = np.flatnonzero(np.abs(samples) > .003)
                if len(active):
                    samples = samples[max(0, active[0] - 720):min(len(samples), active[-1] + 1680)]
                samples = sosfilt(butter(2, 75, btype="highpass", fs=RATE, output="sos"), samples)
                samples *= .62 / max(.01, np.max(np.abs(samples)))
                name = f"{gender}_{cue}_{i}"
                samples = save(name, samples, records, phrase, voice)
                rms = np.array([np.sqrt(np.mean(chunk ** 2)) for chunk in np.array_split(samples, math.ceil(len(samples) / 960))])
                values = np.clip((rms - .008) / max(.01, np.quantile(rms, .9)), 0, 1)
                envelopes[name] = {"seconds": round(len(samples) / RATE, 5), "mouth": np.round(values, 3).tolist()}
    (OUTPUT / "voices.json").write_text(json.dumps(envelopes, indent=2) + "\n")


if __name__ == "__main__":
    OUTPUT.mkdir(parents=True, exist_ok=True)
    manifest = json.loads((OUTPUT / "sources.json").read_text())
    outputs = []
    effects(outputs)
    music(outputs)
    voices(outputs)
    manifest["outputs"] = outputs
    (OUTPUT / "sources.json").write_text(json.dumps(manifest, indent=2) + "\n")
