using Sirenix.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.NRefactory.Ast;
using UnityEditor;
using UnityEngine;
using EGL = UnityEditor.EditorGUILayout;
using GL = UnityEngine.GUILayout;

namespace MetaSprite
{
    public static class ImportMenu
    {
        [MenuItem("Assets/Aseprite/Import", priority = 60)]
        private static void MenuClicked()
        {
            ASEImporter.Refresh();
            DoImport(GetSelectedAseprites());
        }

        [MenuItem("Assets/Aseprite/Import", true)]
        private static bool ValidateMenu()
        {
            return GetSelectedAseprites().Count() > 0;
        }

        [MenuItem("Assets/Aseprite/File Settings", priority = 60)]
        private static void EditAssetSettings()
        {
            var size = new Vector2(Screen.width, Screen.height);
            var rect = new Rect(size.x / 2, size.y / 2, 250, 200);
            var window = ScriptableObject.CreateInstance<InspectSettingsWindow>();
            window.position = rect;
            window._Init(GetSelectedAseprites());
            window.ShowPopup();
        }

        [MenuItem("Assets/Aseprite/File Settings", true)]
        private static bool ValidateEditAssetSettings()
        {
            return GetSelectedAseprites().Length == 1;
        }

        [MenuItem("Assets/Aseprite/Clear File Settings", priority = 60)]
        private static void ClearAssetSettings()
        {
            GetSelectedAseprites()
                .Select(AssetDatabase.GetAssetPath)
                .ToList()
                .ForEach(it =>
                {
                    var import = AssetImporter.GetAtPath(it);
                    import.userData = "";
                    import.SaveAndReimport();
                });
        }

        [MenuItem("Assets/Aseprite/Clear File Settings", true)]
        private static bool ValidateClearFileSettings()
        {
            return GetSelectedAseprites().Length > 0;
        }

        private static void DoImport(DefaultAsset[] assets)
        {
            foreach (var asset in assets)
            {
                var metaData = GetMetaData(asset);
                if (metaData == null)
                {
                    CreateMetaDataThenImport(assets);
                    return;
                }

                var guid = metaData.metaSpriteSettingsGuid;
                var settings = AssetDatabase.LoadAssetAtPath<ImportSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (settings != null)
                {
                    ASEImporter.Import(asset, settings);
                    return;
                }
                Debug.LogWarning("File " + asset.name + " has empty import metaSpriteImporterSettings, it is ignored.");
            }
        }

        private static DefaultAsset[] GetSelectedAseprites()
        {
            return Selection.GetFiltered<DefaultAsset>(SelectionMode.DeepAssets)
                            .Where(it =>
                            {
                                var path = AssetDatabase.GetAssetPath(it);
                                return path.EndsWith(".ase") || path.EndsWith(".aseprite");
                            })
                            .ToArray();
        }

        private static MetaSpriteImportData GetMetaData(DefaultAsset asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            var import = AssetImporter.GetAtPath(path);
            return JsonUtility.FromJson<MetaSpriteImportData>(import.userData);
        }

        private static string GetSettingsPath(DefaultAsset asset)
        {
            var data = GetMetaData(asset);
            return data == null ? null : AssetDatabase.GUIDToAssetPath(data.metaSpriteSettingsGuid);
        }

        private static void CreateMetaDataThenImport(DefaultAsset[] assets)
        {
            var size = new Vector2(Screen.width, Screen.height);
            var rect = new Rect(size.x / 2, size.y / 2, 250, 200);
            var window = ScriptableObject.CreateInstance<CreateSettingsWindow>();
            window.position = rect;

            var paths = assets.Select(AssetDatabase.GetAssetPath).ToList();

            window._Init(paths, settings =>
            {
                foreach (var asset in assets)
                {
                    ASEImporter.Import(asset, settings);
                }
            });

            window.ShowPopup();
        }

        private class InspectSettingsWindow : EditorWindow
        {
            private Editor m_Editor;
            private readonly List<string> m_Paths = new List<string>();
            private ImportSettingsReference m_Reference;

            public void _Init(DefaultAsset[] selectedAssets)
            {
                selectedAssets.ForEach(x => m_Paths.Add(AssetDatabase.GetAssetPath(x)));
                var asepritesPath = m_Paths[0];
                var import = AssetImporter.GetAtPath(asepritesPath);
                var importData = JsonUtility.FromJson<MetaSpriteImportData>(import.userData);
                m_Reference = CreateInstance<ImportSettingsReference>();
                if (importData != null)
                {
                    var settingsPath = AssetDatabase.GUIDToAssetPath(importData.metaSpriteSettingsGuid);
                    m_Reference.ImporterSettings = AssetDatabase.LoadAssetAtPath<ImportSettings>(settingsPath);
                }
                m_Editor = Editor.CreateEditor(m_Reference);
                AssetDatabase.SaveAssets();
            }

            private void OnGUI()
            {
                m_Editor.OnInspectorGUI();
                if (CenteredButton("Close"))
                {
                    var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m_Reference.ImporterSettings));
                    var data = new MetaSpriteImportData
                    {
                        metaSpriteSettingsGuid = guid
                    };
                    m_Paths.ForEach(x =>
                    {
                        var import = AssetImporter.GetAtPath(x);
                        import.userData = JsonUtility.ToJson(data);
                        import.SaveAndReimport();
                    });
                    this.Close();
                }
            }
        }

        private class CreateSettingsWindow : EditorWindow
        {
            private List<string> assetPaths;
            private Action<ImportSettings> finishedAction;

            private ImportSettings settings;

            public CreateSettingsWindow()
            {
            }

            internal void _Init(List<string> _assetPaths, Action<ImportSettings> _finishedAction)
            {
                this.assetPaths = _assetPaths;
                this.finishedAction = _finishedAction;
            }

            private void OnGUI()
            {
                EGL.LabelField("Use Settings");
                settings = EGL.ObjectField(settings, typeof(ImportSettings), false) as ImportSettings;

                EGL.Space();

                if (settings && CenteredButton("OK"))
                {
                    var reference = new MetaSpriteImportData
                    {
                        metaSpriteSettingsGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(settings))
                    };
                    foreach (var path in assetPaths)
                    {
                        var import = AssetImporter.GetAtPath(path);
                        import.userData = JsonUtility.ToJson(reference);
                        import.SaveAndReimport();
                    }

                    finishedAction(settings);
                    this.Close();
                }

                if (CenteredButton("Cancel"))
                {
                    this.Close();
                }
            }
        }

        private static bool CenteredButton(string content)
        {
            EGL.BeginHorizontal();
            GL.FlexibleSpace();
            var res = GL.Button(content, GL.Width(150));
            GL.FlexibleSpace();
            EGL.EndHorizontal();
            return res;
        }
    }
}