using System;
using UnityEngine;

namespace WolfAndRabbits.Client.Core
{
    public enum BuildMode
    {
        Development,
        Release
    }

    [Serializable]
    public class ServerConfigData
    {
        public string devHost = "localhost";
        public int devPort = 5000;
        public bool devUseTls = false;

        public string releaseHost = "localhost";
        public int releasePort = 80;
        public bool releaseUseTls = false;

        public string wsPath = "/ws";
    }

    /// <summary>
    /// Project-wide Development vs Release environment flag.
    /// - Development (APP_DEVELOPMENT / Editor default / Development Build): port 5000.
    /// - Release (APP_RELEASE / non-development production build): port 80.
    /// Switch in Unity Editor via: "Wolf and Rabbits -> Build Flag -> Development / Release".
    /// Edit VPS host/domain for Release in: Assets/Game/Resources/server_config.json.
    /// </summary>
    public static class BuildEnvironment
    {
        public const string DefineDevelopment = "APP_DEVELOPMENT";
        public const string DefineRelease = "APP_RELEASE";

        public const int DevelopmentPort = 5000;
        public const int ReleasePort = 80;

        private static ServerConfigData _cachedConfig;

        public static BuildMode CurrentMode
        {
            get
            {
#if APP_RELEASE
                return BuildMode.Release;
#elif APP_DEVELOPMENT || DEVELOPMENT_BUILD || UNITY_EDITOR
                return BuildMode.Development;
#else
                return BuildMode.Release;
#endif
            }
        }

        public static bool IsDevelopment => CurrentMode == BuildMode.Development;
        public static bool IsRelease => CurrentMode == BuildMode.Release;
        public static int DefaultPort => IsRelease ? ReleasePort : DevelopmentPort;

        public static string PlayerPrefsServerUrlKey =>
            IsRelease ? "WR_ServerUrl_Release" : "WR_ServerUrl_Development";

        public static string BadgeText =>
            IsRelease
                ? "<color=#FFD54F>[RELEASE :" + GetConfiguredPort() + "]</color>"
                : "<color=#70FF9A>[DEV :" + GetConfiguredPort() + "]</color>";

        public static ServerConfigData Config
        {
            get
            {
                if (_cachedConfig == null)
                    _cachedConfig = LoadConfig();
                return _cachedConfig;
            }
        }

        public static int GetConfiguredPort()
        {
            ServerConfigData cfg = Config;
            return IsRelease ? cfg.releasePort : cfg.devPort;
        }

        public static string GetDefaultServerUrl()
        {
            ServerConfigData cfg = Config;
            string path = string.IsNullOrWhiteSpace(cfg.wsPath) ? "/ws" : cfg.wsPath.Trim();
            if (!path.StartsWith("/")) path = "/" + path;

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                Uri uri = new Uri(Application.absoluteURL);
                bool isHttps = string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase);
                string scheme = isHttps ? "wss" : "ws";

                if (IsRelease)
                {
                    // In Release on WebGL, connect to the exact same host & port (80 for http, 443 for https)
                    return uri.IsDefaultPort
                        ? scheme + "://" + uri.Host + path
                        : scheme + "://" + uri.Host + ":" + uri.Port + path;
                }

                // In Development on WebGL, prefer configured devPort (5000) unless page explicitly runs on another non-default port
                int devPort = uri.IsDefaultPort ? cfg.devPort : uri.Port;
                return FormatWsUrl(scheme, uri.Host, devPort, path);
            }
            catch
            {
                return BuildFallbackUrl(cfg, path);
            }
#else
            string fallback = BuildFallbackUrl(cfg, path);
            string saved = PlayerPrefs.GetString(PlayerPrefsServerUrlKey, string.Empty);
            return string.IsNullOrWhiteSpace(saved) ? fallback : saved;
#endif
        }

        public static void SaveServerUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            PlayerPrefs.SetString(PlayerPrefsServerUrlKey, url.Trim());
        }

        private static string BuildFallbackUrl(ServerConfigData cfg, string path)
        {
            bool useTls = IsRelease ? cfg.releaseUseTls : cfg.devUseTls;
            string host = IsRelease ? cfg.releaseHost : cfg.devHost;
            int port = IsRelease ? cfg.releasePort : cfg.devPort;
            if (string.IsNullOrWhiteSpace(host)) host = "localhost";
            string scheme = useTls ? "wss" : "ws";
            return FormatWsUrl(scheme, host.Trim(), port, path);
        }

        private static string FormatWsUrl(string scheme, string host, int port, string path)
        {
            bool isStandardPort = (scheme == "ws" && port == 80) || (scheme == "wss" && port == 443);
            return isStandardPort
                ? scheme + "://" + host + path
                : scheme + "://" + host + ":" + port + path;
        }

        private static ServerConfigData LoadConfig()
        {
            TextAsset asset = Resources.Load<TextAsset>("server_config");
            if (asset != null && !string.IsNullOrWhiteSpace(asset.text))
            {
                try
                {
                    ServerConfigData parsed = JsonUtility.FromJson<ServerConfigData>(asset.text);
                    if (parsed != null)
                        return parsed;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[WolfAndRabbits] Failed to parse server_config.json, using defaults: " + ex.Message);
                }
            }
            return new ServerConfigData();
        }
    }
}
