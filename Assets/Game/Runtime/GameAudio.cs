using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zombs
{
    internal sealed class GameAudio : IDisposable
    {
        private enum Cue
        {
            Wood, Ore, Impact, Explosion, PlayerHit, Place, Upgrade, Fuel, Ready, PowerDown, PowerUp, Wave, Boss, Defeat
        }

        private enum Channel { Harvest, Impact, Blast, Player, Build, Machine, Signal }

        private const int SampleRate = 22050;
        private readonly AudioSource oneShots, wind, generator;
        private readonly AudioClip[] clips = new AudioClip[Enum.GetValues(typeof(Cue)).Length];
        private readonly float[] nextCue = new float[Enum.GetValues(typeof(Channel)).Length];

        public GameAudio(GameObject host)
        {
            oneShots = Source(host);
            wind = Source(host, Loop("Basalt wind", 6, Wind));
            generator = Source(host, Loop("Generator motor", 2, Motor));

            clips[(int)Cue.Wood] = OneShot("Pick on wood", .15f,
                (t, noise) => .50f * Decay(t, 25) * Tone(t, 160, -190) + .42f * Decay(t, 48) * noise);
            clips[(int)Cue.Ore] = OneShot("Pick on rock", .18f,
                (t, noise) => Decay(t, 32) * (.33f * noise + .45f * Tone(t, 610) + .13f * Tone(t, 1110)));
            clips[(int)Cue.Impact] = OneShot("Combat impact", .09f,
                (t, noise) => Decay(t, 42) * (.58f * Tone(t, 175, -680) + .40f * noise));
            clips[(int)Cue.Explosion] = OneShot("Blast", .32f,
                (t, noise) => .55f * Decay(t, 12) * Tone(t, 91, -100) + .44f * Decay(t, 18) * noise);
            clips[(int)Cue.PlayerHit] = OneShot("Suit impact", .20f,
                (t, noise) => Decay(t, 24) * (.45f * Tone(t, 210, -620) + .28f * noise));
            clips[(int)Cue.Place] = OneShot("Structure locked", .25f,
                (t, noise) => Decay(t, 29) * (.45f * Tone(t, 115) + .34f * noise) + Bell(t - .07f, 460) * .39f);
            clips[(int)Cue.Upgrade] = OneShot("Upgrade installed", .44f,
                (t, _) => .31f * (Bell(t, 350) + Bell(t - .08f, 465) + Bell(t - .16f, 585)));
            clips[(int)Cue.Fuel] = OneShot("Fuel loaded", .25f,
                (t, noise) => .28f * Decay(t, 32) * noise + .42f * Bell(t - .05f, 270) + .23f * Bell(t - .10f, 385));
            clips[(int)Cue.Ready] = OneShot("Machine output ready", .30f,
                (t, _) => .31f * (Bell(t, 700) + Bell(t - .09f, 875)));
            clips[(int)Cue.PowerDown] = OneShot("Generator offline", .52f,
                (t, _) => .32f * (Bell(t, 405) + Bell(t - .15f, 245) + Bell(t - .28f, 160)));
            clips[(int)Cue.PowerUp] = OneShot("Generator online", .50f,
                (t, _) => .40f * Decay(t, 7) * Tone(t, 115, 350) + .36f * Bell(t - .20f, 470));
            clips[(int)Cue.Wave] = OneShot("Night warning", .83f,
                (t, _) => Alarm(t, 225) + Alarm(t - .30f, 190));
            clips[(int)Cue.Boss] = OneShot("Boss warning", 1.18f,
                (t, _) => Alarm(t, 155) + Alarm(t - .30f, 155) + Alarm(t - .60f, 125));
            clips[(int)Cue.Defeat] = OneShot("Outpost lost", .85f,
                (t, _) => .39f * (Bell(t, 300) + Bell(t - .18f, 205) + Bell(t - .36f, 124)));
        }

        public void SetScene(RunState run, bool inGame, bool muted, float dt)
        {
            oneShots.mute = muted;
            wind.mute = generator.mute = muted;
            float ambientLevel = inGame ? Mathf.Lerp(.18f, .29f, run.LightBlend) : 0;
            float motorLevel = inGame && run.GeneratorOnline ? .21f * Attenuation(run.Generator.Position, run.Player) : 0;
            wind.volume = Mathf.MoveTowards(wind.volume, ambientLevel, dt * .65f);
            generator.volume = Mathf.MoveTowards(generator.volume, motorLevel, dt * .85f);
        }

        public void Reset()
        {
            oneShots.Stop();
            wind.volume = generator.volume = 0;
            Array.Clear(nextCue, 0, nextCue.Length);
        }

        public void Play(Effect effect, Vector2 player, float time)
        {
            float near = Attenuation(effect.Point, player);
            switch (effect.Kind)
            {
                case EffectKind.Harvested:
                    Play(effect.Material == ResourceKind.Wood ? Cue.Wood : Cue.Ore, Channel.Harvest, .38f, .085f, time);
                    break;
                case EffectKind.Impact:
                case EffectKind.Damage:
                    if (effect.Kind == EffectKind.Damage && (effect.Point - player).sqrMagnitude < .001f)
                        Play(Cue.PlayerHit, Channel.Player, .40f, .16f, time);
                    else Play(Cue.Impact, Channel.Impact, near * .25f, .10f, time);
                    break;
                case EffectKind.Explosion:
                    Play(Cue.Explosion, Channel.Blast, near * .40f, .18f, time);
                    break;
                case EffectKind.Placed:
                    Play(Cue.Place, Channel.Build, .41f, .08f, time);
                    break;
                case EffectKind.Upgraded:
                    Play(Cue.Upgrade, Channel.Build, .40f, .08f, time);
                    break;
                case EffectKind.Refueled:
                    Play(Cue.Fuel, Channel.Machine, near * .48f, .14f, time);
                    break;
                case EffectKind.ProductionReady:
                    Play(Cue.Ready, Channel.Machine, near * .29f, .40f, time);
                    break;
                case EffectKind.PowerLost:
                    Play(Cue.PowerDown, Channel.Signal, .55f, 0, time);
                    break;
                case EffectKind.PowerRestored:
                    Play(Cue.PowerUp, Channel.Signal, .48f, 0, time);
                    break;
                case EffectKind.WaveStarted:
                    Play(Cue.Wave, Channel.Signal, .50f, 0, time);
                    break;
                case EffectKind.BossStarted:
                    Play(Cue.Boss, Channel.Signal, .59f, 0, time);
                    break;
                case EffectKind.Defeated:
                    Play(Cue.Defeat, Channel.Signal, .53f, 0, time);
                    break;
            }
        }

        private void Play(Cue cue, Channel channel, float volume, float gap, float time)
        {
            int index = (int)channel;
            if (time < nextCue[index] || volume <= .005f) return;
            nextCue[index] = time + gap;
            oneShots.PlayOneShot(clips[(int)cue], volume);
        }

        private static float Attenuation(Vector2 source, Vector2 listener) =>
            1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2, 18, Vector2.Distance(source, listener)));

        private static AudioSource Source(GameObject host, AudioClip loop = null)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            if (loop != null)
            {
                source.clip = loop;
                source.loop = true;
                source.volume = 0;
                source.Play();
            }
            return source;
        }

        private static AudioClip OneShot(string name, float duration, Func<float, float, float> voice)
        {
            var random = new System.Random(16);
            return Clip(name, duration, t =>
            {
                float envelope = Mathf.Clamp01(t * 240) * Mathf.Clamp01((duration - t) * 40);
                return envelope * voice(t, (float)random.NextDouble() * 2 - 1);
            });
        }

        private static AudioClip Loop(string name, float duration, Func<float, float> voice) =>
            Clip(name, duration, voice);

        private static AudioClip Clip(string name, float duration, Func<float, float> voice)
        {
            var samples = new float[Mathf.CeilToInt(duration * SampleRate)];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = Mathf.Clamp(voice(i / (float)SampleRate), -1, 1);
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Tone(float t, float frequency, float sweep = 0) =>
            Mathf.Sin(2 * Mathf.PI * t * (frequency + sweep * t * .5f));

        private static float Decay(float t, float rate) => Mathf.Exp(-t * rate);

        private static float Bell(float t, float pitch) =>
            t < 0 ? 0 : Mathf.Clamp01(t * 160) * Decay(t, 13) * (Tone(t, pitch) + .19f * Tone(t, pitch * 2.13f));

        private static float Alarm(float t, float pitch)
        {
            if (t < 0 || t > .29f) return 0;
            float envelope = Mathf.Clamp01(t * 18) * Mathf.Clamp01((.29f - t) * 10);
            return envelope * (.29f * Tone(t, pitch, -85) + .11f * Tone(t, pitch * 2.01f, -170));
        }

        private static float Wind(float t)
        {
            // Frequencies are multiples of 1/6 Hz so the six-second loop joins continuously.
            float low = Tone(t, 172f / 6) * Tone(t, 247f / 6) + .42f * Tone(t, 415f / 6) * Tone(t, 219f / 6);
            float air = Tone(t, 886f / 6) * Tone(t, 1123f / 6) + .35f * Tone(t, 1379f / 6) * Tone(t, 1421f / 6);
            return (.15f * low + .08f * air) * (.66f + .23f * Tone(t, 1f / 6) + .11f * Tone(t, .5f));
        }

        private static float Motor(float t) =>
            (.14f * Tone(t, 61) + .10f * Tone(t, 122) + .045f * Tone(t, 183)) * (.79f + .21f * Tone(t, 2));

        public void Dispose()
        {
            oneShots.Stop();
            wind.Stop();
            generator.Stop();
            Object.Destroy(wind.clip);
            Object.Destroy(generator.clip);
            foreach (var clip in clips) Object.Destroy(clip);
        }
    }
}
