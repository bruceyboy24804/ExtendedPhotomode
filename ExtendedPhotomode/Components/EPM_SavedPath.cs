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
        /// <summary>Version 2 added the closed flag.</summary>
        public const int kVersion = 2;

        public int m_Id;

        public FixedString128Bytes m_Name;

        /// <summary>Whether the path's last point joins back to its first.</summary>
        /// <remarks>
        /// Belongs to the path rather than to a node, which is why it lives here and not in
        /// <see cref="EPM_PathNodeData"/> — the nodes are identical either way, and only the segment
        /// walk changes.
        /// </remarks>
        public bool m_Closed;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(kVersion);
            writer.Write(m_Id);
            writer.Write(m_Name.ToString());
            writer.Write(m_Closed);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);
            reader.Read(out m_Id);

            reader.Read(out string name);
            m_Name = new FixedString128Bytes(name ?? string.Empty);

            // A version 1 record stops here; reading further would run into the next component.
            if (version < 2) {
                m_Closed = false;
                return;
            }

            reader.Read(out m_Closed);
        }
    }
}
