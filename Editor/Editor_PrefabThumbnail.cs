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

    [MenuItem("Assets/Stow Studios/Tools/Generate Prefab Thumbnail")]
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
        GameObject obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        int originalLayer = obj.layer;
        CreateLayerIfNotExists("ThumbnailLayer");
        int isolatedLayer = LayerMask.NameToLayer("ThumbnailLayer");

        // Set layer to all parts of prefab
        SetAllChildrenToLayer(obj, isolatedLayer);

        // Set up RenderTexture with transparent background support
        int width = 512;
        int height = 512;
        RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        renderTexture.useMipMap = false;
        renderTexture.antiAliasing = 8;

        Camera camera = new GameObject("Thumbnail Camera").AddComponent<Camera>();
        camera.backgroundColor = Color.clear;
        camera.clearFlags = CameraClearFlags.SolidColor;

        // Configure the camera to only render the isolated layer
        camera.cullingMask = 1 << isolatedLayer;

        // Position the prefab directly in front of the camera
        obj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, -90, 0));

        Bounds prefabBounds = CalculatePrefabBounds(obj);

        Vector3 cameraPosition = prefabBounds.center - Vector3.forward * (prefabBounds.size.magnitude * 1f);
        camera.transform.position = cameraPosition;
        camera.orthographic = true;
        camera.orthographicSize = prefabBounds.extents.magnitude * 1.1f;
        camera.transform.position = prefabBounds.center - Vector3.forward * 10f;

        // ambient lighting
        var originAmbMode = RenderSettings.ambientMode;
        var originAmbLight = RenderSettings.ambientLight;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white * 0.7f;

        // Render the prefab to the RenderTexture
        camera.targetTexture = renderTexture;
        camera.Render();

        // Copy the RenderTexture to a readable Texture2D
        RenderTexture.active = renderTexture;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        // Clean up and revert the prefab to its original layer
        SetAllChildrenToLayer(obj, originalLayer);
        RenderTexture.active = null;
        camera.targetTexture = null;
        DestroyImmediate(renderTexture);
        DestroyImmediate(camera.gameObject);
        DestroyImmediate(obj);

        RenderSettings.ambientMode = originAmbMode;
        RenderSettings.ambientLight = originAmbLight;

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


    #region TOOLS

    static void SetAllChildrenToLayer(GameObject obj, int targetLayer)
    {
        foreach (Transform t in obj.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = targetLayer;
        }
    }

    static void CreateLayerIfNotExists(string layerName)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layersProp = tagManager.FindProperty("layers");

        bool layerExists = false;

        for (int i = 8; i < layersProp.arraySize; i++) // Layers 0–7 are reserved
        {
            SerializedProperty layer = layersProp.GetArrayElementAtIndex(i);
            if (layer != null && layer.stringValue == layerName)
            {
                layerExists = true;
                break;
            }
        }

        if (!layerExists)
        {
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty layer = layersProp.GetArrayElementAtIndex(i);
                if (layer != null && string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = layerName;
                    Debug.Log($"Created new layer: {layerName}");
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }

            Debug.LogWarning("No available layer slots to add new layer: " + layerName);
        }
        else
        {
            Debug.Log($"Layer already exists: {layerName}");
        }
    }

    static Bounds CalculatePrefabBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogError("No renderers found on prefab.");
            return default;
        }

        Bounds prefabBounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            prefabBounds.Encapsulate(r.bounds);
        }

        return prefabBounds;
    }

    #endregion

}
