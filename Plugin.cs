using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using UnityEngine.UI;
using TMProOld;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;


namespace SilksongFontsInject
{
    [BepInPlugin("com.YLTai.SilksongFontsInject", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private string _pluginFolderPath;
        private Texture2D newTex;
        private bool _hasReplaced = false;
        private MethodInfo _internalCreateInstanceMethod = typeof(TextAsset).GetMethod(
                    "Internal_CreateInstance",
                    BindingFlags.NonPublic | BindingFlags.Static
                );
        private bool _isCsvExist = false;
        private static readonly Dictionary<string, string> TextReplacements = new Dictionary<string, string>();

        void ReplaceTex()
        {
            var texAssets = Resources.FindObjectsOfTypeAll<Texture2D>().Where(t => t.name == "chinese_body Atlas").ToArray();
            var target = texAssets[0];

            foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (mat.HasProperty("_MainTex") && mat.mainTexture == target)
                {
                    mat.mainTexture = newTex;
                }
            }


            var json = File.ReadAllText(Path.Combine(_pluginFolderPath, "assets/font.json"));
            var jsonObject = JsonUtility.FromJson<glyList>(json).data;
            foreach (var mb in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (mb.name == "chinese_body")
                {
                    foreach (PropertyDescriptor descriptor in TypeDescriptor.GetProperties(mb))
                    {
                        string name = descriptor.Name;
                        object value = descriptor.GetValue(mb);
                        Logger.LogInfo($"{name}={value}");
                    }

                    mb.fontInfo.AtlasWidth = newTex.width;
                    mb.fontInfo.AtlasHeight = newTex.height;
                    mb.fontInfo.CharacterCount = 0;

                    mb.atlas = newTex;

                    mb.AddFaceInfo(mb.fontInfo);
                    mb.AddGlyphInfo(jsonObject.ToArray());
                    Logger.LogInfo($"{jsonObject.Count}  glyphs loaded");
                    mb.ReadFontDefinition();

                    // foreach (var textComponent in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    // {
                    //     textComponent.ForceMeshUpdate(true);
                    // }
                }
            }
        }

        void ReplaceText(string textAssetName, string text)
        {
            TextAsset textAsset = (TextAsset)Resources.Load($"Languages/{textAssetName}", typeof(TextAsset));
            _internalCreateInstanceMethod.Invoke(null, new object[] { textAsset, text });
        }

        void Translate()
        {
            string translationFilePath = Path.Combine(_pluginFolderPath, "assets/zh-Hans.json");
            Logger.LogInfo($"Loading translation file from {translationFilePath}");
            if (!File.Exists(translationFilePath)) return;

            try
            {
                var jsonContent = File.ReadAllText(translationFilePath, System.Text.Encoding.UTF8);
                var translationData = JsonConvert.DeserializeObject<TranslationData>(jsonContent);

                if (translationData == null || translationData.entries == null)
                {
                    Logger.LogError("Invalid translation file");
                    return;
                }

                foreach (var entry in translationData.entries)
                {
                    if (!string.IsNullOrEmpty(entry.k) && entry.v != null)
                    {
                        ReplaceText(entry.k, entry.v);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to parse translation.json: {ex.ToString()}");
            }
        }

        private void LoadReplacementsFromCSV()
        {
            string filePath = Path.Combine(_pluginFolderPath, "assets/replacements.csv");
            if (!File.Exists(filePath))
            {
                _isCsvExist = false;
                return;
            }
            _isCsvExist = true;
            Logger.LogInfo($"Loading replacements from {filePath}");

            try
            {
                TextReplacements.Clear();

                string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] parts = line.Split(',');

                    if (parts.Length >= 2)
                    {
                        string original = parts[0].Trim();
                        string replacement = parts[1].Trim();

                        if (!string.IsNullOrEmpty(original) && !string.IsNullOrEmpty(replacement))
                        {
                            TextReplacements[original] = replacement;
                        }
                    }
                }
                Logger.LogInfo("Loaded " + TextReplacements.Count + " text replacements.");
            }
            catch (Exception e)
            {
                Logger.LogError("Failed to load csv file: " + e.Message);
            }
        }

        private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            // Logger.LogInfo($"Scene changed from {oldScene.name} to {newScene.name}");

            // Menu_Title
            if (newScene.name == "Menu_Title")
            {
                if (!_hasReplaced)
                {
                    _hasReplaced = true;
                    ReplaceTex();
                    Logger.LogInfo("Replace TMP Font");
                }
                Translate();
            }
        }

        private void Awake()
        {
            // SceneManager.activeSceneChanged += (oldScene, newScene) =>
            // {
            //     Logger.LogInfo($"SCENE CHANGED: '{newScene.name}' (path: {newScene.path})");
            // };
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            Assembly assembly = Assembly.GetExecutingAssembly();
            string dllPath = assembly.Location;
            _pluginFolderPath = Path.GetDirectoryName(dllPath);
            Logger.LogInfo($"{_pluginFolderPath}");

            Translate();

            var tex_path = Path.Combine(_pluginFolderPath, "assets/font.png");
            var bytes = File.ReadAllBytes(tex_path);
            newTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            newTex.LoadImage(bytes);

            LoadReplacementsFromCSV();
            if (_isCsvExist)
            {
                Harmony.CreateAndPatchAll(typeof(TextPatchers));
            }

        }

        void Update()
        {
        }

        [HarmonyPatch]
        internal static class TextPatchers
        {
            [HarmonyPatch(typeof(Text), "text", MethodType.Setter)]
            [HarmonyPrefix]
            public static void TextSetterPrefix(ref string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                foreach (var entry in TextReplacements)
                {
                    if (value.Contains(entry.Key) && !value.Contains(entry.Value)) value = value.Replace(entry.Key, entry.Value);
                }
            }

            [HarmonyPatch(typeof(TMP_Text), "text", MethodType.Setter)]
            [HarmonyPrefix]
            public static void TMP_TextSetterPrefix(ref string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                foreach (var entry in TextReplacements)
                {
                    if (value.Contains(entry.Key) && !value.Contains(entry.Value)) value = value.Replace(entry.Key, entry.Value);
                }
            }
        }
    }

    public class glyList
    {
        public List<TMP_Glyph> data;
    }

    [System.Serializable]
    public class TranslationEntry
    {
        public string k;
        public string v;
    }

    [System.Serializable]
    public class TranslationData
    {
        public List<TranslationEntry> entries;
    }
}