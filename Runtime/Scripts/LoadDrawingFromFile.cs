using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;


namespace GilbertDyer.DrawRec3D
{
    /// <summary>
    /// Loads a drawing from a JSON file produced by SaveDrawingToFile and converts it to a List of Vector3 points.
    /// </summary>
    public class LoadDrawingFromFile : MonoBehaviour
    {
        /// <summary>
        /// Loads a drawing from a JSON file and returns the points as a List of Vector3 points.
        /// </summary>
        /// <param name="filePath">Full path to the .json file.</param>
        /// <param name="points">Output list of points. Returns null if loading fails.</param>
        /// <returns>True if the file was loaded successfully; otherwise false.</returns>
        public static bool LoadDrawing(string filePath, out List<Vector3> points)
        {
            points = null;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.LogError($"LoadDrawingFromFile: File not found or path empty: {filePath}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                return LoadDrawingFromJson(json, out points);
            }
            catch (Exception e)
            {
                Debug.LogError($"LoadDrawingFromFile: Failed to read file: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Parses JSON text from SaveDrawingToFile and returns the points as a List&lt;Vector3&gt;.
        /// </summary>
        /// <param name="json">JSON string (e.g. from a file or TextAsset.text).</param>
        /// <param name="points">Output list of points. Returns null if parsing fails.</param>
        /// <returns>True if the JSON was parsed successfully; otherwise false.</returns>
        public static bool LoadDrawingFromJson(string json, out List<Vector3> points)
        {
            points = null;
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("LoadDrawingFromFile: JSON string is null or empty.");
                return false;
            }

            try
            {
                DrawingWrapper wrapper = JsonUtility.FromJson<DrawingWrapper>(json);
                if (wrapper?.points == null)
                {
                    Debug.LogError("LoadDrawingFromFile: JSON did not contain a valid points array.");
                    return false;
                }

                points = new List<Vector3>(wrapper.points.Count);
                foreach (SerializablePoint p in wrapper.points)
                {
                    points.Add(new Vector3(p.x, p.y, p.z));
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"LoadDrawingFromFile: Failed to parse JSON: {e.Message}");
                return false;
            }
        }

        // Must match the structure used by SaveDrawingToFile for JsonUtility.FromJson
        [Serializable]
        private class DrawingWrapper
        {
            public string drawingName;
            public int size;
            public List<SerializablePoint> points;
        }

        [Serializable]
        private class SerializablePoint
        {
            public float x;
            public float y;
            public float z;
        }
    }
}