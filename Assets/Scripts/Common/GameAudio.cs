using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// File-free game audio: a looping beach ambience, a continuous soft sand-shuffle that
    /// swells with player movement, and one-shot SFX for ball contacts, net hits, the serve
    /// whistle, points/match win, and the ball landing in/out. Every clip is <b>synthesised in
    /// code</b> at startup (the same spirit as the procedurally-generated sprite art), so the
    /// project ships no audio assets. Self-bootstraps after scene load like <see cref="GameInput"/>;
    /// gameplay code triggers sounds through the static helpers.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        const int SR = 44100;          // sample rate
        static GameConfig Cfg => GameConfig.Instance; // global volume levels live here

        AudioSource _ambient;
        AudioSource _sand;             // continuous movement shuffle (volume tracks motion)
        AudioSource[] _pool;
        int _poolNext;

        AudioClip _ambientClip, _sandClip, _netClip, _crowdClip;
        AudioClip _whistleClip, _thudClip, _outClip, _pointUpClip, _pointDownClip, _winClip;
        readonly Dictionary<HitType, AudioClip> _hitClips = new Dictionary<HitType, AudioClip>();

        // movement tracking, to drive the sand shuffle's volume
        List<VolleyPlayer> _players;
        readonly Dictionary<VolleyPlayer, Vector3> _lastPos = new Dictionary<VolleyPlayer, Vector3>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("GameAudio").AddComponent<GameAudio>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildClips();

            _ambient = AddLoop(_ambientClip, Cfg.ambientVolume * Cfg.masterVolume);
            _sand = AddLoop(_sandClip, 0f); // starts silent, rises while players move

            _pool = new AudioSource[12];
            for (int i = 0; i < _pool.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.spatialBlend = 0f;
                s.playOnAwake = false;
                _pool[i] = s;
            }
        }

        AudioSource AddLoop(AudioClip clip, float vol)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = vol;
            s.playOnAwake = false;
            s.Play();
            return s;
        }

        // ----------------------------------------------------------------- public API

        /// <summary>A ball contact (swing / set / bump / serve / block), coloured by hit type.</summary>
        public static void PlayHit(HitType type, Vector3 pos)
        {
            var inst = Instance;
            if (inst == null) return;
            inst._hitClips.TryGetValue(type, out AudioClip clip);
            inst.OneShot(clip, HitVolume(type), Random.Range(0.95f, 1.07f), pos);
        }

        /// <summary>The ball brushing/hitting the net.</summary>
        public static void PlayNet(Vector3 pos)
            => Instance?.OneShot(Instance._netClip, 0.5f, Random.Range(0.94f, 1.06f), pos);

        /// <summary>Referee whistle authorising the serve.</summary>
        public static void PlayWhistle()
            => Instance?.OneShot(Instance._whistleClip, 0.5f, Random.Range(0.99f, 1.02f), Vector3.zero);

        /// <summary>The ball landing: a sandy thud, plus an "out" blip when it lands out of bounds.</summary>
        public static void PlayLanding(bool inBounds, Vector3 pos)
        {
            var inst = Instance;
            if (inst == null) return;
            inst.OneShot(inst._thudClip, 0.6f, Random.Range(0.95f, 1.06f), pos);
            if (!inBounds) inst.OneShot(inst._outClip, 0.5f, 1f, pos);
        }

        /// <summary>A point is scored: a chime plus a crowd cheer (bigger when it went your way).</summary>
        public static void PlayPoint(bool playerScored)
        {
            var inst = Instance;
            if (inst == null) return;
            inst.OneShot(playerScored ? inst._pointUpClip : inst._pointDownClip, 0.5f, 1f, Vector3.zero);
            PlayCrowd(playerScored ? 0.85f : 0.5f);
        }

        /// <summary>End-of-match fanfare with a full-throated crowd cheer.</summary>
        public static void PlayMatchWin()
        {
            Instance?.OneShot(Instance._winClip, 0.55f, 1f, Vector3.zero);
            PlayCrowd(1f);
        }

        /// <summary>A swell of applause/cheering; intensity 0–1. Uses its own crowd volume.</summary>
        public static void PlayCrowd(float intensity)
        {
            var inst = Instance;
            if (inst == null) return;
            inst.OneShotAt(inst._crowdClip, intensity * Cfg.crowdVolume * Cfg.masterVolume,
                           Random.Range(0.97f, 1.04f), Vector3.zero);
        }

        static float HitVolume(HitType type)
        {
            switch (type)
            {
                case HitType.Spike: return 0.95f;
                case HitType.Block: return 0.9f;
                case HitType.Serve: return 0.8f;
                case HitType.Set:   return 0.45f;
                default:            return 0.6f; // Bump
            }
        }

        void OneShot(AudioClip clip, float vol, float pitch, Vector3 pos)
            => OneShotAt(clip, vol * Cfg.sfxVolume * Cfg.masterVolume, pitch, pos);

        // Plays at an already-final volume (lets the crowd use crowdVolume instead of sfxVolume).
        void OneShotAt(AudioClip clip, float finalVol, float pitch, Vector3 pos)
        {
            if (clip == null) return;
            var s = _pool[_poolNext];
            _poolNext = (_poolNext + 1) % _pool.Length;
            s.pitch = pitch;
            s.panStereo = Mathf.Clamp(pos.x / (CourtGeometry.HalfWidth + 1f), -1f, 1f) * 0.6f;
            s.PlayOneShot(clip, finalVol);
        }

        // ----------------------------------------------------------------- movement shuffle

        void Update()
        {
            // keep the looping beds in sync with live volume changes from the config asset
            if (_ambient != null) _ambient.volume = Cfg.ambientVolume * Cfg.masterVolume;

            if (_sand == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (_players == null) _players = new List<VolleyPlayer>();
            if (_players.Count == 0)
                _players.AddRange(FindObjectsByType<VolleyPlayer>(FindObjectsSortMode.None));

            // the fastest-moving player drives the shuffle (so a busy court isn't deafening)
            float maxSpeed = 0f;
            foreach (var p in _players)
            {
                if (p == null) continue;
                Vector3 cur = p.GroundPosition;
                if (_lastPos.TryGetValue(p, out Vector3 prev))
                    maxSpeed = Mathf.Max(maxSpeed, (cur - prev).magnitude / dt);
                _lastPos[p] = cur;
            }

            float target = Mathf.Clamp01(maxSpeed / 3.5f);
            float k = 1f - Mathf.Exp(-dt * 9f); // smooth fade in/out
            _sand.volume = Mathf.Lerp(_sand.volume, target * Cfg.movementVolume * Cfg.masterVolume, k);
            _sand.pitch = 0.92f + 0.2f * target; // shifts faster when moving quicker
        }

        // ----------------------------------------------------------------- synthesis

        void BuildClips()
        {
            // base freq, amp, noise, duration, decay — tuned per contact feel
            _hitClips[HitType.Spike] = MakeHit("hit_spike", 240f, 0.9f, 0.7f, 0.14f, 30f);
            _hitClips[HitType.Block] = MakeHit("hit_block", 210f, 0.85f, 0.9f, 0.16f, 26f);
            _hitClips[HitType.Serve] = MakeHit("hit_serve", 200f, 0.8f, 0.5f, 0.14f, 30f);
            _hitClips[HitType.Bump]  = MakeHit("hit_bump", 150f, 0.7f, 0.8f, 0.18f, 22f);
            _hitClips[HitType.Set]   = MakeHit("hit_set", 320f, 0.5f, 0.3f, 0.10f, 45f);

            _netClip = MakeNet();
            _whistleClip = MakeWhistle();
            _thudClip = MakeThud();
            _outClip = MakeOutBlip();

            _pointUpClip = MakeChime("point_up", new[] { N(660f, 0f, 0.4f), N(880f, 0.12f, 0.5f) }, 0.62f);
            _pointDownClip = MakeChime("point_down", new[] { N(440f, 0f, 0.4f), N(330f, 0.12f, 0.5f) }, 0.62f);
            _winClip = MakeChime("match_win",
                new[] { N(523f, 0f, 0.25f), N(659f, 0.16f, 0.25f), N(784f, 0.32f, 0.4f), N(1046f, 0.5f, 0.6f) }, 1.15f);

            _crowdClip = MakeCrowd();
            _sandClip = MakeSandLoop();
            _ambientClip = MakeAmbient();
        }

        // A swell of applause: a low crowd "roar" bed sprinkled with hundreds of short claps.
        AudioClip MakeCrowd()
        {
            float dur = 1.8f;
            int n = (int)(SR * dur);
            var d = new float[n];

            // roar bed — heavily low-passed noise rumble
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = Random.value * 2f - 1f;
                lp = lp * 0.93f + w * 0.07f;
                d[i] = lp * 0.5f;
            }

            // claps — many short bright noise bursts at random times
            const int claps = 600;
            for (int c = 0; c < claps; c++)
            {
                int start = Random.Range(0, n - 200);
                int len = Random.Range(60, 160);
                float amp = Random.Range(0.2f, 1f);
                for (int j = 0; j < len; j++)
                {
                    int idx = start + j;
                    if (idx >= n) break;
                    float env = Mathf.Exp(-j * 0.05f);
                    d[idx] += (Random.value * 2f - 1f) * amp * env * 0.15f;
                }
            }

            // overall swell: quick rise, plateau, gentle fall
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float atk = Mathf.Clamp01(t / 0.12f);
                float dec = t < dur * 0.55f ? 1f : Mathf.Clamp01((dur - t) / (dur * 0.45f));
                d[i] *= atk * dec;
            }

            Normalize(d, 0.8f);
            return MakeClip("crowd", d);
        }

        AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            if (m < 1e-4f) return;
            float g = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= g;
        }

        // A short percussive "thwock": a pitch-dropping body thump + a fast noise transient.
        AudioClip MakeHit(string name, float baseFreq, float amp, float noiseAmt, float dur, float decayK)
        {
            int n = (int)(SR * dur);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Exp(-t * decayK);
                float f = baseFreq * (1f + 1.5f * Mathf.Exp(-t * 50f));
                phase += 2f * Mathf.PI * f / SR;
                float body = Mathf.Sin(phase) * env;
                float noise = (Random.value * 2f - 1f) * noiseAmt * Mathf.Exp(-t * decayK * 3f);
                d[i] = (body + noise) * amp;
            }
            Normalize(d, 0.9f);
            return MakeClip(name, d);
        }

        AudioClip MakeNet()
        {
            float dur = 0.2f;
            int n = (int)(SR * dur);
            var d = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Clamp01(t / 0.006f) * Mathf.Exp(-t * 16f);
                float w = Random.value * 2f - 1f;
                lp = lp * 0.6f + w * 0.4f;
                d[i] = lp * env;
            }
            Normalize(d, 0.7f);
            return MakeClip("net", d);
        }

        // Shrill referee whistle: two close tones beating, with a fast "pea" tremolo.
        AudioClip MakeWhistle()
        {
            float dur = 0.34f;
            int n = (int)(SR * dur);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float attack = Mathf.Clamp01(t / 0.012f);
                float release = Mathf.Clamp01((dur - t) / 0.06f);
                float env = attack * release;
                float trem = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 28f * t); // pea rattle
                float tone = Mathf.Sin(2f * Mathf.PI * 2750f * t) + Mathf.Sin(2f * Mathf.PI * 2900f * t);
                float breath = (Random.value * 2f - 1f) * 0.05f;
                d[i] = (tone * 0.5f * trem + breath) * env;
            }
            Normalize(d, 0.7f);
            return MakeClip("whistle", d);
        }

        // Muffled sand impact when the ball lands.
        AudioClip MakeThud()
        {
            float dur = 0.14f;
            int n = (int)(SR * dur);
            var d = new float[n];
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float env = Mathf.Exp(-t * 28f);
                float f = 110f * (1f + 2f * Mathf.Exp(-t * 60f));
                phase += 2f * Mathf.PI * f / SR;
                float w = Random.value * 2f - 1f;
                lp = lp * 0.8f + w * 0.2f;
                d[i] = Mathf.Sin(phase) * env + lp * 0.5f * Mathf.Exp(-t * 55f);
            }
            Normalize(d, 0.85f);
            return MakeClip("thud", d);
        }

        // A gentle descending "bwoop" that marks the ball landing out of bounds.
        AudioClip MakeOutBlip()
        {
            float dur = 0.24f;
            int n = (int)(SR * dur);
            var d = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float frac = t / dur;
                float f = Mathf.Lerp(520f, 300f, frac); // slide down
                phase += 2f * Mathf.PI * f / SR;
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 7f);
                d[i] = (Mathf.Sin(phase) * 0.8f + Mathf.Sin(phase * 2f) * 0.2f) * env;
            }
            Normalize(d, 0.7f);
            return MakeClip("out", d);
        }

        // (freq, startTime, duration) note descriptor for chimes.
        static (float f, float start, float dur) N(float f, float start, float dur) => (f, start, dur);

        // A short bell-like sequence of notes.
        AudioClip MakeChime(string name, (float f, float start, float dur)[] notes, float totalDur)
        {
            int n = (int)(SR * totalDur);
            var d = new float[n];
            foreach (var note in notes)
            {
                int s0 = (int)(note.start * SR);
                int s1 = Mathf.Min(n, (int)((note.start + note.dur) * SR));
                for (int i = s0; i < s1; i++)
                {
                    float lt = (float)(i - s0) / SR;
                    float env = Mathf.Clamp01(lt / 0.005f) * Mathf.Exp(-lt * 8f);
                    float tone = Mathf.Sin(2f * Mathf.PI * note.f * lt) * 0.6f
                               + Mathf.Sin(2f * Mathf.PI * note.f * 2f * lt) * 0.25f; // octave shimmer
                    d[i] += tone * env;
                }
            }
            Normalize(d, 0.8f);
            return MakeClip(name, d);
        }

        // A seamless, steady, soft grain — sand shifting underfoot. Volume is driven at runtime.
        AudioClip MakeSandLoop()
        {
            float dur = 2f, cross = 0.4f;
            int n = (int)(SR * dur);
            int m = (int)(SR * cross);
            var s = new float[n + m];
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n + m; i++)
            {
                float t = (float)i / SR;
                float w = Random.value * 2f - 1f;
                lp = lp * 0.7f + w * 0.3f;       // two-pole low-pass -> soft, dull grain
                lp2 = lp2 * 0.7f + lp * 0.3f;
                float flutter = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 3.3f * t); // subtle life
                s[i] = lp2 * flutter;
            }
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = s[i];
            for (int i = 0; i < m; i++)
            {
                float wgt = (float)i / m;
                d[i] = s[i] * wgt + s[n + i] * (1f - wgt);
            }
            Normalize(d, 0.5f);
            return MakeClip("sand_move", d);
        }

        // A seamless beach loop: leaky-integrated (brown) noise washed by slow wave swells.
        AudioClip MakeAmbient()
        {
            float dur = 5f, cross = 0.6f;
            int n = (int)(SR * dur);
            int m = (int)(SR * cross);
            var s = new float[n + m];
            float brown = 0f, lp = 0f;
            for (int i = 0; i < n + m; i++)
            {
                float t = (float)i / SR;
                float w = Random.value * 2f - 1f;
                brown = brown * 0.985f + w * 0.12f;
                lp = lp * 0.85f + w * 0.15f;
                float wave = 0.4f + 0.35f * Mathf.Sin(2f * Mathf.PI * 0.07f * t)
                                  + 0.18f * Mathf.Sin(2f * Mathf.PI * 0.11f * t + 1.3f);
                if (wave < 0f) wave = 0f;
                s[i] = (brown * 1.3f + lp * 0.22f) * wave;
            }

            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = s[i];
            for (int i = 0; i < m; i++)
            {
                float wgt = (float)i / m;
                d[i] = s[i] * wgt + s[n + i] * (1f - wgt);
            }
            Normalize(d, 0.6f);
            return MakeClip("ambient_beach", d);
        }
    }
}
