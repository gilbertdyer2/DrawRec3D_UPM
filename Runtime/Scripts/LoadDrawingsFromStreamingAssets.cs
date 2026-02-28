using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace GilbertDyer.DrawRec3D
{
    /// <summary>
    /// Scans StreamingAssets/DrawRec3D/RuntimeDrawings for JSON drawing files and builds a dictionary of name -> points on Start.
    /// Uses LoadDrawingFromFile for parsing; on Android loads via UnityWebRequest (no direct file access in APK).
    /// </summary>
    public class LoadDrawingsFromStreamingAssets : MonoBehaviour
    {
        public const string StreamingSubpath = "DrawRec3D/RuntimeDrawings";

        /// <summary>
        /// Dictionary of drawing name (filename without extension) to list of Vector3 points. Populated after Start (sync on standalone, async on Android).
        /// </summary>
        public IReadOnlyDictionary<string, List<Vector3>> Drawings => _drawings;
        private readonly Dictionary<string, List<Vector3>> _drawings = new Dictionary<string, List<Vector3>>();

        [Tooltip("If true, points are processed with DrawingPreprocessing.SetFirstAsOrigin before storing (matches DrawingJSONViewer pipeline).")]
        public bool setFirstAsOrigin = true;

        [Tooltip("When true, loading runs as a coroutine and isReady is set when done. When false, Start blocks until loaded (not recommended on Android).")]
        public bool loadAsync = true;

        /// <summary>
        /// True when the dictionary has been populated (immediately on standalone; after coroutine completes on Android).
        /// </summary>
        public bool isReady { get; private set; }

        private void Start()
        {
            if (loadAsync)
                StartCoroutine(LoadAllDrawingsCoroutine());
            else
                LoadAllDrawingsSync();
        }

        private void LoadAllDrawingsSync()
        {
            _drawings.Clear();
    #if UNITY_ANDROID && !UNITY_EDITOR
            Debug.LogWarning("LoadDrawingsFromStreamingAssets: Sync load on Android not supported; use loadAsync = true.");
            isReady = true;
            return;
    #else
            string folder = Path.Combine(Application.streamingAssetsPath, StreamingSubpath);
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning("LoadDrawingsFromStreamingAssets: Folder not found: " + folder);
                isReady = true;
                return;
            }
            string[] files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            foreach (string path in files)
            {
                if (!LoadDrawingFromFile.LoadDrawing(path, out List<Vector3> points))
                    continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (setFirstAsOrigin)
                    points = DrawingPreprocessing.SetFirstAsOrigin(points);
                _drawings[name] = points;
            }
            isReady = true;
            Debug.Log("LoadDrawingsFromStreamingAssets: Loaded " + _drawings.Count + " drawing(s) from " + folder);
    #endif
        }

        private IEnumerator LoadAllDrawingsCoroutine()
        {
            _drawings.Clear();
            isReady = false;

    #if UNITY_ANDROID && !UNITY_EDITOR
            List<string> jsonNames = ListJsonFilesInStreamingFolder(StreamingSubpath);
            string baseUrl = Application.streamingAssetsPath + "/" + StreamingSubpath.Replace('\\', '/').TrimEnd('/') + "/";
            for (int i = 0; i < jsonNames.Count; i++)
            {
                string filename = jsonNames[i];
                string url = baseUrl + filename;
                using (UnityWebRequest req = UnityWebRequest.Get(url))
                {
                    req.SendWebRequest();
                    while (!req.isDone)
                        yield return null;

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning("LoadDrawingsFromStreamingAssets: Failed to load " + url + ": " + req.error);
                        continue;
                    }
                    string json = req.downloadHandler?.text;
                    if (string.IsNullOrEmpty(json)) continue;

                    if (!LoadDrawingFromFile.LoadDrawingFromJson(json, out List<Vector3> points))
                        continue;
                    string name = Path.GetFileNameWithoutExtension(filename);
                    if (setFirstAsOrigin)
                        points = DrawingPreprocessing.SetFirstAsOrigin(points);
                    _drawings[name] = points;
                }
            }
            Debug.Log("LoadDrawingsFromStreamingAssets: Loaded " + _drawings.Count + " drawing(s) from StreamingAssets/" + StreamingSubpath);
    #else
            string folder = Path.Combine(Application.streamingAssetsPath, StreamingSubpath);
            if (!Directory.Exists(folder))
            {
                Debug.LogWarning("LoadDrawingsFromStreamingAssets: Folder not found: " + folder);
                isReady = true;
                yield break;
            }
            string[] files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            foreach (string path in files)
            {
                if (!LoadDrawingFromFile.LoadDrawing(path, out List<Vector3> points))
                    continue;
                string name = Path.GetFileNameWithoutExtension(path);
                if (setFirstAsOrigin)
                    points = DrawingPreprocessing.SetFirstAsOrigin(points);
                _drawings[name] = points;
            }
            Debug.Log("LoadDrawingsFromStreamingAssets: Loaded " + _drawings.Count + " drawing(s) from " + folder);
    #endif
            isReady = true;
        }

    #if UNITY_ANDROID && !UNITY_EDITOR
        private static List<string> ListJsonFilesInStreamingFolder(string assetPath)
        {
            var result = new List<string>();
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var assets = activity.Call<AndroidJavaObject>("getAssets"))
                {
                    string[] names = assets.Call<string[]>("list", assetPath);
                    if (names == null) return result;
                    foreach (string name in names)
                    {
                        if (string.IsNullOrEmpty(name)) continue;
                        string ext = Path.GetExtension(name);
                        if (!string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase))
                            continue;
                        result.Add(name);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("LoadDrawingsFromStreamingAssets: ListStreamingAssets failed: " + e.Message);
            }
            return result;
        }
    #endif
    }
}