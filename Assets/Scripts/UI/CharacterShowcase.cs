using UnityEngine;
using UnityEngine.UI;

namespace Volleyball
{
    /// <summary>
    /// Live 3D preview for the character-select screens: the chosen animal's generated model
    /// stands on a little sand pedestal (a stage built by MainMenuSceneBuilder far below the
    /// menu's beach), idling and gently turning, and cheers when picked. A dedicated camera
    /// renders the stage into a RenderTexture shown by a RawImage; it only runs while the panel
    /// is open. Falls back to hiding the image when an animal has no 3D model.
    /// </summary>
    public class CharacterShowcase : MonoBehaviour
    {
        public RawImage target;
        public Camera stageCamera;
        public Transform stagePoint;     // where the animal stands (pedestal top)
        public int textureSize = 1024;
        [Tooltip("Degrees the model sways either side of facing the camera.")]
        public float swayDegrees = 30f;

        RenderTexture _rt;
        GameObject _model;
        ModelCharacterView _view;
        string _shownId;
        float _swayPhase;
        float _cheerLeft;

        void OnEnable()
        {
            if (stageCamera == null) return;
            if (_rt == null)
            {
                _rt = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                    name = "CharacterShowcase",
                };
            }
            stageCamera.targetTexture = _rt;
            stageCamera.enabled = true;
            if (target != null) target.texture = _rt;
        }

        void OnDisable()
        {
            if (stageCamera != null) stageCamera.enabled = false;
        }

        void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }

        /// <summary>Show <paramref name="characterId"/> in <paramref name="jersey"/>; a new pick cheers.</summary>
        public void Show(string characterId, Color jersey)
        {
            if (characterId == _shownId && _model != null) return;
            _shownId = characterId;
            if (_model != null) Destroy(_model);
            _model = null;
            _view = null;

            var prefab = CharacterModels.LoadPrefab(characterId);
            if (target != null) target.enabled = prefab != null;
            if (prefab == null || stagePoint == null) return;

            _model = Instantiate(prefab, stagePoint.position, Quaternion.identity);
            _model.name = "Showcase " + characterId;
            _model.GetComponent<AnimalLook>()?.Set(characterId, jersey);
            _view = _model.GetComponent<ModelCharacterView>();
            if (_view != null) _view.PlayShowcase(_view.cheer);
            _cheerLeft = 1.8f; // the cheer loops; settle into idle after a couple of pumps
            _swayPhase = 0f;
            Frame(CharacterRoster.Get(characterId));
        }

        /// <summary>Pull the camera back for tall animals so the whole body (ears, horns) fits.</summary>
        void Frame(CharacterDef ch)
        {
            if (stageCamera == null || ch == null) return;
            float h = 1.8f * ch.height + 0.35f; // body plus ears/horns
            Vector3 look = stagePoint.position + Vector3.up * (h * 0.48f);
            float dist = h * 0.5f / Mathf.Tan(stageCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;
            // a little above and to the side, so the toon lighting models the face
            Vector3 dir = new Vector3(0.28f, 0.22f, 1f).normalized;
            stageCamera.transform.position = look + dir * dist;
            stageCamera.transform.LookAt(look);
        }

        void Update()
        {
            if (_model == null || stageCamera == null) return;
            if (_cheerLeft > 0f && (_cheerLeft -= Time.unscaledDeltaTime) <= 0f && _view != null)
                _view.PlayShowcase(_view.idle);
            _swayPhase += Time.unscaledDeltaTime * 0.6f;
            Vector3 toCam = stageCamera.transform.position - _model.transform.position;
            toCam.y = 0f;
            float yaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg + Mathf.Sin(_swayPhase) * swayDegrees;
            _model.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
