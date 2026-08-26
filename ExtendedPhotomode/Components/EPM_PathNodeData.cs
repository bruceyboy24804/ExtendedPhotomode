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
