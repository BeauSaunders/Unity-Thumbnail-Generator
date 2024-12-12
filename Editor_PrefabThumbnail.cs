using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class Editor_PrefabThumbnail : MonoBehaviour
{
    /// <summary>
    /// HOW TO USE
    /// Right-click the asset you wish to photograph
    /// Select 'Custom Tools>Generate Prefab Thumbnail'
    /// A thumbnail of this option will be placed in the folder of your selected asset
    /// </summary>

    [MenuItem("Assets/Custom Tools/Generate Prefab Thumbnail")]
    public static void GetPrefabThumbnail()
    {
        Object prefab = Selection.activeObject;
        if (prefab == null || !PrefabUtility.IsPartOfPrefabAsset(prefab))
        {
            Debug.LogWarning("Selected object is not a valid prefab.");
            return;
        }

        Texture2D tex = TakeImg(prefab);
        if (tex == null)
        {
            Debug.LogError("Failed to generate thumbnail texture for prefab.");
            return;
        }

        byte[] pngData = tex.EncodeToPNG();
        if (pngData == null)
        {
            Debug.LogError("Failed to encode texture to PNG.");
            return;
        }

        // Define path to save the PNG in the Resources folder
        string prefabName = prefab.name;
        string path = AssetDatabase.GetAssetPath(prefab).Replace(".prefab", "") + "_Thumbnail.png";

        // Write the PNG data to file
        File.WriteAllBytes(path, pngData);
        Debug.Log($"Thumbnail saved to {path}");

        // Refresh the AssetDatabase to make the new file available to Unity
        AssetDatabase.Refresh();

        SetTextureType(path);
    }

    static Texture2D TakeImg(Object prefab)
    {
        // Instantiate the prefab in a temporary isolated layer
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        int originalLayer = instance.layer;
        int isolatedLayer = LayerMask.NameToLayer("ThumbnailLayer");

        // Set layer to all parts of prefab
        CommonFunctions.SetAllChildrenToLayer(instance, isolatedLayer);

        // Set up RenderTexture with transparent background support
        int width = 256;
        int height = 256;
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        renderTexture.useMipMap = false;
        renderTexture.antiAliasing = 1;

        Camera camera = new GameObject("Thumbnail Camera").AddComponent<Camera>();
        camera.backgroundColor = Color.clear;
        camera.clearFlags = CameraClearFlags.SolidColor;

        // Configure the camera to only render the isolated layer
        camera.cullingMask = 1 << isolatedLayer;

        // Position the prefab directly in front of the camera
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        Bounds prefabBounds = instance.GetComponent<Renderer>().bounds;
        Vector3 cameraPosition = prefabBounds.center - Vector3.forward * (prefabBounds.size.magnitude * 1f);
        camera.transform.position = cameraPosition;
        camera.transform.LookAt(prefabBounds.center);

        // Render the prefab to the RenderTexture
        camera.targetTexture = renderTexture;
        camera.Render();

        // Copy the RenderTexture to a readable Texture2D
        RenderTexture.active = renderTexture;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        // Clean up and revert the prefab to its original layer
        CommonFunctions.SetAllChildrenToLayer(instance, originalLayer);
        RenderTexture.active = null;
        camera.targetTexture = null;
        Object.DestroyImmediate(renderTexture);
        Object.DestroyImmediate(camera.gameObject);
        Object.DestroyImmediate(instance);

        return tex;
    }

    static void SetTextureType(string path, TextureImporterType texType = TextureImporterType.Sprite)
    {
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

        importer.textureType = texType;
        AssetDatabase.WriteImportSettingsIfDirty(path);

        AssetDatabase.Refresh();  // Refresh the AssetDatabase to make sure the changes are applied
    }

}
