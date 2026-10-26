using System.Collections.Generic;
using Coffee.UIExtensions;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Owns the trail and landing celebration for a booster introduction.</summary>
public sealed class BoosterUnlockVfx : MonoBehaviour
{
    [SerializeField] private GameObject _trailPrefab;
    [SerializeField] private GameObject _arrivalPrefab;
    [SerializeField] private Texture _glowTexture;
    [SerializeField] private Sprite _ringSprite;
    [SerializeField] private Sprite _sparkleSprite;
    [SerializeField] private Material _additiveMaterial;
    private RectTransform _flightTarget;
    private UIParticle _trail;
    private UIParticle _burst;
    private RawImage _flightGlow;
    private Sequence _arrival;
    private readonly List<Material> _materials = new List<Material>();

    /// <summary>Starts an owned star trail that follows the flying booster artwork.</summary>
    public void BeginFlight(RectTransform icon)
    {
        if (_trailPrefab == null || _arrivalPrefab == null || _glowTexture == null ||
            _ringSprite == null || _sparkleSprite == null || _additiveMaterial == null)
        {
            Debug.LogError("Booster unlock VFX needs StarTrail, UnlockSparks, glow texture, ring and sparkle sprites and UI additive material on BoosterPresentation.prefab.", this);
            return;
        }
        _flightTarget = icon;
        _flightGlow = CreateGlow("Flight glow", new Color(1f, .8f, .32f, .24f));
        _flightGlow.rectTransform.sizeDelta = new Vector2(300f, 300f);
        _trail = CreateParticles(_trailPrefab, "Booster star trail", 18f);
        _trail.positionMode = UIParticle.PositionMode.Absolute;
        foreach (ParticleSystem particle in _trail.particles)
        {
            var main = particle.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .28f);
            main.maxParticles = 32;
            var emission = particle.emission;
            emission.rateOverTime = 28f;
            emission.rateOverDistance = 0f;
        }
        FollowFlight();
        _trail.Play();
    }

    private void LateUpdate()
    {
        if (_flightTarget != null) FollowFlight();
    }

    private void FollowFlight()
    {
        if (_flightTarget == null) return;
        if (_trail != null) _trail.transform.position = _flightTarget.position;
        if (_flightGlow == null) return;
        _flightGlow.rectTransform.position = _flightTarget.position;
        _flightGlow.rectTransform.localScale = _flightTarget.localScale;
    }

    /// <summary>Stops the trail and celebrates the handover at the HUD icon.</summary>
    public void PlayArrival(RectTransform target)
    {
        if (_flightTarget == null || target == null) return;
        _flightTarget = null;
        if (_trail != null) _trail.StopEmission();
        Vector3 center = target.TransformPoint(target.rect.center);
        float overlayScale = Mathf.Max(.0001f, Mathf.Abs(transform.lossyScale.x));
        float diameter = Mathf.Max(target.rect.width * Mathf.Abs(target.lossyScale.x),
            target.rect.height * Mathf.Abs(target.lossyScale.y)) / overlayScale;

        RawImage glow = CreateGlow("Landing glow", new Color(1f, .72f, .18f, 0f));
        glow.rectTransform.position = center;
        glow.rectTransform.sizeDelta = Vector2.one * diameter * 2.8f;
        RawImage flash = CreateGlow("Landing flash", new Color(1f, .96f, .78f, .85f));
        flash.rectTransform.position = center;
        flash.rectTransform.sizeDelta = Vector2.one * diameter * 1.1f;
        Image ring = CreateRing(center, diameter);
        _burst = CreateParticles(_arrivalPrefab, "Booster unlock sparks", diameter / 3.2f);
        _burst.transform.position = center;
        foreach (ParticleSystem particle in _burst.particles)
        {
            var main = particle.main;
            main.loop = false;
            main.maxParticles = 32;
            main.startSpeedMultiplier *= 1.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
        }
        _burst.Play();

        _arrival = DOTween.Sequence().SetAutoKill(false).SetLink(gameObject);
        _arrival.Insert(0f, glow.DOFade(.55f, .08f));
        _arrival.Insert(.08f, glow.DOFade(0f, .48f).SetEase(Ease.OutQuad));
        _arrival.Insert(0f, glow.rectTransform.DOScale(1.18f, .56f).SetEase(Ease.OutQuad));
        _arrival.Insert(0f, flash.DOFade(0f, .14f).SetEase(Ease.OutQuad));
        _arrival.Insert(0f, flash.rectTransform.DOScale(1.7f, .14f).SetEase(Ease.OutQuad));
        _arrival.Insert(0f, ring.rectTransform.DOScale(2.5f, .42f).SetEase(Ease.OutCubic));
        _arrival.Insert(.04f, ring.DOFade(0f, .38f).SetEase(Ease.InQuad));
        if (_flightGlow != null) _arrival.Insert(0f, _flightGlow.DOFade(0f, .12f));
        Vector3 localCenter = transform.InverseTransformPoint(center);
        for (int i = 0; i < 8; i++) AddArrivalSpark(localCenter, diameter, i);
    }

    private void AddArrivalSpark(Vector3 center, float diameter, int index)
    {
        float angle = (index * 45f + 15f) * Mathf.Deg2Rad;
        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var node = new GameObject("Landing sparkle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        node.layer = gameObject.layer;
        node.transform.SetParent(transform, false);
        Image sparkle = node.GetComponent<Image>();
        sparkle.sprite = _sparkleSprite;
        sparkle.material = _additiveMaterial;
        sparkle.color = new Color(1f, .88f, .48f, .95f);
        sparkle.raycastTarget = false;
        RectTransform rect = sparkle.rectTransform;
        float size = diameter * .22f;
        if (index % 2 == 0) size *= 1.35f;
        rect.sizeDelta = Vector2.one * size;
        rect.anchoredPosition = (Vector2)center + direction * diameter * .3f;
        rect.localScale = Vector3.one * .15f;
        rect.localRotation = Quaternion.Euler(0f, 0f, index * 19f);
        float delay = .025f + (index % 3) * .025f;
        _arrival.Insert(delay, rect.DOAnchorPos((Vector2)center + direction * diameter * 1.05f, .42f).SetEase(Ease.OutCubic));
        _arrival.Insert(delay, rect.DOScale(1f, .09f).SetEase(Ease.OutQuad));
        _arrival.Insert(delay + .09f, rect.DOScale(.15f, .33f).SetEase(Ease.InQuad));
        _arrival.Insert(delay + .1f, sparkle.DOFade(0f, .32f).SetEase(Ease.InQuad));
    }

    private UIParticle CreateParticles(GameObject prefab, string effectName, float scale)
    {
        var root = new GameObject(effectName, typeof(RectTransform), typeof(CanvasRenderer));
        root.layer = gameObject.layer;
        root.transform.SetParent(transform, false);
        root.transform.SetAsFirstSibling();
        GameObject instance = Instantiate(prefab, root.transform, false);
        instance.transform.localPosition = Vector3.zero;
        ConfigureParticleMaterials(instance);
        UIParticle view = root.AddComponent<UIParticle>();
        UIParticle[] embeddedViews = instance.GetComponentsInChildren<UIParticle>(true);
        foreach (UIParticle embeddedView in embeddedViews) embeddedView.enabled = false;
        view.raycastTarget = false;
        view.autoScalingMode = UIParticle.AutoScalingMode.Transform;
        view.scale = scale;
        view.RefreshParticles(new List<ParticleSystem>(instance.GetComponentsInChildren<ParticleSystem>(true)));
        foreach (ParticleSystem particle in view.particles)
        {
            var main = particle.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            // World-effect prefabs may offset a glow in depth; all layers land at the UI icon here.
            particle.transform.localPosition = Vector3.zero;
        }
        view.Stop();
        view.Clear();
        return view;
    }

    private void ConfigureParticleMaterials(GameObject instance)
    {
        foreach (ParticleSystemRenderer renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null) continue;
                var material = new Material(_additiveMaterial);
                material.mainTexture = source.mainTexture;
                _materials.Add(material);
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private RawImage CreateGlow(string effectName, Color color)
    {
        var node = new GameObject(effectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        node.layer = gameObject.layer;
        node.transform.SetParent(transform, false);
        node.transform.SetAsFirstSibling();
        RawImage image = node.GetComponent<RawImage>();
        image.texture = _glowTexture;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Image CreateRing(Vector3 center, float diameter)
    {
        var node = new GameObject("Landing shockwave", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        node.layer = gameObject.layer;
        node.transform.SetParent(transform, false);
        node.transform.SetAsFirstSibling();
        Image image = node.GetComponent<Image>();
        image.sprite = _ringSprite;
        image.material = _additiveMaterial;
        image.color = new Color(1f, .85f, .38f, .8f);
        image.raycastTarget = false;
        image.rectTransform.position = center;
        image.rectTransform.sizeDelta = Vector2.one * diameter * .8f;
        return image;
    }

    private void OnDisable()
    {
        _flightTarget = null;
        _arrival?.Kill();
        _arrival = null;
        StopParticles(_trail);
        StopParticles(_burst);
    }

    private static void StopParticles(UIParticle particles)
    {
        if (particles == null) return;
        particles.Stop();
        particles.Clear();
    }

    private void OnDestroy()
    {
        foreach (Material material in _materials)
            if (material != null) Destroy(material);
        _materials.Clear();
    }
}
