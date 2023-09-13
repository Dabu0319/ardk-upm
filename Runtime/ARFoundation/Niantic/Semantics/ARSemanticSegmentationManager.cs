// Copyright 2023 Niantic, Inc. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Niantic.Lightship.AR.Subsystems;
using Niantic.Lightship.AR.Utilities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.ARFoundation
{
    /// <summary>
    /// The <c>ARSemanticSegmentationManager</c> controls the <c>XRSemanticsSubsystem</c> and updates the semantics
    /// textures on each Update loop. Textures and CPU buffers are available for confidence maps of individual semantic
    /// segmentation channels and a bit array indicating which semantic channels have surpassed the chosen confidence
    /// threshold per pixel. For cases where a semantic segmentation texture is overlaid on the screen, utilities are
    /// provided to read semantic properties at a given point on the screen.
    /// </summary>
    [PublicAPI]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(LightshipARUpdateOrder.k_SemanticSegmentationManager)]
    public class ARSemanticSegmentationManager :
        SubsystemLifecycleManager<XRSemanticsSubsystem, XRSemanticsSubsystemDescriptor, XRSemanticsSubsystem.Provider>
    {
        [SerializeField]
        [Tooltip("Frame rate that semantic segmentation inference will aim to run at")]
        [Range(1, 90)]
        private uint _targetFrameRate = LightshipSemanticsSubsystem.MaxRecommendedFrameRate;

        /// <summary>
        /// Frame rate that semantic segmentation inference will aim to run at.
        /// </summary>
        public uint TargetFrameRate
        {
            get => subsystem?.TargetFrameRate ?? _targetFrameRate;
            set
            {
                if (value <= 0)
                {
                    Debug.LogError("Target frame rate value must be greater than zero.");
                    return;
                }

                _targetFrameRate = value;
                if (subsystem != null)
                {
                    subsystem.TargetFrameRate = value;
                }
            }
        }

        /// <summary>
        /// The names of the semantic channels that the current model is able to detect.
        /// </summary>
        public IReadOnlyList<string> ChannelNames => _readOnlyChannelNames;

        private IReadOnlyList<string> _readOnlyChannelNames;
        private List<string> _channelNames;

        /// <summary>
        /// The indices of the semantic channels that the current model is able to detect.
        /// </summary>
        public IReadOnlyDictionary<string, int> ChannelIndices => _readOnlyChannelNamesToIndices;

        private IReadOnlyDictionary<string, int> _readOnlyChannelNamesToIndices;
        private Dictionary<string, int> _channelNamesToIndices;

        /// <summary>
        /// True if the underlying subsystem is in a state where it is outputting data.
        /// </summary>
        public bool IsDataAvailable { get; private set; }

        /// <summary>
        /// An event which fires when the underlying subsystem has finished initializing and started outputting data.
        /// </summary>
        public event Action<ARSemanticSegmentationModelEventArgs> DataInitialized
        {
            add
            {
                _dataInitialized += value;
                if (IsDataAvailable)
                {
                    var args =
                        new ARSemanticSegmentationModelEventArgs
                        {
                            ChannelNames = ChannelNames,
                            ChannelIndices = ChannelIndices
                        };

                    value.Invoke(args);
                }
            }
            remove
            {
                _dataInitialized -= value;
            }
        }

        private Action<ARSemanticSegmentationModelEventArgs> _dataInitialized;

        /// <summary>
        /// A dictionary mapping semantic confidence textures (<c>ARTextureInfo</c>s) to their respective semantic
        /// segmentation channel names.
        /// </summary>
        /// <value>
        /// The semantic segmentation confidence texture infos.
        /// </value>
        private Dictionary<string, ARTextureInfo> _semanticChannelTextureInfos = new();

        /// <summary>
        /// The semantic segmentation packed thresholded bitmask.
        /// </summary>
        /// <value>
        /// The semantic segmentation packed thresholded bitmask.
        /// </value>
        private ARTextureInfo _packedBitmaskTextureInfo;

        /// <summary>
        /// Frequently updated information about the viewport.
        /// </summary>
        private XRCameraParams _viewport;

        /// <summary>
        /// Callback before the subsystem is started (but after it is created).
        /// </summary>
        protected override void OnBeforeStart()
        {
            TargetFrameRate = _targetFrameRate;
            ResetTextureInfos();
            ResetModelData();
        }

        /// <summary>
        /// Callback when the manager is being disabled.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            ResetTextureInfos();
            ResetModelData();
        }

        /// <summary>
        /// Callback as the manager is being updated.
        /// </summary>
        public void Update()
        {
            if (subsystem == null)
                return;

            TargetFrameRate = _targetFrameRate;

            if (subsystem.running)
            {
                if (_channelNames.Count == 0)
                {
                    if (!subsystem.TryPrepareSubsystem())
                    {
                        return;
                    }

                    // Initialize channel names
                    _channelNames = subsystem.GetChannelNames();
                    Debug.Assert(_channelNames.Count > 0, "GetChannelNames is expected to be non-empty after TryPrepareSubsystem returns true");

                    _channelNamesToIndices.Clear();
                    for (int i = 0; i < _channelNames.Count; i++)
                    {
                        _channelNamesToIndices.Add(_channelNames[i], i);
                    }

                    _readOnlyChannelNames = _channelNames.AsReadOnly();
                    _readOnlyChannelNamesToIndices = new ReadOnlyDictionary<string, int>(_channelNamesToIndices);
                }

                // Update viewport info
                _viewport.screenWidth = Screen.width;
                _viewport.screenHeight = Screen.height;
                _viewport.screenOrientation = Screen.orientation;

                // Update the packed semantics texture
                Profiler.BeginSample("TryGetPackedSemanticChannels");
                if (subsystem.TryGetPackedSemanticChannels(_viewport, out var packedTextureDescriptor, out var packedSamplerMatrix))
                {
                    _packedBitmaskTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(_packedBitmaskTextureInfo, packedTextureDescriptor, packedSamplerMatrix);

                    if (!IsDataAvailable)
                    {
                        IsDataAvailable = true;
                        var args =
                            new ARSemanticSegmentationModelEventArgs
                            {
                                ChannelNames = ChannelNames,
                                ChannelIndices = ChannelIndices
                            };

                        _dataInitialized?.Invoke(args);
                    }
                }
                Profiler.EndSample();
            }
        }

        /// <summary>
        /// Retrieves the texture of semantic data where each pixel can be interpreted as a uint with bits
        /// corresponding to different classifications.
        /// </summary>
        /// <param name="texture">The packed semantics texture, owned by the manager.</param>
        /// <param name="samplerMatrix">A matrix that aligns the texture to the viewport according to the latest pose.</param>
        /// <returns></returns>
        public bool TryGetPackedSemanticsChannelsTexture(out Texture2D texture, out Matrix4x4 samplerMatrix)
        {
            // Update the packed semantics texture
            if (subsystem.TryGetPackedSemanticChannels(_viewport, out var textureDescriptor, out samplerMatrix))
            {
                _packedBitmaskTextureInfo = ARTextureInfo.GetUpdatedTextureInfo(_packedBitmaskTextureInfo,
                    textureDescriptor, samplerMatrix);
                texture = _packedBitmaskTextureInfo.Texture as Texture2D;
                return true;
            }

            texture = null;
            samplerMatrix = default;
            return false;
        }

        /// <summary>
        /// Clears the references to the packed and confidence semantic segmentation textures
        /// </summary>
        private void ResetTextureInfos()
        {
            _packedBitmaskTextureInfo.Reset();

            foreach (KeyValuePair<string, ARTextureInfo> pair in _semanticChannelTextureInfos)
                pair.Value.Dispose();

            _semanticChannelTextureInfos.Clear();
        }

        private void ResetModelData()
        {
            IsDataAvailable = false;
            _channelNames = new List<string>();
            _readOnlyChannelNames = _channelNames.AsReadOnly();

            _channelNamesToIndices = new Dictionary<string, int>();
            _readOnlyChannelNamesToIndices = new ReadOnlyDictionary<string, int>(_channelNamesToIndices);
        }

        /// <summary>
        /// Returns semantic segmentation texture for the specified semantic channel.
        /// </summary>
        /// <param name="channelName">The semantic channel to acquire.</param>
        /// <param name="samplerMatrix">A matrix that converts from screen to image coordinates.</param>
        /// <value>
        /// The texture for the specified semantic channel, if any. Otherwise, <c>null</c>.
        /// </value>
        public Texture2D GetSemanticChannelTexture(string channelName, out Matrix4x4 samplerMatrix)
        {
            // If semantic segmentation is unavailable
            if (descriptor?.semanticSegmentationImageSupported != Supported.Supported)
            {
                samplerMatrix = default;
                return null;
            }

            // If we already have an up-to-date texture
            if (_semanticChannelTextureInfos.ContainsKey(channelName))
            {
                var info = _semanticChannelTextureInfos[channelName];
                if (!info.IsDirty)
                {
                    samplerMatrix = info.SamplerMatrix;
                    return info.Texture as Texture2D;
                }
            }

            // Acquire the new texture descriptor
            if (!subsystem.TryGetSemanticChannel(channelName, _viewport, out var textureDescriptor, out samplerMatrix))
            {
                samplerMatrix = default;
                return null;
            }

            // Format mismatch
            if (textureDescriptor.dimension != TextureDimension.Tex2D)
            {
                Debug.LogError
                (
                    "Semantic confidence texture needs to be a Texture2D, but is " + textureDescriptor.dimension + "."
                );

                return null;
            }

            // Cache the texture
            if (!_semanticChannelTextureInfos.ContainsKey(channelName))
            {
                _semanticChannelTextureInfos.Add(channelName, new ARTextureInfo(textureDescriptor, samplerMatrix));
            }
            else
            {
                _semanticChannelTextureInfos[channelName] =
                    ARTextureInfo.GetUpdatedTextureInfo
                    (
                        _semanticChannelTextureInfos[channelName],
                        textureDescriptor,
                        samplerMatrix
                    );
            }

            return _semanticChannelTextureInfos[channelName].Texture as Texture2D;
        }

        /// <summary>
        /// Fills the texture with data of the specified semantic channel.
        /// </summary>
        /// <param name="channelName">The semantic channel to acquire.</param>
        /// <param name="cameraParams">Describes the viewport.</param>
        /// <param name="texture">A reference to a texture to populate with semantics data. This texture
        /// will be allocated with the correct size automatically, but destroying it is the responsibility of the caller.</param>
        /// <param name="samplerMatrix">A matrix that converts from viewport to image coordinates.</param>
        /// <returns>Whether filling the texture with semantics data was successful.</returns>
        public bool GetSemanticChannelTexture(string channelName, XRCameraParams cameraParams, ref Texture texture, out Matrix4x4 samplerMatrix)
        {
            var gotChannel =
                subsystem.TryGetSemanticChannel
                (
                    channelName,
                    cameraParams,
                    out var textureDescriptor,
                    out samplerMatrix
                );

            return gotChannel && ExternalTextureUtils.UpdateExternalTexture(ref texture, textureDescriptor);
        }

        /// <summary>
        /// Attempt to acquire the latest semantic segmentation CPU buffer for the specified semantic class. This
        /// provides direct access to the raw pixel data.
        /// </summary>
        /// <remarks>
        /// The <c>LightshipCpuBuffer</c> must be disposed to avoid resource leaks.
        /// </remarks>
        /// <param name="channel">The semantic channel to acquire.</param>
        /// <param name="cpuBuffer">If this method returns `true`, an acquired <c>LightshipCpuBuffer</c>.</param>
        /// <returns>Returns `true` if the CPU buffer was acquired. Returns `false` otherwise.</returns>
        public bool TryAcquireSemanticChannelCPUBuffer(string channel, out LightshipCpuBuffer cpuBuffer) =>
            subsystem.TryAcquireSemanticChannelCPUImage(channel, out cpuBuffer);

        /// <summary>
        /// Tries to acquire the latest packed semantic channels CPU image. Each element of the buffer is a bit field
        /// indicating which semantic channels have surpassed their respective detection confidence thresholds for that
        /// pixel. (See <c>GetChannelIndex</c>)
        /// </summary>
        /// <remarks>The utility <c>GetChannelNamesAt</c> can be used for reading semantic channel names at a viewport
        /// location.</remarks>
        /// <param name="cpuBuffer">If this method returns `true`, an acquired <see cref="LightshipCpuBuffer"/>. The CPU buffer
        /// must be disposed by the caller.</param>
        /// <returns>True if the CPU image is acquired. Otherwise, false</returns>
        public bool TryAcquirePackedSemanticChannelsCPUBuffer(out LightshipCpuBuffer cpuBuffer) =>
            subsystem.TryAcquirePackedSemanticChannelsCPUImage(out cpuBuffer);

        /// <summary>
        /// Calculates a transformation that
        /// - aligns the image as if it was taken from the latest camera pose, and
        /// - fits the image to the specified viewport resolution and orientation.
        /// </summary>
        /// <param name="cpuBuffer">CPU buffer for a semantic image.</param>
        /// <param name="cameraParams">Describes the viewport.</param>
        /// <param name="result">The 4x4 transformation matrix that when applied to the image, translates its pixels
        /// such that the image will appear as if it was taken from the latest camera pose.</param>
        /// <returns>True if the transform could be calculated, false if the buffer was invalid or it does not
        /// represent a semantic image.</returns>
        /// <exception cref="NotSupportedException">Thrown if the implementation does not support fitting the image using a transformation.</exception>
        public bool TryCalculateSamplerMatrixForCPUBuffer(LightshipCpuBuffer cpuBuffer, XRCameraParams cameraParams, out Matrix4x4 result) =>
            subsystem.TryCalculateSamplerMatrix(cpuBuffer, cameraParams, out result);

        /// <summary>
        /// Dispose a semantic segmentation CPU buffer.
        /// </summary>
        /// <remarks>
        /// The <c>LightshipCpuBuffer</c> must have been successfully acquired with <c>TryAcquireSemanticChannelCPUBuffer</c>.
        /// </remarks>
        /// <param name="cpuBuffer">The <c>LightshipCpuBuffer</c> to dispose.</param>
        public void DisposeCPUBuffer(LightshipCpuBuffer cpuBuffer)
            => subsystem.DisposeCPUImage(cpuBuffer);

        /// <summary>
        /// Get the channel index of a specified semantic class. This corresponds to a bit position in the packed
        /// semantics buffer, with index 0 being the most-significant bit.
        /// </summary>
        /// <param name="channelName">The name of the semantic class.</param>
        /// <returns>The index of the specified semantic class, or -1 if the channel does not exist.</returns>
        public int GetChannelIndex(string channelName)
        {
            if (_channelNamesToIndices.TryGetValue(channelName, out int index))
            {
                return index;
            }

            return -1;
        }

        /// <summary>
        /// Returns the semantics at the specified pixel on screen.
        /// </summary>
        /// <param name="screenX">Horizontal coordinate in screen space.</param>
        /// <param name="screenY">Vertical coordinate in screen space.</param>
        /// <returns>
        /// The result is a 32-bit packed unsigned integer where each bit is a binary indicator for a class, and the
        /// most-significant bit corresponds to the channel that is the 0th element of the ChannelNames list.
        /// </returns>
        public uint GetSemantics(int screenX, int screenY)
        {
            // Acquire the CPU image
            if (!subsystem.TryAcquirePackedSemanticChannelsCPUImage(out var packedBuffer))
                return 0u;

            // Calculate the matrix that aligns the image with the current pose
            subsystem.TryCalculateSamplerMatrix(packedBuffer, _viewport, out Matrix4x4 samplerMatrix);

            // Inspect the image
            var imageWidth = packedBuffer.dimensions.x;
            var imageHeight = packedBuffer.dimensions.y;

            // Get normalized image coordinates
            var x = screenX + 0.5f;
            var y = screenY + 0.5f;
            var uv = new Vector4(x / Screen.width, y / Screen.height, 1.0f, 1.0f);

            uint result;
            unsafe
            {
                // Access the data through a native array
                var nativeArray =
                    NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<UInt32>
                    (
                        (void*)packedBuffer.buffer,
                        imageWidth * imageHeight,
                        Allocator.None
                    );

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, AtomicSafetyHandle.GetTempMemoryHandle());
#endif
                // Sample the image
                result = nativeArray.Sample
                (
                    width: imageWidth,
                    height: imageHeight,
                    uv: uv,
                    transform: samplerMatrix
                );
            }

            subsystem.DisposeCPUImage(packedBuffer);
            return result;
        }

        /// <summary>
        /// Returns an array of channel indices that are present at the specified pixel onscreen.
        /// </summary>
        /// <remarks>This query allocates garbage.</remarks>
        /// <param name="screenX">Horizontal coordinate in screen space.</param>
        /// <param name="screenY">Vertical coordinate in screen space.</param>
        /// <returns>An array of channel indices present for the pixel.</returns>
        public List<int> GetChannelIndicesAt(int screenX, int screenY)
        {
            uint sample = GetSemantics(screenX, screenY);

            var indices = new List<int>();
            for (int idx = 0; idx < ChannelNames.Count; idx++)
            {
                // MSB = beginning of the channel names list
                if ((sample & (1 << 31)) != 0)
                {
                    indices.Add(idx);
                }

                sample <<= 1;
            }

            return indices;
        }

        /// <summary>
        /// Returns an array of channel names that are present for the specified pixel onscreen.
        /// </summary>
        /// <remarks>This query allocates garbage.</remarks>
        /// <param name="screenX">Horizontal coordinate in screen space.</param>
        /// <param name="screenY">Vertical coordinate in screen space.</param>
        /// <returns>An array of channel names present for the pixel.</returns>
        public List<string> GetChannelNamesAt(int screenX, int screenY)
        {
            var names = new List<string>();
            if (_channelNames.Count == 0)
            {
                return names;
            }

            var indices = GetChannelIndicesAt(screenX, screenY);

            foreach (var idx in indices)
            {
                if (idx >= _channelNames.Count)
                {
                    Debug.LogError("Semantics channel index exceeded channel names list");
                    return new List<string>();
                }

                names.Add(_channelNames[idx]);
            }

            return names;
        }

        /// <summary>
        /// Check if a semantic class is detected at the specified location in screen space, based on the confidence
        /// threshold set for this channel. (See <c>TrySetChannelConfidenceThresholds</c>)
        /// </summary>
        /// <param name="screenX">Horizontal coordinate in screen space.</param>
        /// <param name="screenY">Vertical coordinate in screen space.</param>
        /// <param name="channelName">Name of the semantic class to look for.</param>
        /// <returns>True if the semantic class exists at the given coordinates.</returns>
        public bool DoesChannelExistAt(int screenX, int screenY, string channelName)
        {
            if (ChannelIndices.TryGetValue(channelName, out int index))
            {
                return DoesChannelExistAt(screenX, screenY, index);
            }

            return false;
        }

        /// <summary>
        /// Check if a semantic class is detected at the specified location in screen space, based on the confidence
        /// threshold set for this channel. (See <c>TrySetChannelConfidenceThresholds</c>)
        /// </summary>
        /// <param name="screenX">Horizontal coordinate in screen space.</param>
        /// <param name="screenY">Vertical coordinate in screen space.</param>
        /// <param name="channelIndex">Index of the semantic class to look for in the ChannelNames list.</param>
        /// <returns>True if the semantic class exists at the given coordinates.</returns>
        public bool DoesChannelExistAt(int screenX, int screenY, int channelIndex)
        {
            var channelIndices = GetSemantics(screenX, screenY);
            return (channelIndices & (1 << (31 - channelIndex))) != 0;
        }

        /// <summary>
        /// Sets the confidence threshold for including the specified semantic channel in the packed semantic
        /// channel buffer.
        /// </summary>
        /// <remarks>
        /// Each semantic channel will use its default threshold value chosen by the model until a new value is set
        /// by this function during the AR session.
        /// </remarks>
        /// <param name="channelConfidenceThresholds">
        /// A dictionary consisting of keys specifying the name of the semantics channel that is needed and values
        /// between 0 and 1, inclusive, that set the threshold above which the platform will include the specified
        /// channel in the packed semantics buffer. The key must be a semantic channel name present in the list
        /// returned by <c>TryGetChannelNames</c>.
        /// </param>
        /// <exception cref="System.NotSupportedException">Thrown when setting confidence thresholds is not
        /// supported by the implementation.</exception>
        /// <returns>True if the threshold was set. Otherwise, false.</returns>
        public bool TrySetChannelConfidenceThresholds(Dictionary<string,float> channelConfidenceThresholds)
        {
            return subsystem.TrySetChannelConfidenceThresholds(channelConfidenceThresholds);
        }
    }
}
