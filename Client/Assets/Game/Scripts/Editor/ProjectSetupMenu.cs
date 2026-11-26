#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WolfAndRabbits.Client.Audio;
using WolfAndRabbits.Client.Core;
using WolfAndRabbits.Client.Lobby;
using WolfAndRabbits.Client.Network;
using WolfAndRabbits.Client.Play;
using WolfAndRabbits.Client.UI;
using WolfAndRabbits.Client.Visuals;

namespace WolfAndRabbits.Client.Editor
{
    public static class ProjectSetupMenu
    {
        private const string LobbyScenePath = "Assets/Game/Scenes/Lobby.unity";
        private const string PlayScenePath = "Assets/Game/Scenes/Play.unity";
        private const string VolumeProfilePath = "Assets/Settings/SampleSceneProfile.asset";
        private const string MobilePreviewMenuPath = "Wolf and Rabbits/Preview Mobile UI (Joystick) in Editor";
        private const string DevFlagMenuPath = "Wolf and Rabbits/Build Flag/Development (Port 5000)";
        private const string ReleaseFlagMenuPath = "Wolf and Rabbits/Build Flag/Release (Port 80)";

        private static readonly NamedBuildTarget[] SupportedTargets =
        {
            NamedBuildTarget.Standalone,
            NamedBuildTarget.WebGL,
            NamedBuildTarget.Android,
            NamedBuildTarget.iOS
        };

        [MenuItem(DevFlagMenuPath, priority = 5)]
        public static void SetDevelopmentMode()
        {
            ApplyBuildFlag(isRelease: false);
            EditorUtility.DisplayDialog(
                "Build Flag: Development",
                "Đã chuyển sang chế độ DEVELOPMENT:\n- Scripting Define: APP_DEVELOPMENT\n- Cổng mặc định: 5000 (ws://localhost:5000/ws)\n- Development Build = true",
                "OK");
        }

        [MenuItem(DevFlagMenuPath, true)]
        public static bool ValidateSetDevelopmentMode()
        {
            Menu.SetChecked(DevFlagMenuPath, !IsReleaseFlagSet());
            return true;
        }

        [MenuItem(ReleaseFlagMenuPath, priority = 6)]
        public static void SetReleaseMode()
        {
            ApplyBuildFlag(isRelease: true);
            EditorUtility.DisplayDialog(
                "Build Flag: Release",
                "Đã chuyển sang chế độ RELEASE:\n- Scripting Define: APP_RELEASE\n- Cổng mặc định: 80 (ws://<host>/ws)\n- Development Build = false\n\nChỉnh domain/IP VPS cho Mobile tại:\nAssets/Game/Resources/server_config.json",
                "OK");
        }

        [MenuItem(ReleaseFlagMenuPath, true)]
        public static bool ValidateSetReleaseMode()
        {
            Menu.SetChecked(ReleaseFlagMenuPath, IsReleaseFlagSet());
            return true;
        }

        private static bool IsReleaseFlagSet()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
            return defines.Split(';').Any(d => d.Trim() == BuildEnvironment.DefineRelease);
        }

        private static void ApplyBuildFlag(bool isRelease)
        {
            string addDefine = isRelease ? BuildEnvironment.DefineRelease : BuildEnvironment.DefineDevelopment;
            string removeDefine = isRelease ? BuildEnvironment.DefineDevelopment : BuildEnvironment.DefineRelease;

            foreach (NamedBuildTarget target in SupportedTargets)
            {
                string current = PlayerSettings.GetScriptingDefineSymbols(target);
                List<string> list = current
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s) && s != removeDefine)
                    .Distinct()
                    .ToList();

                if (!list.Contains(addDefine))
                    list.Add(addDefine);

                PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            }

            EditorUserBuildSettings.development = !isRelease;
            Debug.Log("[WolfAndRabbits] Build Flag switched to: " + (isRelease ? "RELEASE (Port 80)" : "DEVELOPMENT (Port 5000)"));
        }

        [MenuItem(MobilePreviewMenuPath, priority = 10)]
        public static void ToggleMobilePreviewInEditor()
        {
            bool next = !PlatformProfile.SimulateMobileInEditor;
            PlatformProfile.SimulateMobileInEditor = next;
            Menu.SetChecked(MobilePreviewMenuPath, next);
            Debug.Log("[WolfAndRabbits] Chế độ xem thử trong Editor: " + (next ? "MOBILE (Có Joystick + SafeArea)" : "WEB / PC (Không Joystick)"));
        }

        [MenuItem(MobilePreviewMenuPath, true)]
        public static bool ValidateToggleMobilePreviewInEditor()
        {
            Menu.SetChecked(MobilePreviewMenuPath, PlatformProfile.SimulateMobileInEditor);
            return true;
        }

        [MenuItem("Wolf and Rabbits/Open Lobby Scene", priority = 1)]
        public static void OpenLobbyScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(LobbyScenePath);
        }

        [MenuItem("Wolf and Rabbits/Open Play Scene", priority = 2)]
        public static void OpenPlayScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(PlayScenePath);
        }

        [MenuItem("Wolf and Rabbits/Rebuild Lobby & Play Scenes", priority = 20)]
        public static void RebuildScenesAndBuildSettings()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            BuildLobbyScene();
            BuildPlayScene();
            ApplyBuildSettings();

            EditorSceneManager.OpenScene(LobbyScenePath);
            EditorUtility.DisplayDialog(
                "Wolf and Rabbits 3D",
                "Đã tạo lại và liên kết đầy đủ 2 scene:\n- Assets/Game/Scenes/Lobby.unity (Index 0)\n- Assets/Game/Scenes/Play.unity (Index 1)\n\nMở Lobby.unity và bấm Play để chạy game!",
                "OK");
        }

        [MenuItem("Wolf and Rabbits/Configure Multiplayer & Mobile Settings", priority = 40)]
        public static void ConfigureProjectSettings()
        {
            PlayerSettings.productName = "Wolf and Rabbits 3D";
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 960;
            PlayerSettings.defaultScreenHeight = 540;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            ApplyBuildSettings();

            EditorUtility.DisplayDialog(
                "Wolf and Rabbits 3D",
                "Đã cấu hình:\n- Build Scenes: 0 = Lobby, 1 = Play\n- Run In Background = true & Windowed 960x540 (dễ test nhiều cửa sổ)\n- Màn hình ngang (Landscape) cho Android / iOS",
                "OK");
        }

        private static void ApplyBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyScenePath, true),
                new EditorBuildSettingsScene(PlayScenePath, true)
            };
        }

        private static void BuildLobbyScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSharedLightingAndCamera(out Camera cam, out _, out _);

            GameObject sessionGo = new GameObject("GameSession");
            GameSession session = sessionGo.AddComponent<GameSession>();
            GameWebSocketClient net = sessionGo.AddComponent<GameWebSocketClient>();
            ProceduralAudioManager audio = sessionGo.AddComponent<ProceduralAudioManager>();
            SetSerializedObject(session, "network", net);
            SetSerializedObject(session, "audioManager", audio);

            GameObject dioramaGo = new GameObject("LobbyDiorama");
            MapRenderer3D diorama = dioramaGo.AddComponent<MapRenderer3D>();

            Canvas canvas = CreateScreenCanvas("LobbyCanvas");
            LobbyUiController lobbyUi = canvas.gameObject.AddComponent<LobbyUiController>();

            GameObject ctrlGo = new GameObject("LobbyController");
            LobbySceneController ctrl = ctrlGo.AddComponent<LobbySceneController>();
            SetSerializedObject(ctrl, "lobbyUi", lobbyUi);
            SetSerializedObject(ctrl, "lobbyCanvas", canvas);
            SetSerializedObject(ctrl, "lobbyDiorama", diorama);
            SetSerializedObject(ctrl, "previewCamera", cam);

            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, LobbyScenePath);
        }

        private static void BuildPlayScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateSharedLightingAndCamera(out Camera cam, out _, out _);
            WarcraftCameraController camCtrl = cam.gameObject.AddComponent<WarcraftCameraController>();

            GameObject worldGo = new GameObject("MatchWorld");
            MapRenderer3D map = worldGo.AddComponent<MapRenderer3D>();
            FogOfWarController fog = worldGo.AddComponent<FogOfWarController>();
            MatchEntityManager entities = worldGo.AddComponent<MatchEntityManager>();
            VfxManager3D vfx = worldGo.AddComponent<VfxManager3D>();

            Canvas canvas = CreateScreenCanvas("HudCanvas");
            MatchHudController hud = canvas.gameObject.AddComponent<MatchHudController>();

            GameObject ctrlGo = new GameObject("PlayController");
            PlaySceneController ctrl = ctrlGo.AddComponent<PlaySceneController>();
            PlayerInputController input = ctrlGo.AddComponent<PlayerInputController>();
            BuildPlacementController build = ctrlGo.AddComponent<BuildPlacementController>();

            SetSerializedObject(ctrl, "mapRenderer", map);
            SetSerializedObject(ctrl, "fogController", fog);
            SetSerializedObject(ctrl, "entityManager", entities);
            SetSerializedObject(ctrl, "vfxManager", vfx);
            SetSerializedObject(ctrl, "cameraController", camCtrl);
            SetSerializedObject(ctrl, "inputController", input);
            SetSerializedObject(ctrl, "buildController", build);
            SetSerializedObject(ctrl, "hudCanvas", canvas);
            SetSerializedObject(ctrl, "hud", hud);

            CreateEventSystem();
            EditorSceneManager.SaveScene(scene, PlayScenePath);
        }

        private static void CreateSharedLightingAndCamera(out Camera cam, out Light sun, out Volume vol)
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(22.5f, 14.2f, 12.9f);
            camGo.transform.rotation = Quaternion.Euler(56f, 0f, 0f);
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.11f, 0.14f);
            camGo.AddComponent<AudioListener>();
            UniversalAdditionalCameraData camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.renderShadows = true;

            GameObject sunGo = new GameObject("Directional Light");
            sunGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.92f, 0.76f);
            sun.intensity = 2.15f;
            sun.shadows = LightShadows.Soft;
            sunGo.AddComponent<UniversalAdditionalLightData>();

            GameObject volGo = new GameObject("Global Volume & Atmosphere");
            vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);

            SceneAtmosphereSetup atmo = volGo.AddComponent<SceneAtmosphereSetup>();
            SetSerializedObject(atmo, "directionalSun", sun);
            SetSerializedObject(atmo, "globalVolume", vol);
            SetSerializedObject(atmo, "targetCamera", cam);
        }

        private static Canvas CreateScreenCanvas(string name)
        {
            GameObject go = new GameObject(name);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void CreateEventSystem()
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        private static void SetSerializedObject(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop != null)
            {
                prop.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
#endif
