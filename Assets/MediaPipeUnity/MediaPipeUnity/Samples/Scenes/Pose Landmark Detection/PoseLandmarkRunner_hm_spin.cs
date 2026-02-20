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
    public class PoseLandmarkerRunner_hm_spin : VisionTaskApiRunner<PoseLandmarker>
    {
        [SerializeField] private PoseLandmarkerResultAnnotationController _poseLandmarkerResultAnnotationController;

        private Experimental.TextureFramePool _textureFramePool;

        public readonly PoseLandmarkDetectionConfig config = new PoseLandmarkDetectionConfig();

        [Header("Android Fix: Rotation / XY Remap")]
        [Tooltip("Use UnityEngine.Screen.orientation to set ImageProcessingOptions.rotationDegrees (Android-safe).")]
        public bool useScreenOrientationRotation = true;

        [Tooltip("Remap normalized landmark XY by +/-90 degrees on mobile to fix X<->Y swapped / 90deg-lying output.")]
        public bool remapXY90 = true;

        [Tooltip("1 = 90 CW (x,y)->(1-y,x), -1 = 90 CCW (x,y)->(y,1-x)")]
        [Range(-1, 1)] public int remapDir = 1;

        [Tooltip("If rotationDegrees seems inverted, enable this (rotation = 360-rotation). Usually keep OFF.")]
        public bool invertRotationDegrees = false;

        private readonly object _poseLock = new object();
        private bool _hasPose = false;
        private Vector3[] _lm0;

        private const int L_SHOULDER = 11;
        private const int R_SHOULDER = 12;
        private const int L_ELBOW = 13;
        private const int R_ELBOW = 14;
        private const int L_WRIST = 15;
        private const int R_WRIST = 16;

        private const int L_HIP = 23;
        private const int R_HIP = 24;

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

        public bool TryGetShoulders01Z(out Vector3 left01, out Vector3 right01)
        {
            left01 = right01 = default;
            lock (_poseLock)
            {
                if (!_hasPose || _lm0 == null || _lm0.Length <= R_SHOULDER) return false;
                left01 = _lm0[L_SHOULDER];
                right01 = _lm0[R_SHOULDER];
                return true;
            }
        }

        public bool TryGetHips01Z(out Vector3 left01, out Vector3 right01)
        {
            left01 = right01 = default;
            lock (_poseLock)
            {
                if (!_hasPose || _lm0 == null || _lm0.Length <= R_HIP) return false;
                left01 = _lm0[L_HIP];
                right01 = _lm0[R_HIP];
                return true;
            }
        }

        private static int GetScreenRotationDegrees()
        {
            switch (UnityEngine.Screen.orientation)
            {
                case ScreenOrientation.Portrait:
                    return 90;
                case ScreenOrientation.PortraitUpsideDown:
                    return 270;
                case ScreenOrientation.LandscapeLeft:
                    return 0;
                case ScreenOrientation.LandscapeRight:
                    return 180;
                default:
                    return 0;
            }
        }

        private static Vector3 RemapXY90(Vector3 v, int dir)
        {
            float x = v.x;
            float y = v.y;

            if (dir >= 0) return new Vector3(1f - y, x, v.z);
            return new Vector3(y, 1f - x, v.z);
        }

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
                    var v = new Vector3(x, y, z);

                    if (remapXY90 && Application.isMobilePlatform)
                    {
                        v = RemapXY90(v, remapDir);
                    }

                    tmp.Add(v);
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

        private IEnumerator WaitUntilSourceTextureReady(ImageSource imageSource)
        {
            int guard = 0;
            while (guard < 180)
            {
                var t = imageSource.GetCurrentTexture();
                if (t != null && t.width > 16 && t.height > 16) yield break;
                guard++;
                yield return null;
            }
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

            // 한국어 주석: 시작 직후 텍스처가 아직 준비되지 않는 경우가 있어 대기
            yield return WaitUntilSourceTextureReady(imageSource);

            _textureFramePool = new Experimental.TextureFramePool(
                imageSource.textureWidth,
                imageSource.textureHeight,
                TextureFormat.RGBA32,
                10);

            screen.Initialize(imageSource);

            SetupAnnotationController(_poseLandmarkerResultAnnotationController, imageSource);
            _poseLandmarkerResultAnnotationController.InitScreen(imageSource.textureWidth, imageSource.textureHeight);

            var transformationOptions = imageSource.GetTransformationOptions();
            Debug.Log($"[TRANSFORM OPTIONS TYPE] {transformationOptions.GetType().FullName}");

            var flipHorizontally = transformationOptions.flipHorizontally;
            var flipVertically = transformationOptions.flipVertically;

            int rotationDegrees = 0;
            if (useScreenOrientationRotation)
            {
                rotationDegrees = GetScreenRotationDegrees();
                if (invertRotationDegrees)
                {
                    rotationDegrees = (360 - rotationDegrees) % 360;
                }
            }

            Debug.Log($"[MP] ScreenOrientation={UnityEngine.Screen.orientation}, rot={rotationDegrees}, flipH={flipHorizontally}, flipV={flipVertically}, remapXY90={remapXY90}, remapDir={remapDir}");

            var imageProcessingOptions =
                new Tasks.Vision.Core.ImageProcessingOptions(rotationDegrees: rotationDegrees);

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

                var srcTex = imageSource.GetCurrentTexture();
                if (srcTex == null || srcTex.width <= 16 || srcTex.height <= 16)
                {
                    textureFrame.Release();
                    yield return null;
                    continue;
                }

                Image image;

                if (canUseGpuImage)
                {
                    yield return new WaitForEndOfFrame();
                    textureFrame.ReadTextureOnGPU(srcTex, flipHorizontally, flipVertically);
                    image = textureFrame.BuildGpuImage(glContext);
                }
                else
                {
                    req = textureFrame.ReadTextureAsync(srcTex, flipHorizontally, flipVertically);
                    yield return waitUntilReqDone;

                    if (req.hasError)
                    {
                        // 한국어 주석: 일부 프레임에서 Readback이 실패할 수 있으니 종료하지 않고 다음 프레임으로 넘어감
                        Debug.LogWarning("ReadTextureAsync failed on this frame. Skip frame.");
                        textureFrame.Release();
                        yield return null;
                        continue;
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
