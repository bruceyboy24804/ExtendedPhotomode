namespace ExtendedPhotomode.Components {
    #region Using Statements

    using Colossal.Serialization.Entities;

    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>
    /// Marks an entity as one named path in the library, alongside its
    /// <see cref="EPM_PathNodeData"/> buffer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One entity per saved path rather than one entity holding them all. Serialization is per-entity,
    /// so this keeps each path's nodes in their own buffer instead of needing a path index on every
    /// node and a partition step on load — and deleting a path becomes destroying an entity.
    /// </para>
    /// <para>
    /// The id is stored rather than derived from position. The list the UI sees is sorted by name and
    /// shifts whenever a path is added or removed, so a row's position is not a stable handle; IMT
    /// learned the same lesson with its templates, where positional indexing edited a different
    /// template than the one highlighted.
    /// </para>
    /// </remarks>
    public struct EPM_SavedPath : IComponentData, ISerializable {
        public const int kVersion = 1;

        public int m_Id;

        public FixedString128Bytes m_Name;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(kVersion);
            writer.Write(m_Id);
            writer.Write(m_Name.ToString());
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int _);
            reader.Read(out m_Id);

            reader.Read(out string name);
            m_Name = new FixedString128Bytes(name ?? string.Empty);
        }
    }
}
