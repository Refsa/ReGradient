using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Experimental.AssetImporters;

namespace Refsa.ReGradient
{
    [ScriptedImporter(1, "regradient")]
    public class ReGradientAsset : ScriptedImporter
    {
        [SerializeField] ReGradientDataProxy proxy;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            string contents = System.IO.File.ReadAllText(ctx.assetPath);

            var data = JsonUtility.FromJson<ReGradientData>(contents);
            proxy = new ReGradientDataProxy{data = data, path = ctx.assetPath};

            ctx.AddObjectToAsset("ReGradientAsset", proxy);
            ctx.SetMainObject(proxy);
        }
    }
}