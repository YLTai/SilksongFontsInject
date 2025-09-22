using BepInEx;
using UnityEngine;
using System.IO;
using System.Linq;
using TMProOld;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEngine.SceneManagement;

namespace SilksongFontsInject
{
    [BepInPlugin("com.YLTai.SilksongFontsInject", PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private string _pluginFolderPath;
        private Texture2D newTex;
        private bool _hasReplaced = false;

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

            var tex_path = Path.Combine(_pluginFolderPath, "assets/font.png");
            var bytes = File.ReadAllBytes(tex_path);
            newTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            newTex.LoadImage(bytes);
        }
        void Update()
        {
            // var key = new BepInEx.Configuration.KeyboardShortcut(KeyCode.F9);

            // if (key.IsDown())
            // {
            //     ReplaceTex();
            // }
        }
    }

    public class glyList
    {
        public List<TMP_Glyph> data;
    }
}