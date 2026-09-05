namespace ExtendedPhotomode.Components {
    #region Using Statements

    using ExtendedPhotomode.Camera;

    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>Reads and writes a pair of drawn paths on any entity that carries the node buffers.</summary>
    /// <remarks>
    /// The saved-path library, the working-path store and now each shot in the sequence all persist
    /// the same thing in the same shape. This is that shape, in one place — the third copy of the
    /// field-by-field conversion is where one of them quietly stops storing a field, which is exactly
    /// how the working path came to lose dwell, speed and the rest.
    /// </remarks>
    public static class PathBuffers {
        /// <summary>Writes both paths onto an entity, adding the buffers if it has none.</summary>
        public static void Store(EntityManager entities, Entity entity, CameraPath travel,
                                 CameraPath rail) {
            DynamicBuffer<EPM_PathNodeData> nodes = Buffer<EPM_PathNodeData>(entities, entity);

            nodes.Clear();

            foreach (PathNode node in travel.Nodes) {
                nodes.Add(new EPM_PathNodeData {
                    m_Position   = node.Position,
                    m_TangentOut = node.TangentOut,
                    m_TangentIn  = node.TangentIn,
                    m_Auto       = node.Auto,
                    m_Broken     = node.Broken,

                    m_Dwell        = node.Dwell,
                    m_Pitch        = node.Pitch ?? 0f,
                    m_HasPitch     = node.Pitch.HasValue,
                    m_Speed        = node.Speed,
                    m_LookAt       = node.LookAt ?? default,
                    m_HasLookAt    = node.LookAt.HasValue,
                    m_Fov          = node.Fov ?? 0f,
                    m_HasFov       = node.Fov.HasValue,
                    m_TimeOfDay    = node.TimeOfDay ?? 0f,
                    m_HasTimeOfDay = node.TimeOfDay.HasValue,
                });
            }

            DynamicBuffer<EPM_RailNodeData> rails = Buffer<EPM_RailNodeData>(entities, entity);

            rails.Clear();

            foreach (PathNode node in rail.Nodes) {
                rails.Add(new EPM_RailNodeData {
                    m_Position   = node.Position,
                    m_TangentOut = node.TangentOut,
                    m_TangentIn  = node.TangentIn,
                    m_Auto       = node.Auto,
                    m_Broken     = node.Broken,
                });
            }
        }

        /// <summary>Replaces both paths from an entity's buffers, clearing them when it has none.</summary>
        public static void Load(EntityManager entities, Entity entity, CameraPath travel,
                                CameraPath rail) {
            travel.Clear();
            rail.Clear();

            if (entities.HasBuffer<EPM_PathNodeData>(entity)) {
                DynamicBuffer<EPM_PathNodeData> nodes =
                    entities.GetBuffer<EPM_PathNodeData>(entity, true);

                for (int i = 0; i < nodes.Length; i++) {
                    EPM_PathNodeData data = nodes[i];

                    travel.Nodes.Add(new PathNode(data.m_Position) {
                        TangentOut = data.m_TangentOut,
                        TangentIn  = data.m_TangentIn,
                        Auto       = data.m_Auto,
                        Broken     = data.m_Broken,

                        Dwell     = data.m_Dwell,
                        Pitch     = data.m_HasPitch ? data.m_Pitch : (float?)null,
                        Speed     = data.m_Speed,
                        LookAt    = data.m_HasLookAt ? (Vector3)data.m_LookAt : (Vector3?)null,
                        Fov       = data.m_HasFov ? data.m_Fov : (float?)null,
                        TimeOfDay = data.m_HasTimeOfDay ? data.m_TimeOfDay : (float?)null,
                    });
                }
            }

            if (entities.HasBuffer<EPM_RailNodeData>(entity)) {
                DynamicBuffer<EPM_RailNodeData> rails =
                    entities.GetBuffer<EPM_RailNodeData>(entity, true);

                for (int i = 0; i < rails.Length; i++) {
                    EPM_RailNodeData data = rails[i];

                    rail.Nodes.Add(new PathNode(data.m_Position) {
                        TangentOut = data.m_TangentOut,
                        TangentIn  = data.m_TangentIn,
                        Auto       = data.m_Auto,
                        Broken     = data.m_Broken,
                    });
                }
            }

            travel.RefreshAutoTangents();
            rail.RefreshAutoTangents();
        }

        private static DynamicBuffer<T> Buffer<T>(EntityManager entities, Entity entity)
            where T : unmanaged, IBufferElementData {
            return entities.HasBuffer<T>(entity) ? entities.GetBuffer<T>(entity)
                                                 : entities.AddBuffer<T>(entity);
        }
    }
}
