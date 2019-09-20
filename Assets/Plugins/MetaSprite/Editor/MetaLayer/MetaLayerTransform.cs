using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using TangentMode = UnityEditor.AnimationUtility.TangentMode;

namespace MetaSprite
{

    public class MetaLayerTransform : MetaLayerProcessor
    {

        public override string actionName
        {
            get { return "transform"; }
        }

        public override void Process(ImportContext ctx, Layer layer)
        {
            var childName = layer.group.Path;

            EditorCurveBinding
                bindingX = new EditorCurveBinding { path = childName, type = typeof(Transform), propertyName = "m_LocalPosition.x" },
                bindingY = new EditorCurveBinding { path = childName, type = typeof(Transform), propertyName = "m_LocalPosition.y" };

            var frames = new Dictionary<int, Vector2>();
            var file = ctx.file;

            bool bHaveTargetTransform = false;
            if (layer.group.HaveTarget)
            {
                var arr = layer.group.parent.metaLayers.Where(it => it.actionName == actionName).ToArray();
                if (arr.Length == 1)
                {
                    bHaveTargetTransform = true;
                }
            }

            for (int i = 0; i < file.frames.Count; ++i)
            {
                Vector2 center = Vector2.zero;
                int pixelCount = 0;

                Cel cel;
                file.frames[i].cels.TryGetValue(layer.index, out cel);

                if (cel == null)
                    continue;

                for (int y = 0; y < cel.height; ++y)
                {
                    for (int x = 0; x < cel.width; ++x)
                    {
                        int texX = cel.x + x;
                        int texY = -(cel.y + y) + file.height - 1;
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
                    var pivot = Vector2.Scale(ctx.settings.PivotRelativePos, new Vector2(file.width, file.height));
                    var posWorld = (center - pivot) / ctx.settings.ppu;

                    frames.Add(i, posWorld);
                }
            }

            foreach (var frameTag in file.frameTags)
            {
                var clip = ctx.generatedClips[frameTag];

                AnimationCurve curveX = new AnimationCurve(),
                               curveY = new AnimationCurve();

                float t = 0;
                for (int f = frameTag.from; f <= frameTag.to; ++f)
                {
                    if (frames.ContainsKey(f))
                    {
                        var pos = frames[f];
                        if (bHaveTargetTransform)
                        {
                            var path = layer.group.parent.Path;
                            EditorCurveBinding xb = AnimationUtility.GetCurveBindings(clip).Where(it => it.path == path && it.propertyName == "m_LocalPosition.x").ToArray()[0];
                            EditorCurveBinding yb = AnimationUtility.GetCurveBindings(clip).Where(it => it.path == path && it.propertyName == "m_LocalPosition.y").ToArray()[0];

                            var x = AnimationUtility.GetEditorCurve(clip, xb).keys.Where(it => Mathf.Approximately(it.time, t)).ToArray()[0].value;
                            var y = AnimationUtility.GetEditorCurve(clip, yb).keys.Where(it => Mathf.Approximately(it.time, t)).ToArray()[0].value;

                            pos -= new Vector2(x, y);
                        }
                        curveX.AddKey(t, pos.x);
                        curveY.AddKey(t, pos.y);
                    }

                    t += file.frames[f].duration * 1e-3f;
                }

                if (curveX.length > 0)
                {
                    MakeConstant(curveX);
                    MakeConstant(curveY);

                    AnimationUtility.SetEditorCurve(clip, bindingX, curveX);
                    AnimationUtility.SetEditorCurve(clip, bindingY, curveY);

                    EditorUtility.SetDirty(clip);
                }
            }
            GameObject gameObject = ctx.name2GameObject[layer.group.Name];
            Vector3 position = frames[0];
            gameObject.transform.position= position;
        }


        static void MakeConstant(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; ++i)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, TangentMode.Constant);
            }
        }

    }

}