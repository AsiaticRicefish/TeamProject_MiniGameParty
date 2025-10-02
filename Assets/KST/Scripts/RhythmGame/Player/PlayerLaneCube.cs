using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace RhythmGame
{
    [RequireComponent(typeof(PhotonView))]
    [RequireComponent(typeof(Renderer))]
    public class PlayerLaneCube : MonoBehaviour
    {
        [SerializeField] Renderer _renderer;
        [SerializeField, Range(0f, 1f)] float alphaValue = 0.8f;
        JengaRankingUIAnimated provider;

        string _uid;
        MaterialPropertyBlock _mpb;
        PhotonView _pv;

        readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            _pv = GetComponent<PhotonView>();

            if (!provider) provider = FindObjectOfType<JengaRankingUIAnimated>();
            if (_renderer == null)
                _renderer = GetComponent<Renderer>();

            _mpb = new MaterialPropertyBlock();

            _uid = _pv && _pv.Owner != null
                ? _pv.Owner.CustomProperties?["uid"] as string
                : null;

            if (provider)
                provider.OnPaletteChanged += HandlePaletteChanged;
        }

        void Start()
        {
            ApplyNow();
        }

        void OnDestroy()
        {
            if (provider)
                provider.OnPaletteChanged -= HandlePaletteChanged;
        }

        void HandlePaletteChanged() => ApplyNow();

        public void ApplyNow()
        {
            if (string.IsNullOrEmpty(_uid) || provider == null || _renderer == null) return;

            if (!provider.TryGetColor(_uid, out var c))
                return;

            c.a = Mathf.Clamp01(alphaValue);
            ApplyRenderer(_renderer, c);
        }

        void ApplyRenderer(Renderer r, Color color)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(ID_BaseColor, color);
            r.SetPropertyBlock(_mpb);
        }
    }
}
