using System;
using System.Collections.Generic;
using UnityEngine;
using WolfAndRabbits.Client.Protocol;

namespace WolfAndRabbits.Client.Core
{
    public enum GameLanguage
    {
        Vietnamese = 0,
        English = 1
    }

    [Serializable]
    internal class LocalizationEntry
    {
        public string key = "";
        public string value = "";
    }

    [Serializable]
    internal class LocalizationTable
    {
        public LocalizationEntry[] entries = null;
    }

    /// <summary>
    /// File-driven localization manager reading key-value tables from:
    ///   Assets/Game/Resources/Localization/vi.json
    ///   Assets/Game/Resources/Localization/en.json
    /// </summary>
    public static class LocalizationManager
    {
        private const string PrefKey = "WR_Language";

        public static event Action<GameLanguage> OnLanguageChanged;

        private static bool _initialized;
        private static GameLanguage _current = GameLanguage.Vietnamese;
        private static readonly Dictionary<GameLanguage, Dictionary<string, string>> _tables =
            new Dictionary<GameLanguage, Dictionary<string, string>>();

        public static GameLanguage Current
        {
            get
            {
                EnsureInitialized();
                return _current;
            }
        }

        public static bool IsVietnamese => Current == GameLanguage.Vietnamese;
        public static bool IsEnglish => Current == GameLanguage.English;

        public static void SetLanguage(GameLanguage language)
        {
            EnsureInitialized();
            if (_current == language) return;

            _current = language;
            PlayerPrefs.SetInt(PrefKey, (int)_current);
            PlayerPrefs.Save();
            OnLanguageChanged?.Invoke(_current);
        }

        public static string Get(string key)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(key)) return string.Empty;

            if (_tables.TryGetValue(_current, out Dictionary<string, string> map) &&
                map.TryGetValue(key, out string val))
                return val;

            // Fallback to Vietnamese table if missing in current table
            if (_current != GameLanguage.Vietnamese &&
                _tables.TryGetValue(GameLanguage.Vietnamese, out Dictionary<string, string> viMap) &&
                viMap.TryGetValue(key, out string viVal))
                return viVal;

            return key;
        }

        public static string Format(string key, params object[] args)
        {
            string pattern = Get(key);
            try
            {
                return string.Format(pattern, args);
            }
            catch
            {
                return pattern;
            }
        }

        public static bool HasKey(string key)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(key)) return false;
            return (_tables.TryGetValue(_current, out Dictionary<string, string> map) && map.ContainsKey(key)) ||
                   (_tables.TryGetValue(GameLanguage.Vietnamese, out Dictionary<string, string> vi) && vi.ContainsKey(key));
        }

        public static string GetBuildingName(string buildingType)
        {
            switch (buildingType)
            {
                case GameConstants.BuildingGate: return Get("building.gate");
                case GameConstants.BuildingGoldMine: return Get("building.gold_mine");
                case GameConstants.BuildingCannon: return Get("building.cannon");
                default: return buildingType ?? string.Empty;
            }
        }

        public static string GetShopItemDisplayName(ShopItemDto item)
        {
            if (item == null) return string.Empty;
            if (!string.IsNullOrEmpty(item.nameKey) && HasKey(item.nameKey))
                return Get(item.nameKey);
            if (!string.IsNullOrEmpty(item.name))
                return item.name;
            string conventionKey = "shop.item." + item.itemId;
            return HasKey(conventionKey) ? Get(conventionKey) : item.itemId;
        }

        public static string FormatShopStatLine(string statType, float statValue)
        {
            string valStr = Mathf.Approximately(statValue, Mathf.Round(statValue))
                ? Mathf.RoundToInt(statValue).ToString()
                : statValue.ToString("0.#");
            string statKey = "shop.stat." + (statType ?? "attack");
            return HasKey(statKey) ? Format(statKey, valStr) : ("+" + valStr + " " + statType);
        }

        public static string TranslateServerError(string code, string fallbackMessage)
        {
            if (!string.IsNullOrEmpty(code))
            {
                string key = "err." + code;
                if (HasKey(key))
                    return Get(key);
            }
            return fallbackMessage ?? string.Empty;
        }

        public static string TranslateMatchReason(string serverReason)
        {
            if (string.IsNullOrEmpty(serverReason)) return string.Empty;
            if (serverReason.Contains("hạ gục toàn bộ thỏ"))
                return Get("result.reason.wolf_slay_all");
            if (serverReason.Contains("Sói đã bị hạ"))
                return Get("result.reason.wolf_killed");
            if (serverReason.Contains("Hết thời gian"))
                return Get("result.reason.timeout");
            return serverReason;
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            int raw = PlayerPrefs.GetInt(PrefKey, (int)GameLanguage.Vietnamese);
            _current = raw == (int)GameLanguage.English ? GameLanguage.English : GameLanguage.Vietnamese;

            _tables[GameLanguage.Vietnamese] = LoadTable("Localization/vi");
            _tables[GameLanguage.English] = LoadTable("Localization/en");
        }

        private static Dictionary<string, string> LoadTable(string resourcePath)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.Ordinal);
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
            {
                Debug.LogWarning("[LocalizationManager] Missing localization resource: " + resourcePath);
                return dict;
            }

            try
            {
                LocalizationTable table = JsonUtility.FromJson<LocalizationTable>(asset.text);
                if (table != null && table.entries != null)
                {
                    for (int i = 0; i < table.entries.Length; i++)
                    {
                        LocalizationEntry e = table.entries[i];
                        if (e != null && !string.IsNullOrEmpty(e.key))
                            dict[e.key] = e.value ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[LocalizationManager] Failed to parse " + resourcePath + ": " + ex.Message);
            }

            return dict;
        }
    }
}
