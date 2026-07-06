using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RagNextPlayer.Managers
{
    public class TransitionVFXManager : MonoBehaviour
    {
        public static TransitionVFXManager Instance { get; private set; }

        private Camera _vfxCamera;
        private GameObject _vfxCameraGo;
        private RenderTexture _vfxRT;

        public RenderTexture VFXRenderTexture => _vfxRT;
        
        // Transition Emitters (Heavy)
        private ParticleSystem _smokePS;
        private ParticleSystem _sandPS;
        private ParticleSystem _transitionEmbersPS;
        private ParticleSystem _transitionRainPS;
        private ParticleSystem _transitionSnowPS;

        // Room/Overlay Emitters (Light)
        private ParticleSystem _embersPS;
        private ParticleSystem _rainPS;
        private ParticleSystem _snowPS;
        private ParticleSystem _roomSmokePS;
        private ParticleSystem _roomSandPS;

        // Camera shake variables
        private Vector3 _mainCamOriginalPos;
        private Vector3 _vfxCamOriginalPos;
        private Coroutine _shakeCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeVFXPipeline();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeVFXPipeline()
        {
            // 1. Create a dynamic RenderTexture for UI Toolkit overlay mapping
            _vfxRT = new RenderTexture(1920, 1080, 16, RenderTextureFormat.ARGB32);
            _vfxRT.Create();

            // 2. Create the VFX camera rendering to the RenderTexture
            _vfxCameraGo = new GameObject("VFX Camera");
            _vfxCameraGo.transform.SetParent(transform);
            _vfxCamera = _vfxCameraGo.AddComponent<Camera>();
            _vfxCamera.clearFlags = CameraClearFlags.SolidColor;
            _vfxCamera.backgroundColor = new Color(0, 0, 0, 0); // Transparent background
            _vfxCamera.cullingMask = 1 << 31; // Render only Layer 31 (VFX Layer)
            _vfxCamera.targetTexture = _vfxRT;
            _vfxCamera.orthographic = true;
            _vfxCamera.orthographicSize = 5f;
            _vfxCamera.nearClipPlane = 0.3f;
            _vfxCamera.farClipPlane = 100f;

            int vfxLayer = 31;

            _smokePS = CreateParticleSystem("VFX_Smoke", vfxLayer, createSmoke: true);
            _sandPS = CreateParticleSystem("VFX_Sand", vfxLayer, createSand: true);
            _transitionEmbersPS = CreateParticleSystem("VFX_TransitionEmbers", vfxLayer, createTransitionEmbers: true);
            _transitionRainPS = CreateParticleSystem("VFX_TransitionRain", vfxLayer, createTransitionRain: true);
            _transitionSnowPS = CreateParticleSystem("VFX_TransitionSnow", vfxLayer, createTransitionSnow: true);

            _embersPS = CreateParticleSystem("VFX_Embers", vfxLayer, createEmbers: true);
            _rainPS = CreateParticleSystem("VFX_Rain", vfxLayer, createRain: true);
            _snowPS = CreateParticleSystem("VFX_Snow", vfxLayer, createSnow: true);
            _roomSmokePS = CreateParticleSystem("VFX_RoomSmoke", vfxLayer, createRoomSmoke: true);
            _roomSandPS = CreateParticleSystem("VFX_RoomSand", vfxLayer, createRoomSand: true);
        }

        private ParticleSystem CreateParticleSystem(string name, int layer, 
            bool createSmoke = false, 
            bool createSand = false, 
            bool createTransitionEmbers = false, 
            bool createTransitionRain = false, 
            bool createTransitionSnow = false,
            bool createEmbers = false, 
            bool createRain = false, 
            bool createSnow = false,
            bool createRoomSmoke = false,
            bool createRoomSand = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(_vfxCamera != null ? _vfxCamera.transform : transform);
            go.layer = layer;
            go.transform.localPosition = new Vector3(0, 0, 5f); // Position directly in front of camera frustum

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;

            // Apply texture-less default particle look with dynamic color over lifetime for premium visuals
            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient grad = new Gradient();

            if (createSmoke)
            {
                main.duration = 2f;
                main.loop = true;
                main.prewarm = true; // Instant full-screen coverage
                main.startLifetime = 4.0f; // Fill viewport fully
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(3.0f, 6.0f); // Massive dark puff sizes
                main.startColor = new Color(0.12f, 0.12f, 0.12f, 0.98f); // Opaque black smoke to block out screen
                main.maxParticles = 1200;

                emission.rateOverTime = 600f; // Extreme density
                
                shape.scale = new Vector3(16f, 10f, 1f); // Covers entire viewport

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.black, 0.0f), new GradientColorKey(Color.black, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(0.98f, 0.2f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);
            }
            else if (createSand)
            {
                main.duration = 1.5f;
                main.loop = true;
                main.prewarm = true; // Instant full-screen coverage
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 2.8f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.2f); // Much larger sand grains
                main.startColor = new Color(0.75f, 0.55f, 0.25f, 0.98f); // Thick opaque brown sandstorm
                main.maxParticles = 8000; 

                emission.rateOverTime = 6000f; // Massive sand wave
                
                // Spawn across entire screen instantly, blowing horizontally
                shape.scale = new Vector3(16f, 10f, 1f); 
                go.transform.localPosition = new Vector3(0f, 0f, 5f); // Start centered
                go.transform.localRotation = Quaternion.identity; // No rotation needed

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(0.9f, 0.8f, 0.5f), 0.0f), new GradientColorKey(new Color(0.6f, 0.4f, 0.2f), 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(0.9f, 0.1f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);

                // Move horizontally to the right across the text block, with organic vertical dispersion
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(10f, 18f); // Faster horizontal sweep velocity
                velocity.y = new ParticleSystem.MinMaxCurve(-1.8f, 1.8f); // Wide vertical drift
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                // Add organic wind-blown turbulence to simulate a text-dissolve
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1.2f; // Violent swirly dust motion
                noise.frequency = 0.9f;
                noise.scrollSpeed = 2.0f;
            }
            else if (createEmbers)
            {
                main.duration = 5f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 4.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.5f); // Faster upward movement
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.08f); // Glowing sparks size
                main.startColor = new Color(1.0f, 0.6f, 0.1f, 1.0f); // Bright golden-orange
                main.maxParticles = 800;

                emission.rateOverTime = 120f; // Much denser sparks
                emission.enabled = false; // Toggled by SetAmbientOverlay

                // Due to the -90 rotation around X, Z and Y scales are swapped in world space
                shape.scale = new Vector3(15f, 1f, 10f); // Width 15, depth 1 (Y in local space), height 10 (Z in local space)
                go.transform.localPosition = new Vector3(0, 0, 5f); // Centered
                go.transform.localRotation = Quaternion.Euler(-90f, 0, 0); // Rotate -90 on X so speed shoots them UPWARDS

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0.0f), new GradientColorKey(Color.red, 1.0f) }, // Bright hot core to cooling red
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(1.0f, 0.2f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
                velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
            else if (createRain)
            {
                main.duration = 2f;
                main.loop = true;
                main.startLifetime = 1.0f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 25f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
                main.startColor = new Color(0.6f, 0.8f, 1.0f, 0.4f);
                main.maxParticles = 500;

                emission.rateOverTime = 150f;
                
                // Rotated by 80 on X; swap local Y and Z scales for full height distribution
                shape.scale = new Vector3(15f, 1f, 10f); 
                go.transform.localPosition = new Vector3(0, 0, 5f); // Centered
                go.transform.localRotation = Quaternion.Euler(80f, 0, 0); // Angle downwards

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.cyan, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);
            }
            else if (createSnow)
            {
                main.duration = 5f;
                main.loop = true;
                main.startLifetime = 4.0f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
                main.startColor = new Color(1f, 1f, 1f, 0.7f);
                main.maxParticles = 300;

                emission.rateOverTime = 50f;
                
                // Rotated 90 on X; swap local Y and Z scales so snow spawns across full height
                shape.scale = new Vector3(15f, 1f, 10f); 
                go.transform.localPosition = new Vector3(0, 0, 5f); // Centered
                go.transform.localRotation = Quaternion.Euler(90f, 0, 0); // Angle straight down

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
                velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
            else if (createTransitionEmbers)
            {
                main.duration = 5f;
                main.loop = true;
                main.prewarm = true; // Instant full-screen coverage
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 7.0f); // Fast, high-energy transition particles
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f); // Larger sparks to block screen
                main.startColor = new Color(1.0f, 0.5f, 0.05f, 1.0f); 
                main.maxParticles = 5000;

                emission.rateOverTime = 2000f; // Extreme embers screen fill
                emission.enabled = true;

                shape.scale = new Vector3(16f, 1f, 10f); // Spawns across entire width
                go.transform.localPosition = new Vector3(0, 0, 5f); 
                go.transform.localRotation = Quaternion.Euler(-90f, 0, 0); 

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0.0f), new GradientColorKey(Color.red, 1.0f) }, 
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(1.0f, 0.2f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
                velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
            else if (createTransitionRain)
            {
                main.duration = 2f;
                main.loop = true;
                main.prewarm = true; // Instant full-screen coverage
                main.startLifetime = 1.0f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(25f, 45f); // Fast transition storm
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f); // Thick opaque water streaks
                main.startColor = new Color(0.5f, 0.75f, 1.0f, 0.95f);
                main.maxParticles = 8000;

                emission.rateOverTime = 3500f; // Torrential rainfall screen block
                emission.enabled = true;
                
                shape.scale = new Vector3(16f, 1f, 10f); 
                go.transform.localPosition = new Vector3(0, 0, 5f); 
                go.transform.localRotation = Quaternion.Euler(80f, 0, 0); 

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.cyan, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(0.6f, 0.2f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);
            }
            else if (createTransitionSnow)
            {
                main.duration = 5f;
                main.loop = true;
                main.prewarm = true; // Instant full-screen coverage
                main.startLifetime = 4.0f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(4.0f, 8.0f); // Fast transition blizzard
                main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.2f); // Big fluffy flakes all the way down
                main.startColor = new Color(1f, 1f, 1f, 0.98f);
                main.maxParticles = 5000;

                emission.rateOverTime = 1500f; // Extreme blizzard screen block
                emission.enabled = true;
                
                shape.scale = new Vector3(16f, 1f, 10f); // Wide emitter box
                go.transform.localPosition = new Vector3(0, 0, 5f); 
                go.transform.localRotation = Quaternion.Euler(90f, 0, 0); 

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
                velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }
            else if (createRoomSmoke)
            {
                main.duration = 2f;
                main.loop = true;
                main.startLifetime = 3.5f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
                main.startColor = new Color(0.8f, 0.8f, 0.8f, 0.35f); // Visible smoke for rooms
                main.maxParticles = 200;

                emission.rateOverTime = 45f; // Good ambient atmosphere
                emission.enabled = false;
                
                shape.scale = new Vector3(16f, 10f, 1f); // Covers entire viewport

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.grey, 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(0.4f, 0.2f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);
            }
            else if (createRoomSand)
            {
                main.duration = 1.5f;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.2f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.28f); // Sand grains visible
                main.startColor = new Color(0.9f, 0.72f, 0.42f, 0.55f); // Noticeable sand in rooms
                main.maxParticles = 500; 

                emission.rateOverTime = 80f; // Sparse but clearly visible blowing dust
                emission.enabled = false;
                
                shape.scale = new Vector3(16f, 10f, 1f); // Spawns across entire screen
                go.transform.localPosition = new Vector3(0, 0, 5f); // Centered
                go.transform.localRotation = Quaternion.identity;

                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(0.9f, 0.8f, 0.5f), 0.0f), new GradientColorKey(new Color(0.6f, 0.4f, 0.2f), 1.0f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0f, 0.0f), new GradientAlphaKey(0.8f, 0.1f), new GradientAlphaKey(0f, 1.0f) }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.x = new ParticleSystem.MinMaxCurve(1.5f, 3.5f); // Gentle drift to the right
                velocity.y = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.3f;
                noise.frequency = 0.5f;
                noise.scrollSpeed = 1.0f;
            }

            // Configure Particle Renderer
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                // Select billboard vs stretched lines
                if (createEmbers || createTransitionEmbers)
                {
                    psr.renderMode = ParticleSystemRenderMode.Stretch;
                    psr.velocityScale = 0.25f; // More pronounced stretch
                    psr.lengthScale = 3.5f;    
                }
                else if (createRain || createTransitionRain)
                {
                    psr.renderMode = ParticleSystemRenderMode.Stretch;
                    psr.velocityScale = 0.35f;
                    psr.lengthScale = 4.0f;
                }
                else
                {
                    psr.renderMode = ParticleSystemRenderMode.Billboard;
                }

                psr.sortingLayerName = "Default";
                psr.sortingOrder = 9999; 

                // Use additive blending for glowing hot sparks, standard alpha blend for others
                Shader shader = null;
                if (createEmbers || createTransitionEmbers)
                {
                    shader = Shader.Find("Mobile/Particles/Additive");
                    if (shader == null) shader = Shader.Find("Particles/Additive");
                    if (shader == null) shader = Shader.Find("Legacy Shaders/Particles/Additive");
                }
                
                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                    if (shader == null) shader = Shader.Find("UI/Default");
                }

                if (shader != null)
                {
                    Material mat = new Material(shader);
                    
                    // Assign procedural soft feathered texture to prevent boxy artifacts
                    mat.mainTexture = GenerateSoftCircleTexture();
                    
                    psr.material = mat;
                    Debug.Log($"[TransitionVFXManager] Configured renderer with soft texture for '{name}' using shader '{shader.name}'");
                }
                else
                {
                    Debug.LogWarning($"[TransitionVFXManager] Could not find suitable shader for '{name}' renderer!");
                }
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        private Texture2D GenerateSoftCircleTexture()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float normalizedDist = dist / center;
                    float alpha = Mathf.Clamp01(1f - normalizedDist);
                    alpha = Mathf.Pow(alpha, 1.4f); // Softer curve for wider opacity filling
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        private void EnsureCameraStacked()
        {
            // Decoupled; VFX camera renders directly to RenderTexture
        }

        public void PlayTransitionEffect(string style, float duration)
        {
            Debug.Log($"[TransitionVFXManager] PlayTransitionEffect: style='{style}', duration={duration}, _vfxCamera is null: {(_vfxCamera == null)}");
            EnsureCameraStacked();
            StopAllTransitionEffects();

            if ((style.Equals("ParticleSmoke", StringComparison.OrdinalIgnoreCase) || style.Equals("Smoke", StringComparison.OrdinalIgnoreCase)) && _smokePS != null)
            {
                StartCoroutine(TriggerPSRoutine(_smokePS, duration));
            }
            else if ((style.Equals("ParticleSand", StringComparison.OrdinalIgnoreCase) || style.Equals("Sand", StringComparison.OrdinalIgnoreCase) || style.Equals("Sandstorm", StringComparison.OrdinalIgnoreCase)) && _sandPS != null)
            {
                StartCoroutine(TriggerPSRoutine(_sandPS, duration));
            }
            else if ((style.Equals("ParticleEmbers", StringComparison.OrdinalIgnoreCase) || style.Equals("Embers", StringComparison.OrdinalIgnoreCase)) && _transitionEmbersPS != null)
            {
                StartCoroutine(TriggerPSRoutine(_transitionEmbersPS, duration));
            }
            else if ((style.Equals("ParticleRain", StringComparison.OrdinalIgnoreCase) || style.Equals("Rain", StringComparison.OrdinalIgnoreCase)) && _transitionRainPS != null)
            {
                StartCoroutine(TriggerPSRoutine(_transitionRainPS, duration));
            }
            else if ((style.Equals("ParticleSnow", StringComparison.OrdinalIgnoreCase) || style.Equals("Snow", StringComparison.OrdinalIgnoreCase)) && _transitionSnowPS != null)
            {
                StartCoroutine(TriggerPSRoutine(_transitionSnowPS, duration));
            }
        }

        private IEnumerator TriggerPSRoutine(ParticleSystem ps, float duration)
        {
            var emission = ps.emission;
            emission.enabled = true;
            ps.Play();

            // Emit during transition fade-in and hold
            yield return new WaitForSeconds(duration * 0.7f);
            
            // Turn off emission so it resolves/fades away
            emission.enabled = false;
        }

        public void SetAmbientOverlay(string style, bool active)
        {
            Debug.Log($"[TransitionVFXManager] SetAmbientOverlay: style='{style}', active={active}, _vfxCamera is null: {(_vfxCamera == null)}");
            EnsureCameraStacked();
            if (active)
            {
                if (_embersPS != null) SetPSEnabled(_embersPS, style == "Embers");
                if (_rainPS != null) SetPSEnabled(_rainPS, style == "Rain");
                if (_snowPS != null) SetPSEnabled(_snowPS, style == "Snow");
                if (_roomSmokePS != null) SetPSEnabled(_roomSmokePS, style == "Smoke");
                if (_roomSandPS != null) SetPSEnabled(_roomSandPS, style == "Sand");
            }
            else
            {
                if (style == "Embers" && _embersPS != null) SetPSEnabled(_embersPS, false);
                if (style == "Rain" && _rainPS != null) SetPSEnabled(_rainPS, false);
                if (style == "Snow" && _snowPS != null) SetPSEnabled(_snowPS, false);
                if (style == "Smoke" && _roomSmokePS != null) SetPSEnabled(_roomSmokePS, false);
                if (style == "Sand" && _roomSandPS != null) SetPSEnabled(_roomSandPS, false);
            }
        }

        private void SetPSEnabled(ParticleSystem ps, bool enabled)
        {
            var emission = ps.emission;
            emission.enabled = enabled;
            if (enabled)
            {
                if (!ps.isPlaying) ps.Play();
            }
            else
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void StopAllTransitionEffects()
        {
            if (_smokePS != null) _smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_sandPS != null) _sandPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_transitionEmbersPS != null) _transitionEmbersPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_transitionRainPS != null) _transitionRainPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_transitionSnowPS != null) _transitionSnowPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void LogActiveParticles()
        {
            int smokeCount = _smokePS != null ? _smokePS.particleCount : 0;
            int sandCount = _sandPS != null ? _sandPS.particleCount : 0;
            int embersCount = _embersPS != null ? _embersPS.particleCount : 0;
            int rainCount = _rainPS != null ? _rainPS.particleCount : 0;
            int snowCount = _snowPS != null ? _snowPS.particleCount : 0;
            int roomSmokeCount = _roomSmokePS != null ? _roomSmokePS.particleCount : 0;
            int roomSandCount = _roomSandPS != null ? _roomSandPS.particleCount : 0;

            bool embersPlaying = _embersPS != null ? _embersPS.isPlaying : false;
            bool embersEmitting = _embersPS != null ? _embersPS.emission.enabled : false;

            Debug.Log($"[TransitionVFXManager] Active Particles - Smoke: {smokeCount}, Sand: {sandCount}, Embers: {embersCount} (playing={embersPlaying}, emitting={embersEmitting}), Rain: {rainCount}, Snow: {snowCount}, RoomSmoke: {roomSmokeCount}, RoomSand: {roomSandCount}");
        }

        public void TriggerScreenShake(float intensity, float duration)
        {
            EnsureCameraStacked();
            Camera mainCam = Camera.main;
            if (mainCam != null && _shakeCoroutine == null)
            {
                _mainCamOriginalPos = mainCam.transform.localPosition;
                if (_vfxCamera != null) _vfxCamOriginalPos = _vfxCamera.transform.localPosition;
            }

            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);

            if (intensity <= 0f || duration <= 0f)
            {
                if (mainCam != null) mainCam.transform.localPosition = _mainCamOriginalPos;
                if (_vfxCamera != null) _vfxCamera.transform.localPosition = _vfxCamOriginalPos;
                _shakeCoroutine = null;
                return;
            }

            _shakeCoroutine = StartCoroutine(ShakeCameraRoutine(intensity, duration));
        }

        private IEnumerator ShakeCameraRoutine(float intensity, float duration)
        {
            Camera mainCam = Camera.main;
            UnityEngine.UIElements.VisualElement uiRoot = UIManager.Instance != null ? UIManager.Instance.RootElement : null;
            Vector3 originalUIPos = uiRoot != null ? uiRoot.transform.position : Vector3.zero;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = 1f - (elapsed / duration); // Dampen shake over time
                Vector3 offset = UnityEngine.Random.insideUnitSphere * intensity * percent;
                offset.z = 0; // Keep on 2D plane

                if (mainCam != null) mainCam.transform.localPosition = _mainCamOriginalPos + offset;
                if (_vfxCamera != null) _vfxCamera.transform.localPosition = _vfxCamOriginalPos + offset;

                if (uiRoot != null)
                {
                    Vector3 uiOffset = UnityEngine.Random.insideUnitSphere * (intensity * 80f) * percent;
                    uiOffset.z = 0;
                    uiRoot.transform.position = originalUIPos + uiOffset;
                }

                yield return null;
            }

            if (mainCam != null) mainCam.transform.localPosition = _mainCamOriginalPos;
            if (_vfxCamera != null) _vfxCamera.transform.localPosition = _vfxCamOriginalPos;
            if (uiRoot != null) uiRoot.transform.position = originalUIPos;
            _shakeCoroutine = null;
        }
    }
}
