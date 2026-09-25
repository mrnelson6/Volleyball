using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// A 3D animal hanging around on the menu beach: spawns the character's generated model in
    /// showcase mode (no VolleyPlayer) and loops a clip, re-cheering now and then.
    /// </summary>
    public class MenuAnimal : MonoBehaviour
    {
        public string characterId = "fox";
        public Color jersey = new Color(0.20f, 0.50f, 0.95f);
        public bool cheers = true;
        public float cheerEvery = 5f;

        ModelCharacterView _view;
        float _next;

        void Start()
        {
            var prefab = CharacterModels.LoadPrefab(characterId);
            if (prefab == null) return;
            var go = Instantiate(prefab, transform, false);
            go.GetComponent<AnimalLook>()?.Set(characterId, jersey);
            _view = go.GetComponent<ModelCharacterView>();
            _next = Time.unscaledTime + Random.Range(0.5f, cheerEvery);
        }

        void Update()
        {
            if (_view == null || !cheers || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + cheerEvery + Random.Range(-1f, 2f);
            _view.PlayShowcase(_view.cheer);
            Invoke(nameof(BackToIdle), 1.6f);
        }

        void BackToIdle()
        {
            if (_view != null) _view.PlayShowcase(_view.idle);
        }
    }
}
