using Sirenix.Utilities;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TangentMode = UnityEditor.AnimationUtility.TangentMode;

namespace MetaSprite
{
    public class MetaLayerTransform : MetaLayerProcessor
    {
        public override string actionName => "transform";

        public override void Process(ImportContext ctx, Layer layer)
        {
            var animPath = layer.group.path;
            var frames = new Dictionary<int, Vector2>();
            var file = ctx.file;

            //Change this layer sprites pivot
            var processor = ASEImporter.getProcessor("pivot");
            processor?.Process(ctx, layer);

            //Read data from cel
            for (var i = 0; i < file.frames.Count; ++i)
            {
                var center = Vector2.zero;
                var pixelCount = 0;
                file.frames[i].cels.TryGetValue(layer.layerIndex, out var cel);
                if (cel == null) continue;
                for (var y = 0; y < cel.height; ++y)
                {
                    for (var x = 0; x < cel.width; ++x)
                    {
                        var texX = cel.x + x;
                        var texY = -(cel.y + y) + file.height - 1;
                        var col = cel.GetPixelRaw(x, y);
                        if (col.a > 0.1f)
                        {
                            center += new Vector2(texX, texY);
                            pixelCount++;
                        }
                    }
                }

                if (pixelCount > 0)
                {
                    center /= pixelCount;
                    var pivot = Vector2.Scale(ctx.settings.pivotRelativePos, new Vector2(file.width, file.height));
                    var posWorld = (center - pivot) / ctx.settings.ppu;

                    frames.Add(i, posWorld);
                }
            }

            //Change animation clips
            foreach (var frameTag in file.frameTags)
            {
                var clip = ctx.generatedClips[frameTag];

                AnimationCurve curveX = new AnimationCurve(),
                               curveY = new AnimationCurve();

                float t = 0;
                var firstFramePos = frames[frameTag.from];
                for (var f = frameTag.from; f <= frameTag.to; ++f)
                {
                    if (frames.ContainsKey(f))
                    {
                        var pos = frames[f];

                        var currentGroup = layer.group.parent;
                        while (currentGroup != null)
                        {
                            var path = currentGroup.path;
                            var bindings = AnimationUtility.GetCurveBindings(clip);

                            var xb = bindings.Where(it => it.path == path && it.propertyName == "m_LocalPosition.x")
                                .ToList();
                            var yb = bindings.Where(it => it.path == path && it.propertyName == "m_LocalPosition.y")
                                .ToList();
                            if (!(xb.IsNullOrEmpty() || yb.IsNullOrEmpty()))
                            {
                                var x = AnimationUtility.GetEditorCurve(clip, xb[0]).keys
                                    .First(it => Mathf.Approximately(it.time, t)).value;
                                var y = AnimationUtility.GetEditorCurve(clip, yb[0]).keys
                                    .First(it => Mathf.Approximately(it.time, t)).value;
                                pos -= new Vector2(x, y);
                            }
                            currentGroup = currentGroup.parent;
                        }

                        if (f == frameTag.from) firstFramePos = pos;
                        curveX.AddKey(t, pos.x);
                        curveY.AddKey(t, pos.y);
                    }
                    t += file.frames[f].duration * 1e-3f;
                }
                //Completing the end frame for the loop
                //t -= 1.0f / clip.frameRate;
                curveX.AddKey(t, firstFramePos.x);
                curveY.AddKey(t, firstFramePos.y);

                if (curveX.length <= 0) continue;
                MakeConstant(curveX);
                MakeConstant(curveY);

                EditorCurveBinding
                    bindingX = new EditorCurveBinding { path = animPath, type = typeof(Transform), propertyName = "m_LocalPosition.x" },
                    bindingY = new EditorCurveBinding { path = animPath, type = typeof(Transform), propertyName = "m_LocalPosition.y" };
                AnimationUtility.SetEditorCurve(clip, bindingX, curveX);
                AnimationUtility.SetEditorCurve(clip, bindingY, curveY);

                EditorUtility.SetDirty(clip);
            }

            if (ctx.settings.generatePrefab)
            {
                var layerTransform = ctx.name2GameObject[layer.group.name].transform;
                var position = frames[0];
                var transform = layerTransform.parent;
                while (transform.gameObject != ctx.rootGameObject)
                {
                    position -= (Vector2)transform.localPosition;
                    transform = transform.parent;
                }

                layerTransform.transform.localPosition = position;
            }
        }

        private static void MakeConstant(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; ++i)
            {
                AnimationUtility.SetKeyRightTangentMode(curve, i, TangentMode.Constant);
                AnimationUtility.SetKeyLeftTangentMode(curve, i, TangentMode.Constant);
                AnimationUtility.SetKeyBroken(curve, i, true);
            }
        }
    }
}