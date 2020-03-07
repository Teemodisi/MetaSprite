using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System;
using UnityEditor.Animations;

using GL = UnityEngine.GUILayout;
using EGL = UnityEditor.EditorGUILayout;

namespace MetaSprite
{

    public enum AnimControllerOutputPolicy
    {
        Skip, CreateOrOverride
    }

    [CreateAssetMenu(menuName = "ASE Import Settings")]
    public class ImportSettings : ScriptableObject
    {

        public int ppu = 48;

        public SpriteAlignment alignment;

        public Vector2 customPivot;

        public bool densePacked = true;

        public int border = 3;

        public string atlasOutputDirectory = "";

        public string clipOutputDirectory = "";

        public string prefabsDirectory = "";

        public AnimControllerOutputPolicy controllerPolicy;

        public string animControllerOutputPath;

        public bool generatePrefab = false;

        public int orderInLayerInterval = 5;

        public int sortIndex = 0;
        public int spritesSortInLayer = 0;
        public Vector2 PivotRelativePos
        {
            get
            {
                return alignment.GetRelativePos(customPivot);
            }
        }
    }

    [CustomEditor(typeof(ImportSettings))]
    public class ImportSettingsEditor : Editor
    {

        public override void OnInspectorGUI()
        {
            var settings = (ImportSettings)target;

            EditorGUI.BeginChangeCheck();
            Undo.RecordObject(settings, "AseFileSettings");

            using (new GL.HorizontalScope(EditorStyles.toolbar))
            {
                GL.Label("Options");
            }

            settings.generatePrefab = EGL.Toggle("Generate Prefab", settings.generatePrefab);
            settings.ppu = EGL.IntField("Pixel Per Unit", settings.ppu);
            settings.alignment = (SpriteAlignment)EGL.EnumPopup("Default Align", settings.alignment);
            if (settings.alignment == SpriteAlignment.Custom)
            {
                settings.customPivot = EGL.Vector2Field("Custom Pivot", settings.customPivot);
            }

            settings.densePacked = EGL.Toggle("Dense Pack", settings.densePacked);
            settings.border = EGL.IntField("Border", settings.border);

            EGL.Space();
            if (settings.generatePrefab)
            {
                using (new GL.HorizontalScope(EditorStyles.toolbar))
                {
                    GL.Label("Prefab Options");
                }
                EGL.BeginHorizontal();

                string[] opetions = SortingLayer.layers.Select(it => it.name).ToArray();
                settings.sortIndex = EGL.Popup("Sort In Layer", settings.sortIndex, opetions);
                settings.spritesSortInLayer = SortingLayer.NameToID(opetions[settings.sortIndex]);

                if (GL.Button("Edit", GL.Width(36)))
                {
                    Selection.objects = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
                }
                EGL.EndHorizontal();
                settings.orderInLayerInterval = EGL.IntField("Interval of order in layer", settings.orderInLayerInterval);
            }

            EGL.Space();
            using (new GL.HorizontalScope(EditorStyles.toolbar))
            {
                GL.Label("Output");
            }

            settings.atlasOutputDirectory = PathSelection("Atlas Directory", settings.atlasOutputDirectory);
            settings.clipOutputDirectory = PathSelection("Anim Clip Directory", settings.clipOutputDirectory);

            settings.controllerPolicy = (AnimControllerOutputPolicy)EGL.EnumPopup("Anim Controller Policy", settings.controllerPolicy);
            if (settings.controllerPolicy == AnimControllerOutputPolicy.CreateOrOverride)
            {
                settings.animControllerOutputPath = PathSelection("Anim Controller Directory", settings.animControllerOutputPath);
            }

            if (settings.generatePrefab)
            {
                settings.prefabsDirectory = PathSelection("Prefab Directory", settings.prefabsDirectory);
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(settings);
            }
        }

        string PathSelection(string id, string path)
        {
            EGL.BeginHorizontal();
            EGL.PrefixLabel(id);
            path = EGL.TextField(path);
            if (GL.Button("...", GL.Width(30)))
            {
                path = GetAssetPath(EditorUtility.OpenFolderPanel("Select path", path, ""));
            }

            EGL.EndHorizontal();
            return path;
        }

        static string GetAssetPath(string path)
        {
            if (path == null)
            {
                return null;
            }

            var projectPath = Application.dataPath;
            projectPath = projectPath.Substring(0, projectPath.Length - "/Assets".Length);
            path = Remove(path, projectPath);

            if (path.StartsWith("\\") || path.StartsWith("/"))
            {
                path = path.Remove(0, 1);
            }

            if (!path.StartsWith("Assets") && !path.StartsWith("/Assets"))
            {
                path = Path.Combine("Assets", path);
            }

            path.Replace('\\', '/');

            return path;
        }

        static string Remove(string s, string exactExpression)
        {
            return s.Replace(exactExpression, "");
        }

    }

}