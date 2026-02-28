using UnityEngine;
using Unity.Sentis;
using System.Collections.Generic;

namespace GilbertDyer.DrawRec3D
{
    public class DrawingRecognizer : MonoBehaviour
    {
        [Header("Model Settings")]
        [Tooltip("Drag .onnx model file here")]
        public ModelAsset modelAsset;

        [Header("Drawing dictionary")]
        [Tooltip("Source of reference drawings (name -> points). If unset, will try to find in scene.")]
        [SerializeField] private LoadDrawingsFromStreamingAssets loadDrawingsFromStreamingAssets;
        
        [Header("Runtime Settings")]
        public BackendType workerType = BackendType.GPUCompute;

        private Model runtimeModel;
        private Worker worker;
        
        void Start()
        {
            if (modelAsset == null)
            {
                Debug.LogError("Model asset is not assigned!");
                return;
            }
            
            // Load the model
            runtimeModel = ModelLoader.Load(modelAsset);
            Debug.Log("Model loaded successfully");
            
            // Create worker for inference
            worker = new Worker(runtimeModel, workerType);
            Debug.Log($"Worker created with type: {workerType}");

            if (loadDrawingsFromStreamingAssets == null)
                loadDrawingsFromStreamingAssets = FindObjectOfType<LoadDrawingsFromStreamingAssets>();
        }

        public string GetMatch(List<Vector3> points)
        {
            if (loadDrawingsFromStreamingAssets == null || !loadDrawingsFromStreamingAssets.isReady)
            {
                Debug.LogWarning("[DrawingRecognizer]: LoadDrawingsFromStreamingAssets not set or not ready.");
                return "None";
            }

            IReadOnlyDictionary<string, List<Vector3>> drawings = loadDrawingsFromStreamingAssets.Drawings;
            if (drawings.Count == 0)
            {
                Debug.LogWarning("[DrawingRecognizer]: No reference drawings loaded.");
                return "None";
            }

            // Process Input Points
            points = DrawingPreprocessing.FurthestPointSampling(points, 128, 0);
            points = DrawingPreprocessing.SetFirstAsOrigin(points);

            string res = "None";
            float minScore = float.MaxValue;

            // Get Input Embedding
            float[] embInput = GetEmbedding(points);
            if (embInput == null) return "None";

            // Compare against reference library
            foreach (var drawing in drawings)
            {
                List<Vector3> pointsCompare = DrawingPreprocessing.FurthestPointSampling(drawing.Value, 128, drawing.Key.GetHashCode());
                pointsCompare = DrawingPreprocessing.SetFirstAsOrigin(pointsCompare);
                
                if (pointsCompare == null || pointsCompare.Count != 128) continue;

                float[] embCompare = GetEmbedding(pointsCompare);
                if (embCompare == null) continue;

                float score = CompareSimilarity(embCompare, embInput);
                Debug.Log($"\t[DrawingRecognizer]: Compared to '{drawing.Key}', score {score}");
                
                if (score < minScore)
                {
                    minScore = score;
                    res = drawing.Key;
                }
            }

            Debug.Log($"[DrawingRecognizer]: Found match: '{res}'");
            return res;
        }

        public float[] GetEmbedding(List<Vector3> points)
        {
            if (worker == null)
            {
                Debug.LogError("Worker not initialized!");
                return null;
            }
            
            if (points.Count != 128)
            {
                Debug.LogWarning($"Expected 128 points, got {points.Count}");
            }
            
            // Create strongly typed input tensor
            Tensor<float> inputTensor = CreateInputTensor(points);

            // Run inference (Execute -> Schedule)
            worker.Schedule(inputTensor);
            
            // Get output tensor and cast it
            Tensor<float> outputTensor = worker.PeekOutput() as Tensor<float>;
            
            // SAFELY download the data from the GPU to the CPU
            float[] outputData = outputTensor.DownloadToArray();
            
            int embedDim = 128;
            float[] embedding = new float[embedDim];
            for (int i = 0; i < embedDim && i < outputData.Length; i++)
            {
                // Now reading from the local float array, not the GPU tensor directly
                embedding[i] = outputData[i]; 
            }
            
            // Clean up input tensor to prevent memory leaks
            inputTensor.Dispose();
            
            return embedding;
        }

        public static Tensor<float> CreateInputTensor(List<Vector3> points)
        {
            float[] multiChannelData = Compute2ChannelMatrix(points);
            
            // Sentis requires a TensorShape object
            TensorShape shape = new TensorShape(1, 128, 128, 2);
            return new Tensor<float>(shape, multiChannelData);
        }

        /// <summary>
        /// Computes a distance matrix with a height channel for each point
        /// Expects exactly 128 points (must run through your FurthestPointSampling first).
        /// </summary>
        public static float[] Compute2ChannelMatrix(List<Vector3> points)
        {
            int numPoints = points.Count; // Must be 128
            int channels = 2;
            float[] matrix = new float[numPoints * numPoints * channels];

            // Find the global scale (maximum Euclidean distance between any two points)
            float maxDist = 0f;
            for (int i = 0; i < numPoints; i++)
            {
                for (int j = 0; j < numPoints; j++)
                {
                    float dist = Vector3.Distance(points[i], points[j]);
                    if (dist > maxDist) maxDist = dist;
                }
            }
            float scale = maxDist + 1e-8f; // Prevent division by zero

            // Populate the flat 2-Channel Array (NHWC format)
            for (int i = 0; i < numPoints; i++)
            {
                for (int j = 0; j < numPoints; j++)
                {
                    // Calculate the 1D index for Channel 0 and Channel 1
                    int indexDist = (i * numPoints + j) * channels + 0;
                    int indexYDiff = (i * numPoints + j) * channels + 1;

                    // Channel 0: Euclidean Distance (distance matrix)
                    float dist = Vector3.Distance(points[i], points[j]);
                    
                    // Channel 1: Y-Axis Difference
                    float yDiff = points[i].y - points[j].y;

                    // Normalize and assign
                    matrix[indexDist] = dist / scale;
                    matrix[indexYDiff] = yDiff / scale;
                }
            }

            return matrix;
        }
        
        /// <summary>
        /// Compare two embeddings and return similarity score (Lower = more similar)
        /// </summary>
        public float CompareSimilarity(float[] embeddingA, float[] embeddingB)
        {
            if (embeddingA.Length != embeddingB.Length)
            {
                Debug.LogError("Embeddings must be same length!");
                return float.MaxValue; // Return max distance so it is ignored as a match
            }
            
            // Compute Euclidean distance
            float sumSquares = 0f;
            for (int i = 0; i < embeddingA.Length; i++)
            {
                float diff = embeddingA[i] - embeddingB[i];
                sumSquares += diff * diff;
            }
            
            return Mathf.Sqrt(sumSquares);
        }
        
        void OnDestroy()
        {
            // Clean up resources
            worker?.Dispose();
        }
    }
}