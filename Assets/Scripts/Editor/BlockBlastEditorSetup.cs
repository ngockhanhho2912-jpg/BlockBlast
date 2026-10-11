using UnityEditor;
using UnityEngine;

namespace BlockBlast.EditorTools
{
    /// <summary>Imports everything under Resources/Art as crisp UI sprites.</summary>
    public sealed class ArtImporter : AssetPostprocessor
    {
        const string ArtFolder = "Assets/Resources/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }

    /// <summary>One-time project setup: portrait orientation and a reimport of the sliced art.</summary>
    [InitializeOnLoad]
    static class BlockBlastEditorSetup
    {
        const string Key = "BlockBlast.SetupDone.v1";

        static BlockBlastEditorSetup()
        {
            if (SessionState.GetBool(Key, false) || EditorPrefs.GetBool(Key + Application.dataPath, false)) return;
            SessionState.SetBool(Key, true);
            EditorApplication.delayCall += Run;
        }

        [MenuItem("BlockBlast/Apply Project Setup")]
        static void Run()
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            AssetDatabase.ImportAsset("Assets/Resources/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            EditorPrefs.SetBool(Key + Application.dataPath, true);
            Debug.Log("[BlockBlast] Project setup applied (portrait, art reimported). Press Play in any scene.");
        }
    }
}
