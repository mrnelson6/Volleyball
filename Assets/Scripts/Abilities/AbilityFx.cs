using UnityEngine;
using UnityEngine.Rendering;

namespace Volleyball
{
    /// <summary>
    /// Tiny runtime effects kit for abilities, built from primitives with the always-included
    /// Sprites/Default shader (unlit, alpha, double-sided) — no assets, so abilities need no
    /// scene rebuild. Pure view: nothing here has a collider or touches gameplay, and on a
    /// headless server (<see cref="CanRender"/> false) callers skip it entirely.
    /// </summary>
    public static class AbilityFx
    {
        public static bool CanRender => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        static Shader _shader;

        public static Material Mat(Color c)
        {
            if (_shader == null) _shader = Shader.Find("Sprites/Default");
            var m = new Material(_shader) { color = c };
            return m;
        }

        static GameObject Prim(PrimitiveType type, string name, Color c)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
            else Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat(c);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<FxOwnedMaterial>();
            return go;
        }

        public static GameObject Quad(string name, Color c) => Prim(PrimitiveType.Quad, name, c);
        public static GameObject Box(string name, Color c) => Prim(PrimitiveType.Cube, name, c);
        public static GameObject Ball(string name, Color c) => Prim(PrimitiveType.Sphere, name, c);

        /// <summary>A flat disc lying on the sand (a squashed cylinder).</summary>
        public static GameObject Disc(string name, Vector3 center, float radius, Color c, float y = 0.03f)
        {
            GameObject go = Prim(PrimitiveType.Cylinder, name, c);
            go.transform.position = new Vector3(center.x, y, center.z);
            go.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            return go;
        }

        /// <summary>An expanding, fading ring on the ground (shockwaves, roars, pops).</summary>
        public static void Ring(Vector3 center, Color c, float toRadius, float seconds, float width = 0.18f)
        {
            if (!CanRender) return;
            var go = new GameObject("FxRing");
            go.transform.position = new Vector3(center.x, center.y + 0.06f, center.z);
            go.AddComponent<FxRing>().Init(c, toRadius, seconds, width);
        }

        /// <summary>A burst of little blobs flying out and shrinking (dust, splashes, pops).</summary>
        public static void Puff(Vector3 at, Color c, int count = 10, float speed = 3f, float size = 0.22f,
                                float seconds = 0.6f)
        {
            if (!CanRender) return;
            for (int i = 0; i < count; i++)
            {
                GameObject b = Ball("FxPuff", c);
                b.transform.position = at;
                Vector3 v = Random.onUnitSphere * speed;
                v.y = Mathf.Abs(v.y) * 0.8f + speed * 0.3f;
                b.AddComponent<FxBlob>().Init(v, size * Random.Range(0.7f, 1.3f), seconds);
            }
        }

        public static void Kill(GameObject go)
        {
            if (go != null) Object.Destroy(go);
        }
    }

    /// <summary>Destroys the per-object material made by <see cref="AbilityFx"/> with its object.</summary>
    public class FxOwnedMaterial : MonoBehaviour
    {
        void OnDestroy()
        {
            var r = GetComponent<Renderer>();
            if (r != null && r.sharedMaterial != null) Destroy(r.sharedMaterial);
        }
    }

    /// <summary>A ground ring that grows and fades, then removes itself.</summary>
    public class FxRing : MonoBehaviour
    {
        LineRenderer _line;
        Color _color;
        float _to, _dur, _t;
        const int Segments = 48;

        public void Init(Color c, float toRadius, float seconds, float width)
        {
            _color = c;
            _to = toRadius;
            _dur = Mathf.Max(seconds, 0.05f);
            _line = gameObject.AddComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = true;
            _line.positionCount = Segments;
            _line.widthMultiplier = width;
            _line.material = AbilityFx.Mat(Color.white);
            _line.shadowCastingMode = ShadowCastingMode.Off;
            Tick();
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _dur) { Destroy(gameObject); return; }
            Tick();
        }

        void Tick()
        {
            float u = _t / _dur;
            float r = Mathf.Lerp(0.2f, _to, 1f - (1f - u) * (1f - u)); // ease out
            Vector3 c = transform.position;
            for (int i = 0; i < Segments; i++)
            {
                float a = i / (float)Segments * Mathf.PI * 2f;
                _line.SetPosition(i, c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            Color col = _color;
            col.a *= 1f - u;
            _line.startColor = _line.endColor = col;
        }

        void OnDestroy()
        {
            if (_line != null && _line.material != null) Destroy(_line.material);
        }
    }

    /// <summary>One flying, shrinking puff blob.</summary>
    public class FxBlob : MonoBehaviour
    {
        Vector3 _v;
        float _size, _dur, _t;

        public void Init(Vector3 velocity, float size, float seconds)
        {
            _v = velocity;
            _size = size;
            _dur = Mathf.Max(seconds, 0.05f);
            transform.localScale = Vector3.one * size;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _dur) { Destroy(gameObject); return; }
            _v *= Mathf.Exp(-3f * Time.deltaTime);
            _v.y -= 4f * Time.deltaTime;
            transform.position += _v * Time.deltaTime;
            if (transform.position.y < 0.05f) transform.position = new Vector3(transform.position.x, 0.05f, transform.position.z);
            transform.localScale = Vector3.one * (_size * (1f - _t / _dur));
        }
    }
}
