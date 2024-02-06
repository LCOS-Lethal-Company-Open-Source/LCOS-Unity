using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class AssetBundler
{
    [MenuItem("Assets/Create AssetBundles")]
    private static void Run()
    {
        var path = Application.dataPath + "/../AssetBundles";

        try
        {
            BuildPipeline.BuildAssetBundles(path, BuildAssetBundleOptions.None, EditorUserBuildSettings.activeBuildTarget);
        }
        catch (Exception e)
        {
            Debug.LogWarning(e);
        }
    }
}
