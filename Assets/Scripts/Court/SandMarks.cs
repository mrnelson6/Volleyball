using System.Collections.Generic;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Transient impressions in the sand — footprints, dive drag streaks and ball impact
    /// craters — that fade away over time. Purely cosmetic. Bootstraps itself on first use
    /// (no scene setup needed): marks are pooled flat sprites lying on the ground, drawn
    /// beneath the drop shadows, and the oldest are recycled past a cap.
    /// </summary>
    public class SandMarks : MonoBehaviour
    {
        const int MaxMarks = 160;      // beyond this the oldest mark is recycled early
        const float GroundY = 0.015f;  // just above the sand, below the drop shadows (0.03)

        static SandMarks _instance;
        static Sprite _blob;

        struct Mark
        {
            public SpriteRenderer sr;
            public float age, life, fadeTime, baseAlpha;
        }

        readonly List<Mark> _active = new List<Mark>();
        readonly Stack<SpriteRenderer> _free = new Stack<SpriteRenderer>();
        int _order; // cycled so newer marks draw on top of older ones

        // ------------------------------------------------------------------ public API

        /// <summary>A single footprint: a small oval along the walk direction, nudged to
        /// the side (+1/-1 alternating) so left and right steps land apart.</summary>
        public static void Footstep(Vector3 pos, Vector3 dir, float side)
        {
            Vector3 perp = new Vector3(dir.z, 0f, -dir.x);
            Spawn(pos + perp * (0.11f * side), YawAlong(dir) + Random.Range(-8f, 8f),
                  new Vector2(0.14f, 0.26f), alpha: 0.10f, life: 4f, fadeTime: 2.5f);
        }

        /// <summary>A chunk of the drag trough a dive slide gouges out, stretched along it.</summary>
        public static void DiveStreak(Vector3 pos, Vector3 dir)
        {
            Vector3 perp = new Vector3(dir.z, 0f, -dir.x);
            Spawn(pos + perp * Random.Range(-0.06f, 0.06f), YawAlong(dir) + Random.Range(-6f, 6f),
                  new Vector2(Random.Range(0.28f, 0.4f), Random.Range(0.6f, 0.85f)),
                  alpha: 0.14f, life: 6f, fadeTime: 3f);
        }

        /// <summary>The crater where the ball slams into the sand — bigger and darker than
        /// the body marks (it should read at a glance), scaled by how hard it came in.</summary>
        public static void BallImpact(Vector3 pos, float impactSpeed)
        {
            if (impactSpeed < 1.5f) return; // a dribbling roll doesn't dent the sand
            float t = Mathf.InverseLerp(3f, 20f, impactSpeed);
            float s = Mathf.Lerp(0.45f, 0.9f, t);
            Spawn(new Vector3(pos.x, 0f, pos.z), Random.Range(0f, 360f),
                  new Vector2(s, s), alpha: Mathf.Lerp(0.28f, 0.42f, t), life: 10f, fadeTime: 4f);
        }

        // ------------------------------------------------------------------ internals

        static float YawAlong(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        static void Spawn(Vector3 pos, float yawDeg, Vector2 size, float alpha, float life, float fadeTime)
        {
            if (_instance == null)
                _instance = new GameObject("Sand Marks").AddComponent<SandMarks>();
            _instance.SpawnMark(pos, yawDeg, size, alpha, life, fadeTime);
        }

        void SpawnMark(Vector3 pos, float yawDeg, Vector2 size, float alpha, float life, float fadeTime)
        {
            if (_active.Count >= MaxMarks) Recycle(0);

            SpriteRenderer sr = _free.Count > 0 ? _free.Pop() : NewRenderer();
            sr.gameObject.SetActive(true);
            sr.transform.position = new Vector3(pos.x, GroundY, pos.z);
            sr.transform.rotation = Quaternion.Euler(90f, yawDeg, 0f);
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            sr.color = new Color(0f, 0f, 0f, alpha);
            _order = (_order + 1) % 90;
            sr.sortingOrder = -100 + _order; // below the drop shadows; newer above older

            _active.Add(new Mark { sr = sr, age = 0f, life = life, fadeTime = fadeTime, baseAlpha = alpha });
        }

        SpriteRenderer NewRenderer()
        {
            var go = new GameObject("Mark");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Blob();
            return sr;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Mark m = _active[i];
                m.age += dt;
                if (m.age >= m.life) { Recycle(i); continue; }

                Color c = m.sr.color;
                c.a = m.baseAlpha * Mathf.Clamp01((m.life - m.age) / m.fadeTime);
                m.sr.color = c;
                _active[i] = m;
            }
        }

        void Recycle(int index)
        {
            SpriteRenderer sr = _active[index].sr;
            _active.RemoveAt(index);
            sr.gameObject.SetActive(false);
            _free.Push(sr);
        }

        /// <summary>Soft radial blob, generated once — white so the renderer tint decides
        /// the colour, with a smooth falloff so marks read as dents rather than decals.</summary>
        static Sprite Blob()
        {
            if (_blob != null) return _blob;

            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S * 2f - 1f;
                    float dy = (y + 0.5f) / S * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a); // smoothstep: dense centre, feathered edge
                    px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();

            _blob = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S);
            _blob.name = "SandMarkBlob";
            return _blob;
        }
    }
}
