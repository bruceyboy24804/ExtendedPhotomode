namespace ExtendedPhotomode.Components {
    #region Using Statements

    using Colossal.Serialization.Entities;

    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    /// <summary>One control point of a saved aim rail.</summary>
    /// <remarks>
    /// A separate buffer type rather than a flag on <see cref="EPM_PathNodeData"/>. Two reasons: a
    /// buffer holds one element type, so partitioning a shared buffer would mean a discriminator on
    /// every node and a split on every load; and adding a brand new component cannot disturb saves
    /// written before it existed, whereas widening the existing record needs another format version
    /// and another branch to get wrong.
    /// <para>
    /// The rail carries only geometry. Dwell, speed, lens and time of day are properties of the
    /// camera's move, and a point on the rail is nothing but somewhere to look — so the fields that
    /// would be meaningless here are simply absent rather than stored and ignored.
    /// </para>
    /// </remarks>
    public struct EPM_RailNodeData : IBufferElementData, ISerializable {
        public const int kVersion = 1;

        public float3 m_Position;

        public float3 m_TangentOut;

        public float3 m_TangentIn;

        public bool m_Auto;

        public bool m_Broken;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(kVersion);
            writer.Write(m_Position);
            writer.Write(m_TangentOut);
            writer.Write(m_TangentIn);
            writer.Write(m_Auto);
            writer.Write(m_Broken);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int _);
            reader.Read(out m_Position);
            reader.Read(out m_TangentOut);
            reader.Read(out m_TangentIn);
            reader.Read(out m_Auto);
            reader.Read(out m_Broken);
        }
    }
}
