using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MetaSprite
{
    public class MetaLayerPivot : MetaLayerProcessor
    {
        public override string actionName => "pivot";

        private struct PivotFrame
        {
            public int frame;
            public Vector2 pivot;
        }

        public override void Process(ImportContext ctx, Layer layer)
        {
            var pivots = new List<PivotFrame>();

            var file = ctx.file;
            var path = Path.Combine(ctx.settings.atlasOutputDirectory, ctx.fileNameNoExt + "_" + layer.group.name + ".png");

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            var spriteSheet = importer.spritesheet;

            //Read data from cel
            for (var i = 0; i < file.frames.Count; ++i)
            {
                file.frames[i].cels.TryGetValue(layer.layerIndex, out var cel);

                if (cel == null) continue;
                var center = Vector2.zero;
                var pixelCount = 0;

                for (var y = 0; y < cel.height; ++y)
                    for (var x = 0; x < cel.width; ++x)
                    {
                        // tex coords relative to full texture boundaries
                        var texX = cel.x + x;
                        var texY = -(cel.y + y) + file.height - 1;

                        var col = cel.GetPixelRaw(x, y);
                        if (col.a > 0.1f)
                        {
                            center += new Vector2(texX, texY);
                            ++pixelCount;
                        }
                    }

                if (pixelCount > 0)
                {
                    center /= pixelCount;
                    pivots.Add(new PivotFrame { frame = i, pivot = center });
                }
            }
            if (pivots.Count == 0) return;

            for (var i = 0; i < spriteSheet.Length; ++i)
            {
                var j = 1;
                while (j < pivots.Count && pivots[j].frame <= i) ++j; // j = layerIndex after found item

                var pivot = pivots[j - 1].pivot;
                ctx.groupIndex2SpriteCropPositions.TryGetValue(layer.group.groupIndex, out var value);
                if (value == null) return;
                pivot -= value[i];
                pivot = Vector2.Scale(pivot, new Vector2(1.0f / spriteSheet[i].rect.width, 1.0f / spriteSheet[i].rect.height));

                spriteSheet[i].pivot = pivot;
            }

            importer.spritesheet = spriteSheet;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}