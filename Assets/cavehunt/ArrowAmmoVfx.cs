using UnityEngine;
using UnityEngine.Rendering;

public class ArrowAmmoVfx : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] private float tipOffsetScale = 0.55f;

    [Header("Normal")]
    [SerializeField] private Color normalCoreColor = new Color(1f, 0.95f, 0.72f, 0.82f);
    [SerializeField] private Color normalEdgeColor = new Color(1f, 1f, 1f, 0.04f);

    [Header("Fire")]
    [SerializeField] private Color fireCoreColor = new Color(1f, 0.58f, 0.08f, 1f);
    [SerializeField] private Color fireEdgeColor = new Color(1f, 0.06f, 0.01f, 0.18f);

    [Header("Air")]
    [SerializeField] private Color airCoreColor = new Color(0.72f, 0.95f, 1f, 0.9f);
    [SerializeField] private Color airEdgeColor = new Color(0.12f, 0.62f, 1f, 0.12f);

    [Header("Grenade")]
    [SerializeField] private Color grenadeCoreColor = new Color(1f, 0.95f, 0.22f, 1f);
    [SerializeField] private Color grenadeEdgeColor = new Color(0.18f, 1f, 0.08f, 0.18f);

    private AmmoType ammoType = AmmoType.Normal;
    private ParticleSystem nockedParticles;
    private ParticleSystem flightParticles;
    private TrailRenderer flightTrail;
    private Light ammoLight;
    private Material particleMaterial;
    private Material trailMaterial;
    private bool hasBeenShot;

    private void Awake()
    {
        EnsureVisuals();
        ApplyVisualState();
    }

    public void PrepareForNockedArrow()
    {
        hasBeenShot = false;
        EnsureVisuals();
        ResetTrail();
        ApplyVisualState();
    }

    public void SetAmmoType(AmmoType newAmmoType)
    {
        ammoType = newAmmoType;
        EnsureVisuals();
        ApplyVisualState();
    }

    public void PlayShot()
    {
        hasBeenShot = true;
        EnsureVisuals();
        ApplyVisualState();

        if (flightTrail != null)
        {
            flightTrail.Clear();
            flightTrail.emitting = true;
        }

        if (flightParticles != null && ammoType != AmmoType.Normal)
        {
            flightParticles.Play(true);
        }
    }

    public void PlayImpact(AmmoType impactAmmoType, Vector3 position, float explosionRadius)
    {
        Color coreColor;
        Color edgeColor;
        ResolveColors(impactAmmoType, out coreColor, out edgeColor);

        GameObject impactObject = new GameObject($"{impactAmmoType} Arrow Impact VFX");
        impactObject.transform.position = position;

        ParticleSystem burst = impactObject.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(
            burst,
            coreColor,
            edgeColor,
            impactAmmoType == AmmoType.Grenade ? 0.18f : impactAmmoType == AmmoType.Normal ? 0.045f : 0.08f,
            impactAmmoType == AmmoType.Grenade ? 7.5f : impactAmmoType == AmmoType.Normal ? 1.6f : 3.2f,
            false,
            ParticleSystemSimulationSpace.World,
            ParticleSystemShapeType.Sphere,
            impactAmmoType == AmmoType.Grenade ? 90 : impactAmmoType == AmmoType.Normal ? 14 : 28);

        ParticleSystemRenderer burstRenderer = burst.GetComponent<ParticleSystemRenderer>();
        burstRenderer.material = ResolveParticleMaterial();
        burstRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        if (impactAmmoType == AmmoType.Grenade)
        {
            CreateShockwave(impactObject.transform, Mathf.Max(0.25f, explosionRadius), grenadeCoreColor, 0.55f);
            CreateSmoke(impactObject.transform);
            ProceduralGameAudio.PlayExplosion(position);
        }
        else if (impactAmmoType == AmmoType.Air)
        {
            CreateShockwave(impactObject.transform, 1.35f, airCoreColor, 0.35f);
        }
        else if (impactAmmoType == AmmoType.Fire)
        {
            CreateShockwave(impactObject.transform, 0.85f, fireCoreColor, 0.25f);
        }
        else if (impactAmmoType == AmmoType.Normal)
        {
            CreateShockwave(impactObject.transform, 0.45f, normalCoreColor, 0.18f);
        }

        Destroy(impactObject, impactAmmoType == AmmoType.Grenade ? 3.5f : impactAmmoType == AmmoType.Normal ? 0.9f : 1.6f);
    }

    private void EnsureVisuals()
    {
        if (nockedParticles == null)
        {
            nockedParticles = CreateChildParticles("Selected Ammo Particles", ParticleSystemSimulationSpace.Local);
        }

        if (flightParticles == null)
        {
            flightParticles = CreateChildParticles("Shot Ammo Particles", ParticleSystemSimulationSpace.World);
        }

        if (flightTrail == null)
        {
            GameObject trailObject = new GameObject("Ammo Trail");
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = Vector3.zero;
            flightTrail = trailObject.AddComponent<TrailRenderer>();
            flightTrail.shadowCastingMode = ShadowCastingMode.Off;
            flightTrail.receiveShadows = false;
            flightTrail.time = 0.35f;
            flightTrail.minVertexDistance = 0.02f;
            flightTrail.startWidth = 0.16f;
            flightTrail.endWidth = 0.01f;
            flightTrail.material = ResolveTrailMaterial();
            flightTrail.emitting = false;
        }

        if (ammoLight == null)
        {
            GameObject lightObject = new GameObject("Ammo Glow");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = ResolveTipLocalPosition();
            ammoLight = lightObject.AddComponent<Light>();
            ammoLight.type = LightType.Point;
            ammoLight.range = 1.4f;
            ammoLight.intensity = 1.4f;
            ammoLight.enabled = false;
        }
    }

    private ParticleSystem CreateChildParticles(string objectName, ParticleSystemSimulationSpace simulationSpace)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = ResolveTipLocalPosition();

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(
            particles,
            Color.white,
            Color.white,
            0.06f,
            0.6f,
            true,
            simulationSpace,
            ParticleSystemShapeType.Cone,
            12);
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = ResolveParticleMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        return particles;
    }

    private void ApplyVisualState()
    {
        bool showAmmoVfx = ammoType != AmmoType.Normal;
        bool showNormalFlightFeedback = ammoType == AmmoType.Normal && hasBeenShot;
        Color coreColor;
        Color edgeColor;
        ResolveColors(ammoType, out coreColor, out edgeColor);

        if (nockedParticles != null)
        {
            nockedParticles.transform.localPosition = ResolveTipLocalPosition();
            ConfigureParticleSystem(
                nockedParticles,
                coreColor,
                edgeColor,
                ammoType == AmmoType.Air ? 0.045f : 0.065f,
                ammoType == AmmoType.Air ? 0.75f : 0.55f,
                true,
                ParticleSystemSimulationSpace.Local,
                ammoType == AmmoType.Air ? ParticleSystemShapeType.Donut : ParticleSystemShapeType.Cone,
                ammoType == AmmoType.Grenade ? 18 : 28);

            SetParticlePlaying(nockedParticles, showAmmoVfx && !hasBeenShot);
        }

        if (flightParticles != null)
        {
            flightParticles.transform.localPosition = ResolveTipLocalPosition();
            ConfigureParticleSystem(
                flightParticles,
                coreColor,
                edgeColor,
                ammoType == AmmoType.Air ? 0.05f : 0.075f,
                ammoType == AmmoType.Air ? 1.7f : 1.05f,
                true,
                ParticleSystemSimulationSpace.World,
                ammoType == AmmoType.Air ? ParticleSystemShapeType.Donut : ParticleSystemShapeType.Cone,
                ammoType == AmmoType.Grenade ? 22 : 38);

            SetParticlePlaying(flightParticles, showAmmoVfx && hasBeenShot);
        }

        if (flightTrail != null)
        {
            flightTrail.startColor = coreColor;
            flightTrail.endColor = edgeColor;
            flightTrail.startWidth = ammoType == AmmoType.Grenade ? 0.22f : ammoType == AmmoType.Fire ? 0.18f : ammoType == AmmoType.Normal ? 0.055f : 0.13f;
            flightTrail.time = ammoType == AmmoType.Air ? 0.48f : ammoType == AmmoType.Normal ? 0.22f : 0.36f;
            flightTrail.emitting = hasBeenShot && (showAmmoVfx || showNormalFlightFeedback);
        }

        if (ammoLight != null)
        {
            ammoLight.transform.localPosition = ResolveTipLocalPosition();
            ammoLight.color = coreColor;
            ammoLight.intensity = ammoType == AmmoType.Air ? 0.9f : 1.6f;
            ammoLight.enabled = showAmmoVfx;
        }
    }

    private void ConfigureParticleSystem(
        ParticleSystem particles,
        Color coreColor,
        Color edgeColor,
        float size,
        float speed,
        bool loop,
        ParticleSystemSimulationSpace simulationSpace,
        ParticleSystemShapeType shapeType,
        int emissionRate)
    {
        ParticleSystem.MainModule main = particles.main;
        main.loop = loop;
        main.playOnAwake = false;
        main.duration = 1f;
        main.maxParticles = 180;
        main.simulationSpace = simulationSpace;
        main.startLifetime = new ParticleSystem.MinMaxCurve(loop ? 0.18f : 0.2f, loop ? 0.5f : 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(coreColor, edgeColor);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = loop ? emissionRate : 0;
        if (!loop)
        {
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Clamp(emissionRate, 1, short.MaxValue))
            });
        }

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = shapeType;
        shape.radius = shapeType == ParticleSystemShapeType.Donut ? 0.11f : 0.035f;
        shape.angle = 24f;

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = ammoType == AmmoType.Air ? 0.32f : 0.12f;
        noise.frequency = ammoType == AmmoType.Air ? 1.4f : 0.7f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(coreColor, 0f),
                new GradientColorKey(edgeColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(coreColor.a, 0f),
                new GradientAlphaKey(edgeColor.a, 1f)
            });
        colorOverLifetime.color = gradient;
    }

    private void SetParticlePlaying(ParticleSystem particles, bool shouldPlay)
    {
        if (particles == null) return;

        if (shouldPlay)
        {
            if (!particles.isPlaying)
            {
                particles.Play(true);
            }
        }
        else if (particles.isPlaying)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void ResetTrail()
    {
        if (flightTrail == null) return;

        flightTrail.emitting = false;
        flightTrail.Clear();
    }

    private Vector3 ResolveTipLocalPosition()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponent<ParticleSystemRenderer>() != null || renderer.GetComponent<TrailRenderer>() != null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            return Vector3.forward * 0.45f;
        }

        Vector3 tipWorldPosition = bounds.center + transform.forward * bounds.extents.magnitude * Mathf.Max(0.05f, tipOffsetScale);
        return transform.InverseTransformPoint(tipWorldPosition);
    }

    private void ResolveColors(AmmoType type, out Color coreColor, out Color edgeColor)
    {
        switch (type)
        {
            case AmmoType.Fire:
                coreColor = fireCoreColor;
                edgeColor = fireEdgeColor;
                break;
            case AmmoType.Air:
                coreColor = airCoreColor;
                edgeColor = airEdgeColor;
                break;
            case AmmoType.Grenade:
                coreColor = grenadeCoreColor;
                edgeColor = grenadeEdgeColor;
                break;
            default:
                coreColor = normalCoreColor;
                edgeColor = normalEdgeColor;
                break;
        }
    }

    private Material ResolveParticleMaterial()
    {
        if (particleMaterial != null) return particleMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default");
        particleMaterial = shader != null ? new Material(shader) : null;
        if (particleMaterial != null)
        {
            particleMaterial.name = "Runtime Arrow Ammo Particle Material";
        }

        return particleMaterial;
    }

    private Material ResolveTrailMaterial()
    {
        if (trailMaterial != null) return trailMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Sprites/Default");
        trailMaterial = shader != null ? new Material(shader) : null;
        if (trailMaterial != null)
        {
            trailMaterial.name = "Runtime Arrow Ammo Trail Material";
        }

        return trailMaterial;
    }

    private void CreateShockwave(Transform parent, float radius, Color color, float lifetime)
    {
        GameObject ringObject = new GameObject("Ammo Impact Shockwave");
        ringObject.transform.SetParent(parent, false);
        ringObject.transform.localPosition = Vector3.zero;

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 64;
        ring.widthMultiplier = 0.055f;
        ring.shadowCastingMode = ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.material = ResolveTrailMaterial();
        ring.startColor = color;
        ring.endColor = color;

        ArrowAmmoShockwave shockwave = ringObject.AddComponent<ArrowAmmoShockwave>();
        shockwave.Initialize(ring, radius, color, lifetime);
    }

    private void CreateSmoke(Transform parent)
    {
        GameObject smokeObject = new GameObject("Grenade Smoke");
        smokeObject.transform.SetParent(parent, false);
        smokeObject.transform.localPosition = Vector3.zero;

        ParticleSystem smoke = smokeObject.AddComponent<ParticleSystem>();
        ConfigureParticleSystem(
            smoke,
            new Color(0.28f, 0.28f, 0.28f, 0.55f),
            new Color(0.08f, 0.08f, 0.08f, 0.02f),
            0.34f,
            1.2f,
            false,
            ParticleSystemSimulationSpace.World,
            ParticleSystemShapeType.Sphere,
            36);

        ParticleSystem.MainModule main = smoke.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.95f);

        ParticleSystemRenderer renderer = smoke.GetComponent<ParticleSystemRenderer>();
        renderer.material = ResolveParticleMaterial();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }
}

public class ArrowAmmoShockwave : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private float maxRadius;
    private Color color;
    private float lifetime;
    private float age;

    public void Initialize(LineRenderer ring, float radius, Color ringColor, float duration)
    {
        lineRenderer = ring;
        maxRadius = Mathf.Max(0.01f, radius);
        color = ringColor;
        lifetime = Mathf.Max(0.05f, duration);
        DrawRing(0.01f);
    }

    private void Update()
    {
        if (lineRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        float radius = Mathf.Lerp(0.05f, maxRadius, 1f - (1f - t) * (1f - t));
        DrawRing(radius);

        Color faded = color;
        faded.a *= 1f - t;
        lineRenderer.startColor = faded;
        lineRenderer.endColor = faded;
        lineRenderer.widthMultiplier = Mathf.Lerp(0.07f, 0.015f, t);

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void DrawRing(float radius)
    {
        int count = lineRenderer.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = (i / (float)count) * Mathf.PI * 2f;
            lineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }
}
