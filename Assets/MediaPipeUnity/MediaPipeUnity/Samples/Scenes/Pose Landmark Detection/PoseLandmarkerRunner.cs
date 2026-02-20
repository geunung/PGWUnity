// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mediapipe.Tasks.Vision.PoseLandmarker;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mediapipe.Unity.Sample.PoseLandmarkDetection
{
    public class PoseLandmarkerRunner : VisionTaskApiRunner<PoseLandmarker>
    {
        [SerializeField] private PoseLandmarkerResultAnnotationController _poseLandmarkerResultAnnotationController;

        private Experimental.TextureFramePool _textureFramePool;

        public readonly PoseLandmarkDetectionConfig config = new PoseLandmarkDetectionConfig();

        // Latest landmarks cache (0..1 normalized)
        private readonly object _poseLock = new object();
        private bool _hasPose = false;
        private Vector3[] _lm0; // person 0 landmarks, usually 33 (x,y,z)

        // BlazePose indices
        private const int L_SHOULDER = 11;
        private const int R_SHOULDER = 12;
        private const int L_ELBOW = 13;
        private const int R_ELBOW = 14;
        private const int L_WRIST = 15;
        private const int R_WRIST = 16;

        private const int L_HIP = 23;
        private const int R_HIP = 24;

        // One generic getter only (Vector3). Avoid Vector2 overload to prevent CS0121 ambiguous calls.
        public bool TryGetLandmark01(int index, out Vector3 xyz01)
        {
            xyz01 = default;
            lock (_poseLock)
            {
                if (!_hasPose || _lm0 == null) return false;
                if (index < 0 || index >= _lm0.Length) return false;
                xyz01 = _lm0[index];
                return true;
            }
        }

        // Public helpers (Vector2 using xy)
        public bool TryGetShoulders01(out Vector2 left01, out Vector2 right01)
        {
            left01 = right01 = default;
            lock (_poseLock)
            {
                if (!_hasPose || _lm0 == null || _lm0.Length <= R_SHOULDER) return false;
                left01 = new Vector2(_lm0[L_SHOULDER].x, _lm0[L_SHOULDER].y);
                right01 = new Vector2(_lm0[R_SHOULDER].x, _lm0[R_SHOULDER].y);
                return true;
            }
        }

        public bool TryGetHips01(out Vector2 left01, out Vector2 right01)
        {
            left01 = right01 = default;
            lock (_poseLock)
            {
                if (!_hasPose || _lm0 == null || _lm0.Length <= R_HIP) return false;
                left01 = new Vector2(_lm0[L_HIP].x, _lm0[L_HIP].y);
                right01 = new Vector2(_lm0[R_HIP].x, _lm0[R_HIP].y);
                return true;
            }
        }

        public bool TryGetLeftArm3Points01(out Vector2 shoulder, out Vector2 elbow, out Vector2 wrist)
        {
            shoulder = elbow = wrist = default;
            lock (_poseLock)
            {
                if (!_hasPose || _lm0 == null || _lm0.Length <= L_WRIST) return false;
                shoulder = new Vector2(_lm0[L_SHOULDER].x, _lm0[L_SHOULDER].y);
                elbow = new Vector2(_lm0[L_ELBOW].x, _lm0[L_ELBOW].y);
                wrist = new Vector2(_lm0[L_WRIST].x, _lm0[L_WRIST].y);
                return true;
            }
        }

        public bool TryGetRightArm3Points01(out Vector2 shoulder, out Vector2 elbow, out Vector2 wrist)
        {
            shoulder = elbow = wrist = default;
            lock (_poseLock)
            {
                if (!_hasPose || _lm0 == null || _lm0.Length <= R_WRIST) return false;
                shoulder = new Vector2(_lm0[R_SHOULDER].x, _lm0[R_SHOULDER].y);
                elbow = new Vector2(_lm0[R_ELBOW].x, _lm0[R_ELBOW].y);
                wrist = new Vector2(_lm0[R_WRIST].x, _lm0[R_WRIST].y);
                return true;
            }
        }

        // Extract enumerable landmark list in a version-safe way.
        private static bool TryGetLandmarkEnumerable(object normalizedLandmarks, out IEnumerable enumerable)
        {
            enumerable = null;
            if (normalizedLandmarks == null) return false;

            string[] memberNames = { "Landmarks", "landmarks", "Landmark", "landmark" };
            var t = normalizedLandmarks.GetType();

            foreach (var name in memberNames)
            {
                var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p != null)
                {
                    var v = p.GetValue(normalizedLandmarks);
                    if (v is IEnumerable e) { enumerable = e; return true; }
                }

                var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (f != null)
                {
                    var v = f.GetValue(normalizedLandmarks);
                    if (v is IEnumerable e) { enumerable = e; return true; }
                }
            }

            if (normalizedLandmarks is IEnumerable e2)
            {
                enumerable = e2;
                return true;
            }

            return false;
        }

        // Read x/y/z from an unknown landmark object safely.
        private static bool TryReadXYZ(object lm, out float x, out float y, out float z)
        {
            x = y = z = 0f;
            if (lm == null) return false;

            var t = lm.GetType();

            bool okX = TryReadFloatMember(t, lm, "X", "x", out x);
            bool okY = TryReadFloatMember(t, lm, "Y", "y", out y);
            bool okZ = TryReadFloatMember(t, lm, "Z", "z", out z);

            return okX && okY && okZ;
        }

        private static bool TryReadFloatMember(Type t, object obj, string name1, string name2, out float value)
        {
            value = 0f;

            var p = t.GetProperty(name1, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(float))
            {
                value = (float)p.GetValue(obj);
                return true;
            }

            p = t.GetProperty(name2, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.PropertyType == typeof(float))
            {
                value = (float)p.GetValue(obj);
                return true;
            }

            var f = t.GetField(name1, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(float))
            {
                value = (float)f.GetValue(obj);
                return true;
            }

            f = t.GetField(name2, BindingFlags.Public | BindingFlags.Instance);
            if (f != null && f.FieldType == typeof(float))
            {
                value = (float)f.GetValue(obj);
                return true;
            }

            return false;
        }

        private void CachePose0(PoseLandmarkerResult result)
        {
            if (result.poseLandmarks == null || result.poseLandmarks.Count == 0)
            {
                lock (_poseLock) { _hasPose = false; _lm0 = null; }
                return;
            }

            var nlms = result.poseLandmarks[0];

            if (!TryGetLandmarkEnumerable(nlms, out var enumerable) || enumerable == null)
            {
                lock (_poseLock) { _hasPose = false; _lm0 = null; }
                return;
            }

            var tmp = new List<Vector3>(33);
            foreach (var item in enumerable)
            {
                if (TryReadXYZ(item, out var x, out var y, out var z))
                {
                    tmp.Add(new Vector3(x, y, z));
                }
            }

            if (tmp.Count < 33)
            {
                lock (_poseLock) { _hasPose = false; _lm0 = null; }
                return;
            }

            lock (_poseLock)
            {
                _lm0 = tmp.ToArray();
                _hasPose = true;
            }
        }

        public override void Stop()
        {
            base.Stop();
            _textureFramePool?.Dispose();
            _textureFramePool = null;

            lock (_poseLock) { _hasPose = false; _lm0 = null; }
        }

        protected override IEnumerator Run()
        {
            Debug.Log($"Delegate = {config.Delegate}");
            Debug.Log($"Model = {config.ModelName}");
            Debug.Log($"Running Mode = {config.RunningMode}");
            Debug.Log($"NumPoses = {config.NumPoses}");
            Debug.Log($"MinPoseDetectionConfidence = {config.MinPoseDetectionConfidence}");
            Debug.Log($"MinPosePresenceConfidence = {config.MinPosePresenceConfidence}");
            Debug.Log($"MinTrackingConfidence = {config.MinTrackingConfidence}");
            Debug.Log($"OutputSegmentationMasks = {config.OutputSegmentationMasks}");

            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

            var options = config.GetPoseLandmarkerOptions(
              config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM ? OnPoseLandmarkDetectionOutput : null
            );

            taskApi = PoseLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
            var imageSource = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Logger.LogError(TAG, "Failed to start ImageSource, exiting...");
                yield break;
            }

            _textureFramePool = new Experimental.TextureFramePool(imageSource.textureWidth, imageSource.textureHeight, TextureFormat.RGBA32, 10);

            screen.Initialize(imageSource);

            SetupAnnotationController(_poseLandmarkerResultAnnotationController, imageSource);
            _poseLandmarkerResultAnnotationController.InitScreen(imageSource.textureWidth, imageSource.textureHeight);

            var transformationOptions = imageSource.GetTransformationOptions();
            var flipHorizontally = transformationOptions.flipHorizontally;
            var flipVertically = transformationOptions.flipVertically;

            var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: 0);

            AsyncGPUReadbackRequest req = default;
            var waitUntilReqDone = new WaitUntil(() => req.done);
            var result = PoseLandmarkerResult.Alloc(options.numPoses, options.outputSegmentationMasks);

            var canUseGpuImage = options.baseOptions.delegateCase == Tasks.Core.BaseOptions.Delegate.GPU &&
              SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 &&
              GpuManager.GpuResources != null;
            using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

            while (true)
            {
                if (isPaused) yield return new WaitWhile(() => isPaused);

                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                Image image;
                if (canUseGpuImage)
                {
                    yield return new WaitForEndOfFrame();
                    textureFrame.ReadTextureOnGPU(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                    image = textureFrame.BuildGpuImage(glContext);
                }
                else
                {
                    req = textureFrame.ReadTextureAsync(imageSource.GetCurrentTexture(), flipHorizontally, flipVertically);
                    yield return waitUntilReqDone;

                    if (req.hasError)
                    {
                        Debug.LogError("Failed to read texture from the image source, exiting...");
                        break;
                    }
                    image = textureFrame.BuildCPUImage();
                    textureFrame.Release();
                }

                switch (taskApi.runningMode)
                {
                    case Tasks.Vision.Core.RunningMode.IMAGE:
                        if (taskApi.TryDetect(image, imageProcessingOptions, ref result))
                        {
                            CachePose0(result);
                            _poseLandmarkerResultAnnotationController.DrawNow(result);
                        }
                        else
                        {
                            lock (_poseLock) { _hasPose = false; _lm0 = null; }
                            _poseLandmarkerResultAnnotationController.DrawNow(default);
                        }
                        break;

                    case Tasks.Vision.Core.RunningMode.VIDEO:
                        if (taskApi.TryDetectForVideo(image, GetCurrentTimestampMillisec(), imageProcessingOptions, ref result))
                        {
                            CachePose0(result);
                            _poseLandmarkerResultAnnotationController.DrawNow(result);
                        }
                        else
                        {
                            lock (_poseLock) { _hasPose = false; _lm0 = null; }
                            _poseLandmarkerResultAnnotationController.DrawNow(default);
                        }
                        break;

                    case Tasks.Vision.Core.RunningMode.LIVE_STREAM:
                        taskApi.DetectAsync(image, GetCurrentTimestampMillisec(), imageProcessingOptions);
                        break;
                }
            }
        }

        private void OnPoseLandmarkDetectionOutput(PoseLandmarkerResult result, Image image, long timestamp)
        {
            CachePose0(result);
            _poseLandmarkerResultAnnotationController.DrawLater(result);
        }
    }
}