using System;
using Niantic.Lightship.AR.Subsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.PersistentAnchorSubsystem
{
    public class MockApi : IApi
    {
        public IntPtr Construct(IntPtr unityContext)
        {
            throw new NotImplementedException();
        }

        public void Start(IntPtr anchorProviderHandle)
        {
            throw new NotImplementedException();
        }

        public void Stop(IntPtr anchorProviderHandle)
        {
            throw new NotImplementedException();
        }

        public void Configure(IntPtr anchorProviderHandle)
        {
            throw new NotImplementedException();
        }

        public void Destruct(IntPtr persistentAnchorApiHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryCreateAnchor(IntPtr anchorProviderHandle, Pose pose, out TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public bool TryRemoveAnchor(IntPtr anchorProviderHandle, TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public bool TryTrackAnchor(IntPtr anchorProviderHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public bool TryLocalize(IntPtr persistentAnchorApiHandle, IntPtr anchorPayload, int payloadSize,
            out TrackableId anchorId)
        {
            throw new NotImplementedException();
        }

        public IntPtr AcquireLatestChanges(IntPtr anchorProviderHandle, out IntPtr addedPtr, out int addedCount,
            out IntPtr updatedPtr,
            out int updatedCount, out IntPtr removedPtr, out int removedCount)
        {
            throw new NotImplementedException();
        }

        public void ReleaseLatestChanges(IntPtr context)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractAnchorChange(IntPtr anchorChangeHandle, out TrackableId trackableId, out Pose pose,
            out int trackingState, out int trackingStateReason, out IntPtr anchorPayloadPtr, out int anchorPayloadSize, out UInt64 timestampMs)
        {
            throw new NotImplementedException();
        }

        public IntPtr AcquireNetworkStatus
            (IntPtr anchorProviderHandle, out IntPtr networkStatusList, out int listCount) =>
            throw new NotImplementedException();

        public void ReleaseNetworkStatus(IntPtr latestChangesHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractNetworkStatus
        (
            IntPtr networkStatusHandle,
            out Guid requestId,
            out RequestStatus networkStatus,
            out RequestType typeOut,
            out ErrorCode errorOut,
            out ulong startTimeMsOut,
            out ulong endTimeMsOut
        ) =>
            throw new NotImplementedException();

        public IntPtr AcquireLocalizationStatus
            (IntPtr anchorProviderHandle, out IntPtr localizationStatusList, out int listCount) =>
            throw new NotImplementedException();

        public void ReleaseLocalizationStatus(IntPtr latestChangesHandle)
        {
            throw new NotImplementedException();
        }

        public bool TryExtractLocalizationStatus
            (IntPtr localizationStatusHandle, out Guid nodeId, out LocalizationStatus statusOut, out float confidenceOut) =>
            throw new NotImplementedException();

        public bool GetVpsSessionId (IntPtr anchorProviderHandle, out string vpsSessionId) =>
            throw new NotImplementedException();
    }
}
