using System.Collections.Generic;
using UnityEngine;

namespace BlockBlast
{
    /// <summary>Sound effects. Every clip is synthesized at runtime, so the game has sound with no audio assets.
    /// Drop an AudioClip with the same name into Assets/Resources/Audio to replace any of them.</summary>
    public static class Sfx
    {
        const int Voices = 8;

        static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        static AudioSource[] sources;
        static int next;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt("sound", 1) == 1;
            set { PlayerPrefs.SetInt("sound", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Play(string name, float pitch = 1f)
        {
            if (!Enabled) return;
            var clip = Get(name);
            if (!clip) return;
            if (sources == null || !sources[0])
            {
                var go = new GameObject("[Sfx]");
                Object.DontDestroyOnLoad(go);
                sources = new AudioSource[Voices];
                for (int i = 0; i < Voices; i++)
                {
                    sources[i] = go.AddComponent<AudioSource>();
                    sources[i].playOnAwake = false;
                }
            }
            // Round-robin voices so a pitched sound never re-pitches one that is still ringing.
            var src = sources[next];
            next = (next + 1) % Voices;
            src.pitch = pitch;
            src.clip = clip;
            src.Play();
        }

        /// <summary>Builds every clip up front so the first tap doesn't hitch.</summary>
        public static void Preload()
        {
            foreach (var name in new[] { "click", "pick", "place", "drop", "clear", "combo", "bomb", "bonus", "gameover", "newbest", "win" })
                Get(name);
        }

        static AudioClip Get(string name)
        {
            if (clips.TryGetValue(name, out var c) && c) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (!c) c = Build(name);
            clips[name] = c;
            return c;
        }

        static AudioClip Build(string name)
        {
            var s = new Synth(name == "bomb" ? .9f : name == "gameover" ? 1.4f : name == "win" || name == "newbest" ? 1.3f : .6f);
            switch (name)
            {
                case "click":
                    s.Tone(0, .05f, 1300, 750, Wave.Sine, .55f, decay: 35);
                    s.Tone(0, .03f, 2600, 2600, Wave.Triangle, .12f, decay: 80);
                    break;
                case "pick":
                    s.Tone(0, .08f, 480, 900, Wave.Sine, .4f, decay: 18);
                    break;
                case "place":
                    s.Tone(0, .14f, 240, 85, Wave.Sine, .8f, decay: 16);
                    s.Tone(0, .05f, 3500, 1200, Wave.Noise, .3f, decay: 45);
                    break;
                case "drop":
                    s.Tone(0, .1f, 620, 340, Wave.Triangle, .35f, decay: 18);
                    break;
                case "clear":
                    int[] up = { 84, 88, 91, 96 };
                    for (int i = 0; i < up.Length; i++)
                        s.Tone(i * .045f, .3f, Synth.Midi(up[i]), Synth.Midi(up[i]), Wave.Pluck, .3f, decay: 9);
                    s.Tone(0, .25f, 9000, 5000, Wave.Noise, .1f, decay: 14);
                    break;
                case "combo":
                    int[] run = { 72, 76, 79, 84, 88 };
                    for (int i = 0; i < run.Length; i++)
                    {
                        float f = Synth.Midi(run[i]);
                        s.Tone(i * .05f, .16f, f, f, Wave.Square, .1f, decay: 10);
                        s.Tone(i * .05f, .2f, f, f, Wave.Pluck, .22f, decay: 10);
                    }
                    break;
                case "bomb":
                    s.Tone(0, .75f, 2600, 180, Wave.Noise, 1f, decay: 5);
                    s.Tone(0, .5f, 120, 32, Wave.Sine, .9f, decay: 6);
                    break;
                case "bonus":
                    int[] sparkle = { 91, 96, 100, 103 };
                    for (int i = 0; i < sparkle.Length; i++)
                        s.Tone(i * .06f, .25f, Synth.Midi(sparkle[i]), Synth.Midi(sparkle[i]), Wave.Pluck, .25f, decay: 10);
                    break;
                case "gameover":
                    int[] down = { 67, 64, 60, 55 };
                    for (int i = 0; i < down.Length; i++)
                    {
                        float f = Synth.Midi(down[i]);
                        float len = i == down.Length - 1 ? .7f : .2f;
                        s.Tone(i * .22f, len, f, i == down.Length - 1 ? f * .94f : f, Wave.Triangle, .45f, release: .15f, decay: 2);
                        s.Tone(i * .22f, len, f * .5f, f * .5f, Wave.Square, .06f, release: .15f, decay: 3);
                    }
                    break;
                case "newbest":
                case "win":
                    int[] fanfare = { 72, 76, 79 };
                    for (int i = 0; i < fanfare.Length; i++)
                    {
                        float f = Synth.Midi(fanfare[i]);
                        s.Tone(i * .12f, .11f, f, f, Wave.Square, .12f);
                        s.Tone(i * .12f, .11f, f, f, Wave.Triangle, .3f);
                    }
                    foreach (int m in new[] { 72, 76, 79, 84 })
                    {
                        float f = Synth.Midi(m);
                        s.Tone(.36f, .6f, f, f, Wave.Triangle, .2f, release: .3f, decay: 1.5f);
                        s.Tone(.36f, .6f, f, f, Wave.Square, .04f, release: .3f, decay: 2f);
                    }
                    s.Tone(.36f, .6f, 9000, 6000, Wave.Noise, .05f, decay: 6);
                    break;
                default:
                    return null;
            }
            return s.ToClip(name);
        }
    }

    /// <summary>Looping background music, composed and synthesized at startup (a short C-major chiptune loop).
    /// An AudioClip at Assets/Resources/Audio/music replaces it.</summary>
    public static class Music
    {
        const float Volume = .4f;
        static AudioSource source;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt("music", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("music", value ? 1 : 0);
                PlayerPrefs.Save();
                if (value) Play(); else if (source) source.Stop();
            }
        }

        public static void Play()
        {
            if (!Enabled) return;
            if (!source)
            {
                var go = new GameObject("[Music]");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.volume = Volume;
                var custom = Resources.Load<AudioClip>("Audio/music");
                source.clip = custom ? custom : Compose();
            }
            if (!source.isPlaying) source.Play();
        }

        /// <summary>Briefly lowers the music under a jingle (level complete, game over).</summary>
        public static void Duck(float seconds)
        {
            if (!source) return;
            Tween.Run(source, "duck", .15f, k => source.volume = Mathf.Lerp(Volume, Volume * .25f, k), Ease.Linear, 0,
                () => Tween.Run(source, "duck", .8f, k => source.volume = Mathf.Lerp(Volume * .25f, Volume, k),
                    Ease.Linear, seconds));
        }

        // Chord roots / triads for 8 bars: C G Am F | C G F G
        static readonly int[] Roots = { 48, 43, 45, 41, 48, 43, 41, 43 };
        static readonly int[][] Triads =
        {
            new[] { 60, 64, 67 }, new[] { 59, 62, 67 }, new[] { 57, 60, 64 }, new[] { 57, 60, 65 },
            new[] { 60, 64, 67 }, new[] { 59, 62, 67 }, new[] { 57, 60, 65 }, new[] { 59, 62, 67 },
        };

        // Melody per bar: (beat, midi note, length in beats).
        static readonly float[][][] Melody =
        {
            new[] { new[] { 0f, 76, 1 }, new[] { 1f, 79, 1 }, new[] { 2f, 81, .5f }, new[] { 2.5f, 79, .5f }, new[] { 3f, 76, 1 } },
            new[] { new[] { 0f, 74, 1.5f }, new[] { 1.5f, 76, .5f }, new[] { 2f, 79, 2 } },
            new[] { new[] { 0f, 81, 1 }, new[] { 1f, 79, .5f }, new[] { 1.5f, 76, .5f }, new[] { 2f, 72, 2 } },
            new[] { new[] { 0f, 74, .5f }, new[] { .5f, 76, .5f }, new[] { 1f, 72, 1 }, new[] { 2f, 69, 2 } },
            new[] { new[] { 0f, 76, .5f }, new[] { .5f, 79, .5f }, new[] { 1f, 84, 1 }, new[] { 2f, 81, 1 }, new[] { 3f, 79, 1 } },
            new[] { new[] { 0f, 79, 1 }, new[] { 1f, 76, 1 }, new[] { 2f, 74, 2 } },
            new[] { new[] { 0f, 72, 1 }, new[] { 1f, 74, .5f }, new[] { 1.5f, 76, .5f }, new[] { 2f, 77, 1 }, new[] { 3f, 76, 1 } },
            new[] { new[] { 0f, 74, 2 }, new[] { 2f, 71, 1 }, new[] { 3f, 74, 1 } },
        };

        static AudioClip Compose()
        {
            const float bpm = 104;
            float beat = 60f / bpm, bar = beat * 4;
            var s = new Synth(bar * Roots.Length, 32000, wrap: true);

            for (int b = 0; b < Roots.Length; b++)
            {
                float t0 = b * bar;
                // soft pad
                foreach (int m in Triads[b])
                    s.Tone(t0, bar, Synth.Midi(m), Synth.Midi(m), Wave.Sine, .045f, attack: .25f, release: .4f);
                // bouncy bass
                float root = Synth.Midi(Roots[b]);
                foreach (float at in new[] { 0f, 1.5f, 2f, 3f })
                    s.Tone(t0 + at * beat, beat * .45f, root, root, Wave.Triangle, .3f, release: .05f, decay: 3);
                // eighth-note arpeggio
                int[] arp = { 0, 1, 2, 1, 0, 1, 2, 1 };
                for (int i = 0; i < 8; i++)
                {
                    float f = Synth.Midi(Triads[b][arp[i]] + 12);
                    s.Tone(t0 + i * beat * .5f, beat * .4f, f, f, Wave.Pluck, .07f, decay: 7);
                }
                // melody
                foreach (var n in Melody[b])
                {
                    float f = Synth.Midi((int)n[1]);
                    s.Tone(t0 + n[0] * beat, n[2] * beat * .9f, f, f, Wave.Triangle, .17f, attack: .01f, release: .08f, decay: .8f);
                    s.Tone(t0 + n[0] * beat, n[2] * beat * .9f, f, f, Wave.Square, .025f, attack: .01f, release: .08f, decay: 1.5f);
                }
                // light drums: kick on 1 and 3, hat on the off-beats
                for (int i = 0; i < 4; i++)
                {
                    if (i % 2 == 0) s.Tone(t0 + i * beat, .14f, 130, 45, Wave.Sine, .45f, decay: 14);
                    s.Tone(t0 + (i + .5f) * beat, .04f, 9000, 9000, Wave.Noise, .06f, decay: 60);
                }
            }
            return s.ToClip("music");
        }
    }

    public enum Wave { Sine, Triangle, Square, Pluck, Noise }

    /// <summary>Tiny additive synth that renders tones into a float buffer.</summary>
    public sealed class Synth
    {
        readonly float[] buf;
        readonly int rate;
        readonly bool wrap;
        readonly System.Random rng = new System.Random(7);

        public Synth(float seconds, int rate = 44100, bool wrap = false)
        {
            this.rate = rate;
            this.wrap = wrap;
            buf = new float[Mathf.CeilToInt(seconds * rate)];
        }

        public static float Midi(int note) => 440f * Mathf.Pow(2, (note - 69) / 12f);

        /// <summary>Adds a tone starting at <paramref name="start"/> s. Frequency glides exponentially from f0 to f1;
        /// for Noise, the frequency is the low-pass cutoff instead.</summary>
        public void Tone(float start, float duration, float f0, float f1, Wave wave, float volume,
            float attack = .004f, float release = .03f, float decay = 0f)
        {
            int begin = Mathf.RoundToInt(start * rate);
            int count = Mathf.CeilToInt((duration + release) * rate);
            double phase = 0;
            float lp = 0;
            for (int i = 0; i < count; i++)
            {
                int idx = begin + i;
                if (idx >= buf.Length)
                {
                    if (!wrap) break;
                    idx %= buf.Length;
                }
                float t = (float)i / rate;
                float k = Mathf.Clamp01(t / duration);
                float f = f0 * Mathf.Pow(f1 / f0, k);

                float env = (t < attack ? t / attack : 1f) * Mathf.Exp(-decay * t);
                if (t > duration) env *= 1f - (t - duration) / release;

                float v;
                if (wave == Wave.Noise)
                {
                    float a = 1f - Mathf.Exp(-2f * Mathf.PI * f / rate);
                    lp += a * ((float)rng.NextDouble() * 2 - 1 - lp);
                    v = lp;
                }
                else
                {
                    phase += f / rate;
                    float p = (float)(phase - System.Math.Floor(phase));
                    switch (wave)
                    {
                        case Wave.Triangle: v = 1 - 4 * Mathf.Abs(p - .5f); break;
                        case Wave.Square: v = p < .5f ? 1 : -1; break;
                        case Wave.Pluck:
                            float w = 2 * Mathf.PI * p;
                            v = (Mathf.Sin(w) + .5f * Mathf.Sin(2 * w) + .25f * Mathf.Sin(3 * w)) * .57f;
                            break;
                        default: v = Mathf.Sin(2 * Mathf.PI * p); break;
                    }
                }
                buf[idx] += v * env * volume;
            }
        }

        public AudioClip ToClip(string name)
        {
            float peak = 0;
            foreach (float v in buf) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > .9f)
                for (int i = 0; i < buf.Length; i++) buf[i] *= .9f / peak;
            var clip = AudioClip.Create(name, buf.Length, 1, rate, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }
}
