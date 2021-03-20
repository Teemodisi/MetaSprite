using MetaSprite.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MetaSprite
{
    public class ImportContext
    {
        public ASEFile file;
        public ImportSettings settings;

        public string fileDirectory;
        public string fileName;
        public string fileNameNoExt;

        public string animControllerPath;
        public string animClipDirectory;
        public string prefabDirectory;

        // The local texture coordinate for bottom-left point of each frame's crop rect, in Unity texture space.
        public Dictionary<int, List<Vector2>> groupIndex2SpriteCropPositions = new Dictionary<int, List<Vector2>>();

        public Dictionary<FrameTag, AnimationClip> generatedClips = new Dictionary<FrameTag, AnimationClip>();

        //所有生成独立图集的都是一个层动画意义上的图层
        public Dictionary<string, List<Sprite>> mapSprite = new Dictionary<string, List<Sprite>>();

        public AnimatorController controller;

        public GameObject rootGameObject;
        public Dictionary<string, GameObject> name2GameObject = new Dictionary<string, GameObject>();
    }

    public static class ASEImporter
    {
        private static readonly Dictionary<string, MetaLayerProcessor> layerProcessors = new Dictionary<string, MetaLayerProcessor>();

        public static MetaLayerProcessor getProcessor(string action)
        {
            layerProcessors.TryGetValue(action, out var processor);
            return processor;
        }

        private enum Stage
        {
            LoadFile,
            GenerateAtlas,
            GenerateClips,
            GenerateController,
            GeneratePrefab,
            InvokeMetaLayerProcessor
        }

        private static float GetProgress(this Stage stage)
        {
            return (float)(int)stage / Enum.GetValues(typeof(Stage)).Length;
        }

        private static string GetDisplayString(this Stage stage)
        {
            return stage.ToString();
        }

        public static void Refresh()
        {
            layerProcessors.Clear();
            var processorTypes = FindAllTypes(typeof(MetaLayerProcessor));
            // Debug.Log("Found " + processorTypes.Length + " layer processor(s).");
            foreach (var type in processorTypes)
            {
                if (type.IsAbstract) continue;
                try
                {
                    var instance = (MetaLayerProcessor)type.GetConstructor(new Type[0]).Invoke(new object[0]);
                    if (layerProcessors.ContainsKey(instance.actionName))
                    {
                        Debug.LogError(string.Format("Duplicate processor with name {0}: {1}", instance.actionName, instance));
                    }
                    else
                    {
                        layerProcessors.Add(instance.actionName, instance);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("Can't instantiate meta processor " + type);
                    Debug.LogException(ex);
                }
            }
        }

        private static Type[] FindAllTypes(Type interfaceType)
        {
            var types = System.Reflection.Assembly.GetExecutingAssembly()
                .GetTypes();
            return types.Where(type => type.IsClass && interfaceType.IsAssignableFrom(type))
                        .ToArray();
        }

        private struct LayerAndProcessor
        {
            public Layer layer;
            public MetaLayerProcessor processor;
        }

        public static void Import(DefaultAsset defaultAsset, ImportSettings settings)
        {
            var path = AssetDatabase.GetAssetPath(defaultAsset);

            var context = new ImportContext
            {
                // file = file,
                settings = settings,
                fileDirectory = Path.GetDirectoryName(path),
                fileName = Path.GetFileName(path),
                fileNameNoExt = Path.GetFileNameWithoutExtension(path)
            };

            try
            {
                ImportStage(context, Stage.LoadFile);
                context.file = ASEParser.Parse(File.ReadAllBytes(path));
                if (settings.controllerPolicy != AnimControllerOutputPolicy.Skip)
                    context.animControllerPath = settings.animControllerOutputPath + "/" + context.fileNameNoExt + ".controller";
                context.animClipDirectory = settings.clipOutputDirectory;
                context.prefabDirectory = Path.Combine(settings.prefabsDirectory, context.fileNameNoExt + "_Origin.prefab");

                // Create paths in advance
                Directory.CreateDirectory(settings.atlasOutputDirectory);
                Directory.CreateDirectory(context.animClipDirectory);
                if (context.animControllerPath != null)
                    Directory.CreateDirectory(Path.GetDirectoryName(context.animControllerPath));
                if (settings.generatePrefab)
                    Directory.CreateDirectory(settings.prefabsDirectory);

                ImportStage(context, Stage.GenerateAtlas);
                foreach (var group in context.file.name2Group.Values)
                {
                    if (group.contentLayers.Count == 0) continue;
                    string atlasPath = Path.Combine(settings.atlasOutputDirectory, context.fileNameNoExt + "_" + group.name + ".png");
                    var sprites = AtlasGenerator.GenerateAtlas(context, group.contentLayers, atlasPath);
                    context.mapSprite.Add(group.name, sprites);
                }

                ImportStage(context, Stage.GenerateClips);
                GenerateAnimClips(context);

                ImportStage(context, Stage.GenerateController);
                GenerateAnimController(context);

                ImportStage(context, Stage.GeneratePrefab);
                GeneratePrefab(context);

                ImportStage(context, Stage.InvokeMetaLayerProcessor);
                MetaProcess(context);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.ClearProgressBar();
            }

            ImportEnd(context);
        }

        private static void ImportStage(ImportContext ctx, Stage stage)
        {
            EditorUtility.DisplayProgressBar("Importing " + ctx.fileName, stage.GetDisplayString(), stage.GetProgress());
        }

        private static void ImportEnd(ImportContext ctx)
        {
            if (ctx.settings.generatePrefab)
            {
                //Clean gameobject
                PrefabUtility.SaveAsPrefabAssetAndConnect(ctx.rootGameObject, ctx.prefabDirectory, InteractionMode.UserAction);
                UnityEngine.Object.DestroyImmediate(ctx.rootGameObject);
            }
            EditorUtility.ClearProgressBar();
        }

        public static void GenerateClipImageLayer(ImportContext ctx, string childPath, System.Collections.Generic.List<Sprite> frameSprites)
        {
            foreach (var tag in ctx.file.frameTags)
            {
                AnimationClip clip = ctx.generatedClips[tag];

                int time = 0;
                var keyFrames = new ObjectReferenceKeyframe[tag.to - tag.from + 2];
                for (int i = tag.from; i <= tag.to; ++i)
                {
                    var aseFrame = ctx.file.frames[i];
                    keyFrames[i - tag.from] = new ObjectReferenceKeyframe
                    {
                        time = time * 1e-3f,
                        value = frameSprites[aseFrame.frameID]
                    };

                    time += aseFrame.duration;
                }

                keyFrames[keyFrames.Length - 1] = new ObjectReferenceKeyframe
                {
                    time = time * 1e-3f - 1.0f / clip.frameRate,
                    value = frameSprites[tag.to]
                };

                var binding = new EditorCurveBinding
                {
                    path = childPath,
                    type = typeof(SpriteRenderer),
                    propertyName = "m_Sprite"
                };

                AnimationUtility.SetObjectReferenceCurve(clip, binding, keyFrames);
            }
        }

        private static void GenerateAnimClips(ImportContext ctx)
        {
            Directory.CreateDirectory(ctx.animClipDirectory);
            var fileNamePrefix = ctx.animClipDirectory + '/' + ctx.fileNameNoExt;

            // Generate one animation for each tag
            foreach (var tag in ctx.file.frameTags)
            {
                var clipPath = fileNamePrefix + '_' + tag.name + ".anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

                // Create clip
                if (!clip)
                {
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                else
                {
                    AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
                }

                // Set loop property
                var loop = tag.properties.Contains("loop");
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (loop)
                {
                    clip.wrapMode = WrapMode.Loop;
                    settings.loopBlend = true;
                    settings.loopTime = true;
                }
                else
                {
                    clip.wrapMode = WrapMode.Clamp;
                    settings.loopBlend = false;
                    settings.loopTime = false;
                }
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                EditorUtility.SetDirty(clip);
                ctx.generatedClips.Add(tag, clip);
            }

            // Generate main image
            foreach (var group in ctx.file.name2Group.Values)
            {
                if (group.contentLayers.Count == 0) continue;
                GenerateClipImageLayer(ctx, group.path, ctx.mapSprite[group.name]);
            }
        }

        private static void GenerateAnimController(ImportContext ctx)
        {
            if (ctx.settings.controllerPolicy == AnimControllerOutputPolicy.Skip)
            {
                return;
            }

            ctx.controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctx.animControllerPath);
            if (!ctx.controller)
            {
                ctx.controller = AnimatorController.CreateAnimatorControllerAtPath(ctx.animControllerPath);
            }
            else if (ctx.settings.controllerPolicy == AnimControllerOutputPolicy.CreateNotOverride)
            {
                return;
            }

            var layer = ctx.controller.layers[0];
            var stateMap = new Dictionary<string, AnimatorState>();
            PopulateStateTable(stateMap, layer.stateMachine);

            foreach (var pair in ctx.generatedClips)
            {
                var frameTag = pair.Key;
                var clip = pair.Value;

                AnimatorState st;
                stateMap.TryGetValue(frameTag.name, out st);
                if (!st)
                {
                    st = layer.stateMachine.AddState(frameTag.name);
                }

                st.motion = clip;
            }

            EditorUtility.SetDirty(ctx.controller);
        }

        private static void GeneratePrefab(ImportContext ctx)
        {
            ctx.rootGameObject = new GameObject(ctx.fileNameNoExt);
            ctx.name2GameObject.Add(ctx.fileNameNoExt, ctx.rootGameObject);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctx.animControllerPath);
            ctx.rootGameObject.AddComponent<Animator>().runtimeAnimatorController = controller;

            var name2Group = ctx.file.name2Group;
            foreach (var group in name2Group.Values)
            {
                var gameObject = new GameObject(group.name);
                if (group.name == "Sprites")
                {
                    gameObject.transform.parent = ctx.rootGameObject.transform;
                }
                else
                {
                    var father = ctx.name2GameObject[group.parent.name];
                    gameObject.transform.parent = father.transform;
                }
                ctx.name2GameObject.Add(group.name, gameObject);

                if (group.contentLayers.Count != 0)
                {
                    var sr = gameObject.AddComponent<SpriteRenderer>();
                    sr.sprite = ctx.mapSprite[group.name][0];
                    //uncomment these codes, you can see how it sort in 3D view
                    //var a = gameObject.transform.position;
                    //a.z = -group.layerIndex * 0.01f;
                    //gameObject.transform.position = a;
                    sr.sortingOrder = group.contentLayers.Min(layer => layer.layerIndex);
                    sr.sortingLayerID = ctx.settings.spritesSortInLayer;
                }
            }
        }

        private static void MetaProcess(ImportContext ctx)
        {
            ctx.file.metaLayers.Values.Reverse()
                .Select(layer =>
                {
                    layerProcessors.TryGetValue(layer.actionName, out var processor);
                    return new LayerAndProcessor { layer = layer, processor = processor };
                })
                .OrderBy(it => it.processor?.executionOrder ?? 0)
                .ToList()
                .ForEach(it =>
                {
                    var layer = it.layer;
                    var processor = it.processor;
                    if (processor != null)
                    {
                        processor.Process(ctx, layer);
                    }
                    else
                    {
                        Debug.LogWarning(string.Format("No processor for meta layer {0}", layer.layerName));
                    }
                });
        }

        private static void PopulateStateTable(Dictionary<string, AnimatorState> table, AnimatorStateMachine machine)
        {
            foreach (var state in machine.states)
            {
                var name = state.state.name;
                if (table.ContainsKey(name))
                {
                    Debug.LogWarning("Duplicate state with name " + name + " in animator controller. Behaviour is undefined.");
                }
                else
                {
                    table.Add(name, state.state);
                }
            }

            foreach (var subMachine in machine.stateMachines)
            {
                PopulateStateTable(table, subMachine.stateMachine);
            }
        }
    }
}