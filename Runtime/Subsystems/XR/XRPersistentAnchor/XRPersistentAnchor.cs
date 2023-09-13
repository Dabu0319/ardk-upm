using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.Subsystems
{
    /// <summary>
    /// Describes session-relative data for an anchor.
    /// </summary>
    /// <seealso cref="XRPersistentAnchor"/>
    [StructLayout(LayoutKind.Sequential)]
    public struct XRPersistentAnchor : ITrackable, IEquatable<XRPersistentAnchor>
    {
        /// <summary>
        /// Gets a default-initialized <see cref="XRPersistentAnchor"/>. This may be
        /// different from the zero-initialized version (for example, the <see cref="pose"/>
        /// is <c>Pose.identity</c> instead of zero-initialized).
        /// </summary>
        public static XRPersistentAnchor defaultValue => s_Default;

        static readonly XRPersistentAnchor s_Default = new XRPersistentAnchor
        {
            m_Id = TrackableId.invalidId, m_Pose = Pose.identity, m_timestampMs = 0
        };

        /// <summary>
        /// Constructs the session-relative data for an anchor.
        /// This is typically provided by an implementation of the <see cref="XRPersistentAnchor"/>
        /// and not invoked directly.
        /// </summary>
        /// <param name="trackableId">The <see cref="TrackableId"/> associated with this anchor.</param>
        /// <param name="pose">The <c>Pose</c>, in session space, of the anchor.</param>
        /// <param name="trackingState">The <see cref="TrackingState"/> of the anchor.</param>
        /// <param name="trackingStateReason">The reason for the current tracking state.</param>
        /// <param name="xrPersistentAnchorPayload">The payload associated with the anchor.</param>
        public XRPersistentAnchor(
            TrackableId trackableId,
            Pose pose,
            TrackingState trackingState,
            TrackingStateReason trackingStateReason,
            XRPersistentAnchorPayload xrPersistentAnchorPayload,
            UInt64 timestampMs)
        {
            m_Id = trackableId;
            m_Pose = pose;
            m_TrackingState = trackingState;
            m_TrackingStateReason = trackingStateReason;
            m_XRPersistentAnchorPayload = xrPersistentAnchorPayload;
            m_timestampMs = timestampMs;
        }

        public XRPersistentAnchor(
            TrackableId trackableId)
        {
            m_Id = trackableId;
            m_Pose = Pose.identity;
            m_TrackingState = TrackingState.None;
            m_TrackingStateReason = TrackingStateReason.None;
            m_XRPersistentAnchorPayload = new XRPersistentAnchorPayload(new IntPtr(0), 0);
            m_timestampMs = 0;
        }

        /// <summary>
        /// Get the <see cref="TrackableId"/> associated with this anchor.
        /// </summary>
        public TrackableId trackableId => m_Id;

        /// <summary>
        /// Get the <c>Pose</c>, in session space, for this anchor.
        /// </summary>
        public Pose pose => m_Pose;

        /// <summary>
        /// Get the <see cref="TrackingState"/> of this anchor.
        /// </summary>
        public TrackingState trackingState => m_TrackingState;

        /// <summary>
        /// Get the <see cref="trackingStateReason"/> of this anchor.
        /// </summary>
        public TrackingStateReason trackingStateReason => m_TrackingStateReason;

        /// <summary>
        /// The payload for this anchor
        /// </summary>
        public XRPersistentAnchorPayload xrPersistentAnchorPayload => m_XRPersistentAnchorPayload;

        /// <summary>
        /// Get the timestamp in miliseconds of the latest update for this anchor.
        /// The timestamp has the same base as the frame.
        /// </summary>
        public UInt64 timestampMs => m_timestampMs;

        /// <summary>
        /// A native pointer associated with the anchor.
        /// The data pointed to by this pointer is implementation-specific.
        /// </summary>
        public IntPtr nativePtr => m_XRPersistentAnchorPayload.nativePtr;

        /// <summary>
        /// Generates a hash suitable for use with containers like `HashSet` and `Dictionary`.
        /// </summary>
        /// <returns>A hash code generated from this object's fields.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = m_Id.GetHashCode();
                hashCode = hashCode * 486187739 + m_Pose.GetHashCode();
                hashCode = hashCode * 486187739 + ((int)m_TrackingState).GetHashCode();
                hashCode = hashCode * 486187739 + m_timestampMs.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="other">The other <see cref="XRPersistentAnchor"/> to compare against.</param>
        /// <returns>`True` if every field in <paramref name="other"/> is equal to this <see cref="XRPersistentAnchor"/>, otherwise false.</returns>
        public bool Equals(XRPersistentAnchor other)
        {
            return
                m_Id.Equals(other.m_Id) &&
                m_Pose.Equals(other.m_Pose) &&
                m_TrackingState == other.m_TrackingState &&
                m_XRPersistentAnchorPayload == other.m_XRPersistentAnchorPayload &&
                m_timestampMs == other.m_timestampMs;
        }

        /// <summary>
        /// Tests for equality.
        /// </summary>
        /// <param name="obj">The `object` to compare against.</param>
        /// <returns>`True` if <paramref name="obj"/> is of type <see cref="XRPersistentAnchor"/> and
        /// <see cref="Equals(XRPersistentAnchor)"/> also returns `true`; otherwise `false`.</returns>
        public override bool Equals(object obj) => obj is XRPersistentAnchor && Equals((XRPersistentAnchor)obj);

        /// <summary>
        /// Tests for equality. Same as <see cref="Equals(XRPersistentAnchor)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator ==(XRPersistentAnchor lhs, XRPersistentAnchor rhs) => lhs.Equals(rhs);

        /// <summary>
        /// Tests for inequality. Same as `!`<see cref="Equals(XRPersistentAnchor)"/>.
        /// </summary>
        /// <param name="lhs">The left-hand side of the comparison.</param>
        /// <param name="rhs">The right-hand side of the comparison.</param>
        /// <returns>`True` if <paramref name="lhs"/> is not equal to <paramref name="rhs"/>, otherwise `false`.</returns>
        public static bool operator !=(XRPersistentAnchor lhs, XRPersistentAnchor rhs) => !lhs.Equals(rhs);

        TrackableId m_Id;

        Pose m_Pose;

        TrackingState m_TrackingState;

        TrackingStateReason m_TrackingStateReason;

        XRPersistentAnchorPayload m_XRPersistentAnchorPayload;

        UInt64 m_timestampMs;
    }
}
