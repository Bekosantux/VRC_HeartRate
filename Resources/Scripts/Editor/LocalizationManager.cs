using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using nadena.dev.ndmf.localization;

namespace BekoShop.VRCHeartRate
{
    public static class LocalizationManager
    {
        private const string FallbackLanguage = "en-US";
        private static Dictionary<string, Dictionary<string, string>> _cache = new Dictionary<string, Dictionary<string, string>>();
        private static string _localizationFolderPath;

        /// <summary>
        /// ���̃X�N���v�g���g�̃p�X���瑊�ΓI��Localization�t�H���_�̃p�X���擾
        /// </summary>
        private static string LocalizationFolderPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_localizationFolderPath)) return _localizationFolderPath;

                // ���̃X�N���v�g(LocalizationManager)��GUID��T��
                var guids = AssetDatabase.FindAssets("t:Script LocalizationManager");
                if (guids.Length == 0)
                {
                    Debug.LogError("LocalizationManager script not found. Localization will not work.");
                    return null;
                }

                // GUID����A�Z�b�g�p�X���擾
                var scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                // �X�N���v�g�̐e�f�B���N�g�����擾
                var scriptDirectory = Path.GetDirectoryName(scriptPath);
                // ���ΓI��Localization�t�H���_�̃p�X��g�ݗ��Ă�
                _localizationFolderPath = Path.Combine(scriptDirectory, "Localization").Replace("\\", "/");

                return _localizationFolderPath;
            }
        }

        public static string CurrentLanguage
        {
            get
            {
                var lang = LanguagePrefs.Language;
                if (string.IsNullOrEmpty(lang)) lang = FallbackLanguage;
                return lang;
            }
        }

        /// <summary>
        /// ���[�J���C�Y���͎擾�B�L�[��������Ȃ���΁A�p����t�H�[���o�b�N�B�p����Ȃ���΃L�[����Ԃ��B
        /// </summary>
        public static string S(string key)
        {
            string lang = CurrentLanguage;
            var text = S(key, lang);
            if (text != null) return text;

            text = S(key, FallbackLanguage);
            return text ?? key;
        }

        private static string S(string key, string lang)
        {
            var map = GetLangMap(lang);
            if (map != null && map.TryGetValue(key, out var val)) return val;
            return null;
        }

        private static Dictionary<string, string> GetLangMap(string lang)
        {
            if (_cache.TryGetValue(lang, out var map)) return map;

            var folderPath = LocalizationFolderPath;
            if (string.IsNullOrEmpty(folderPath)) return null;

            var filePath = Path.Combine(folderPath, $"{lang}.json");
            if (!File.Exists(filePath))
            {
                _cache[lang] = null;
                return null;
            }

            try
            {
                var txt = File.ReadAllText(filePath);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(txt);
                _cache[lang] = dict;
                return dict;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load localization for {lang}: {e}");
                _cache[lang] = null;
                return null;
            }
        }
    }
}