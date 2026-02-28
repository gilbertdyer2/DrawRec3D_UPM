using System.Collections.Generic;
using UnityEngine;

namespace GilbertDyer.DrawRec3D
{
    public static class DrawingPreprocessing
    {
        /// <summary>
        /// Uniformly sample a point list to exactly N points.
        /// - If points.Count > N: uniform downsample
        /// - If points.Count < N: randomly repeat some points
        /// </summary>
        public static List<Vector3> SamplePointsFixed(List<Vector3> points, int N = 128, int? seed = null)
        {
            if (points == null || points.Count == 0)
            {
                Debug.LogError("Cannot sample from an empty point list.");
                return new List<Vector3>();
            }

            // Set random seed if provided
            if (seed.HasValue)
            {
                Random.InitState(seed.Value);
            }

            int L = points.Count;

            // Case 1: More than N, downsample uniformly
            if (L > N)
            {
                List<Vector3> sampled = new List<Vector3>(N);
                for (int i = 0; i < N; i++)
                {
                    // Linearly interpolate indices
                    float index = i * (L - 1) / (float)(N - 1);
                    sampled.Add(points[Mathf.FloorToInt(index)]);
                }
                return sampled;
            }

            // Case 2: Less than N, pad with random repeats
            if (L < N)
            {
                List<Vector3> padded = new List<Vector3>(points);
                int needed = N - L;
                
                for (int i = 0; i < needed; i++)
                {
                    int randomIndex = Random.Range(0, L);
                    padded.Add(points[randomIndex]);
                }
                
                return padded;
            }

            // Case 3: Already exactly N
            return new List<Vector3>(points);
        }

        /// <summary>
        /// Treat first point as origin (0,0,0) - Subtracts first point value from every point.
        /// </summary>
        public static List<Vector3> SetFirstAsOrigin(List<Vector3> points)
        {
            if (points == null || points.Count == 0)
            {
                Debug.LogError("Cannot set origin on empty point list.");
                return new List<Vector3>();
            }

            List<Vector3> pointsCopy = new List<Vector3>(points.Count);
            Vector3 origin = points[0];

            for (int i = 0; i < points.Count; i++)
            {
                pointsCopy.Add(points[i] - origin);
            }

            return pointsCopy;
        }

        /// <summary>
        /// Perform Furthest Point Sampling (FPS) on a set of 3D points.
        /// If less points than N, adds random "jitter" points, then performs FPS.
        /// </summary>
        public static List<Vector3> FurthestPointSampling(List<Vector3> points, int N = 128, int? seed = null, float jitterRatio = 1e-4f, bool jitterUpscale = false)
        {
            int M = points.Count;

            if (M == 0)
            {
                Debug.LogError("Point list is empty");
                return new List<Vector3>();
            }

            if (seed.HasValue)
            {
                Random.InitState(seed.Value);
            }

            // If less points than N, add random "jitter" points then do fps
            if (M <= N)
            {
                // Compute scale of the point cloud
                Vector3 bboxMin = points[0];
                Vector3 bboxMax = points[0];
                for (int i = 1; i < M; i++)
                {
                    bboxMin = Vector3.Min(bboxMin, points[i]);
                    bboxMax = Vector3.Max(bboxMax, points[i]);
                }
                float scale = Vector3.Distance(bboxMin, bboxMax);

                // Fallback if all points are identical
                if (scale == 0f) scale = 1.0f;

                int repeatCount = N - M;
                
                // Upscale to N + (N / 2)
                if (jitterUpscale)
                {
                    repeatCount = (N - M) + (N / 2);
                }

                List<Vector3> padded = new List<Vector3>(points);

                for (int i = 0; i < repeatCount; i++)
                {
                    int repeatIdx = Random.Range(0, M);
                    Vector3 repeatedPoint = points[repeatIdx];

                    // Apply jitter (Gaussian noise)
                    float stdDev = jitterRatio * scale;
                    repeatedPoint.x += GenerateNormalRandom(0f, stdDev);
                    repeatedPoint.y += GenerateNormalRandom(0f, stdDev);
                    repeatedPoint.z += GenerateNormalRandom(0f, stdDev);

                    padded.Add(repeatedPoint);
                }

                // Use normal furthest point sampling on jitter points
                if (jitterUpscale)
                {
                    return FurthestPointSampling(padded, N, seed, jitterRatio, jitterUpscale);
                }
                else
                {
                    return padded;
                }
            }

            // --- FPS Logic ---
            
            // 1. Find Centroid
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < M; i++)
            {
                centroid += points[i];
            }
            centroid /= M;

            // 2. Find the point furthest from the centroid to start
            int firstIdx = 0;
            float maxDistFromCentroid = -1f;
            for (int i = 0; i < M; i++)
            {
                float dist = Vector3.Distance(points[i], centroid);
                if (dist > maxDistFromCentroid)
                {
                    maxDistFromCentroid = dist;
                    firstIdx = i;
                }
            }

            List<int> selected = new List<int>();
            selected.Add(firstIdx);

            // Initialize distances to infinity
            float[] distances = new float[M];
            for (int i = 0; i < M; i++)
            {
                distances[i] = float.PositiveInfinity;
            }

            // 3. Iteratively select the furthest points
            for (int step = 1; step < N; step++)
            {
                Vector3 lastPoint = points[selected[selected.Count - 1]];
                
                int nextIdx = -1;
                float maxDistToSelected = -1f;

                for (int i = 0; i < M; i++)
                {
                    // Compute distance to the last selected point
                    float d = Vector3.Distance(points[i], lastPoint);

                    // Update minimum distances to the selected set
                    if (d < distances[i])
                    {
                        distances[i] = d;
                    }

                    // Keep track of the point with the maximum distance
                    if (distances[i] > maxDistToSelected)
                    {
                        maxDistToSelected = distances[i];
                        nextIdx = i;
                    }
                }
                
                selected.Add(nextIdx);
            }

            // Map indices back to Vector3 points
            List<Vector3> sampledPoints = new List<Vector3>(N);
            foreach (int idx in selected)
            {
                sampledPoints.Add(points[idx]);
            }

            return sampledPoints;
        }

        /// <summary>
        /// Helper method to generate Gaussian/Normal distribution noise using Box-Muller transform.
        /// Equivalent to numpy.random.normal
        /// </summary>
        private static float GenerateNormalRandom(float mean, float stdDev)
        {
            float u1 = 1.0f - Random.value; // Uniform(0,1]
            float u2 = 1.0f - Random.value;
            float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2); // Random normal(0,1)
            
            return mean + stdDev * randStdNormal;
        }
    }
}