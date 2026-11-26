using UnityEngine;

namespace WolfAndRabbits.Client.Core
{
    /// <summary>
    /// Centralizes Web / Desktop vs Mobile platform layout and control decisions
    /// from a single shared Unity codebase.
    /// - Android / iOS builds: Mobile layout (Virtual Joystick + SafeArea, touch labels).
    /// - WebGL / Desktop builds: Web/PC layout (No Joystick, keyboard shortcut hints).
    /// - Unity Editor: toggleable via "Wolf and Rabbits -> Preview Mobile UI (Joystick) in Editor".
    /// </summary>
    public static class PlatformProfile
    {
        public const string EditorSimulateMobileKey = "WR_SimulateMobileInEditor";

        public static bool SimulateMobileInEditor
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(EditorSimulateMobileKey, false);
#else
                return false;
#endif
            }
            set
            {
#if UNITY_EDITOR
                UnityEditor.EditorPrefs.SetBool(EditorSimulateMobileKey, value);
#endif
            }
        }

        public static bool ShowVirtualJoystick
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return true;
#elif UNITY_WEBGL && !UNITY_EDITOR
                return false;
#elif UNITY_EDITOR
                return SimulateMobileInEditor;
#else
                return false;
#endif
            }
        }

        public static bool ShowKeyboardHints => !ShowVirtualJoystick;
        public static bool UseSafeArea => ShowVirtualJoystick;

        public static string CurrentModeLabel
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return "Mobile";
#elif UNITY_WEBGL && !UNITY_EDITOR
                return "WebGL";
#elif UNITY_EDITOR
                return SimulateMobileInEditor ? "Editor (Mobile Preview)" : "Editor (Web/PC Preview)";
#else
                return "Desktop";
#endif
            }
        }
    }
}
