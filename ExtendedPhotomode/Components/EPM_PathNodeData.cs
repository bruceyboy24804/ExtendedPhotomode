namespace ExtendedPhotomode.Components {
    #region Using Statements

    using Colossal.Serialization.Entities;

    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    /// <summary>
    /// One control point of a saved camera path, stored so the game's own save system persists it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Held as an <see cref="IBufferElementData"/> on a singleton entity rather than written to a file
    /// of our own. A camera path is made of world coordinates, so it only means anything in the city
    /// it was drawn in — putting it in the save file ties it to that city automatically, survives
    /// save-as and reload, and travels with the save if it is shared. This is the pattern IMT uses for
    /// its marking data.
    /// </para>
    /// <para>
    /// Every record writes its version first and reads it back before anything else, so the format can
    /// grow without invalidating existing saves. Bump <see cref="kVersion"/> and add a branch in
    /// <see cref="Deserialize{TReader}"/> — never reorder an existing version's fields.
    /// </para>
    /// </remarks>
    public struct EPM_PathNodeData : IBufferElementData, ISerializable {
        /// <summary>Version 2 added dwell and the pitch override; version 3 the per-point shot data.</summary>
        public const int kVersion = 3;

        public float3 m_Position;

        public float3 m_TangentOut;

        public float3 m_TangentIn;

        public bool m_Auto;

        public bool m_Broken;

        public float m_Dwell;

        /// <summary>The point's pitch override, meaningful only when <see cref="m_HasPitch"/> is set.</summary>
        /// <remarks>
        /// Stored as a value plus a flag rather than a nullable, because a buffer element has to be an
        /// unmanaged struct and the reader has no notion of null.
        /// </remarks>
        public float m_Pitch;

        public bool m_HasPitch;

        public float m_Speed;

        public float3 m_LookAt;

        public bool m_HasLookAt;

        public float m_Fov;

        public bool m_HasFov;

        public float m_TimeOfDay;

        public bool m_HasTimeOfDay;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(kVersion);
            writer.Write(m_Position);
            writer.Write(m_TangentOut);
            writer.Write(m_TangentIn);
            writer.Write(m_Auto);
            writer.Write(m_Broken);
            writer.Write(m_Dwell);
            writer.Write(m_Pitch);
            writer.Write(m_HasPitch);
            writer.Write(m_Speed);
            writer.Write(m_LookAt);
            writer.Write(m_HasLookAt);
            writer.Write(m_Fov);
            writer.Write(m_HasFov);
            writer.Write(m_TimeOfDay);
            writer.Write(m_HasTimeOfDay);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);

            reader.Read(out m_Position);
            reader.Read(out m_TangentOut);
            reader.Read(out m_TangentIn);
            reader.Read(out m_Auto);
            reader.Read(out m_Broken);

            // A version 1 record stops here. Reading further would consume the next element's fields
            // and corrupt the whole buffer, so the branch is not optional.
            if (version < 2) {
                m_Dwell    = 0f;
                m_Pitch    = 0f;
                m_HasPitch = false;
                ClearVersion3();
                return;
            }

            reader.Read(out m_Dwell);
            reader.Read(out m_Pitch);
            reader.Read(out m_HasPitch);

            if (version < 3) {
                ClearVersion3();
                return;
            }

            reader.Read(out m_Speed);
            reader.Read(out m_LookAt);
            reader.Read(out m_HasLookAt);
            reader.Read(out m_Fov);
            reader.Read(out m_HasFov);
            reader.Read(out m_TimeOfDay);
            reader.Read(out m_HasTimeOfDay);
        }

        // Speed defaults to 1, not 0: an older record means "no opinion", and a zero would stall the
        // camera on every point of a path saved before the field existed.
        private void ClearVersion3() {
            m_Speed         = 1f;
            m_LookAt        = default;
            m_HasLookAt     = false;
            m_Fov           = 0f;
            m_HasFov        = false;
            m_TimeOfDay     = 0f;
            m_HasTimeOfDay  = false;
        }
    }
}
