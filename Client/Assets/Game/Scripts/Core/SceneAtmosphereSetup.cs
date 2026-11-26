using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WolfAndRabbits.Client.Core
{
    /// <summary>
    /// Applies stylized Warcraft 3 golden-hour sunlight, cool forest trilight ambient,
    /// and sparkling URP Bloom / ACES Tonemapping on scene startup.
    /// </summary>
    public class SceneAtmosphereSetup : MonoBehaviour
    {
        [Header("Lighting References")]
        [SerializeField] private Light directionalSun;
        [SerializeField] private Volume globalVolume;
        [SerializeField] private Camera targetCamera;

        [Header("Bloom Settings")]
        [SerializeField] private float bloomThreshold = 0.90f;
        [SerializeField] private float bloomIntensity = 0.58f;
        [SerializeField] private float bloomScatter = 0.64f;

        private void Awake()
        {
            ApplyLighting();
            ApplyPostProcessing();
            ApplyCameraSettings();
        }

        private void ApplyLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.50f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.24f, 0.34f, 0.28f);
            RenderSettings.ambientGroundColor = new Color(0.11f, 0.14f, 0.18f);
            RenderSettings.fog = false;

            if (directionalSun == null)
                directionalSun = FindFirstObjectByType<Light>();

            if (directionalSun != null && directionalSun.type == LightType.Directional)
            {
                directionalSun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
                directionalSun.color = new Color(1.0f, 0.92f, 0.76f);
                directionalSun.intensity = 2.15f;
                directionalSun.shadows = LightShadows.Soft;
                directionalSun.shadowStrength = 0.72f;
            }
        }

        private void ApplyPostProcessing()
        {
            if (globalVolume == null)
                globalVolume = FindFirstObjectByType<Volume>();

            if (globalVolume == null) return;
            if (globalVolume.profile == null)
                globalVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            if (!globalVolume.profile.TryGet(out Bloom bloom))
                bloom = globalVolume.profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = bloomThreshold;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = bloomIntensity;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = bloomScatter;

            if (!globalVolume.profile.TryGet(out Tonemapping tone))
                tone = globalVolume.profile.Add<Tonemapping>(true);
            tone.active = true;
            tone.mode.overrideState = true;
            tone.mode.value = TonemappingMode.ACES;

            if (!globalVolume.profile.TryGet(out Vignette vignette))
                vignette = globalVolume.profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.18f;
        }

        private void ApplyCameraSettings()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            if (targetCamera == null) return;

            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0.06f, 0.11f, 0.14f);

            UniversalAdditionalCameraData urpData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
            if (urpData == null)
                urpData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = true;
            urpData.renderShadows = true;
        }
    }
}
