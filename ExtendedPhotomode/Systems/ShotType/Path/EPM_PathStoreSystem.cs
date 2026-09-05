namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using Colossal.Serialization.Entities;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Components;
    using ExtendedPhotomode.Tools;

    using Game;
    using Game.Common;
    using Game.Serialization;

    using ModsCommon.Utils;

    using Unity.Entities;
    using Unity.Mathematics;

    using UnityEngine;

    #endregion

    /// <summary>Persists the drawn camera path into the city save and restores it on load.</summary>
    /// <remarks>
    /// A singleton entity carrying an <see cref="EPM_PathNodeData"/> buffer. <c>OnGameLoaded</c> and
    /// <c>IPreSerialize</c> bracket the only two moments the data has to be correct, so nothing is
    /// mirrored into ECS on every edit.
    /// </remarks>
    public partial class EPM_PathStoreSystem : GameSystemBase, IPreSerialize {
        private EPM_PathToolSystem m_PathTool;
        private PrefixedLogger     m_Log;
        private EntityQuery        m_Query;

        protected override void OnCreate() {
            base.OnCreate();
            m_Log      = new PrefixedLogger(nameof(EPM_PathStoreSystem));
            m_PathTool = World.GetOrCreateSystemManaged<EPM_PathToolSystem>();
            m_Query    = GetEntityQuery(ComponentType.ReadWrite<EPM_PathNodeData>());

            Enabled = false;
        }

        protected override void OnUpdate() { }

        protected override void OnGameLoaded(Context serializationContext) {
            base.OnGameLoaded(serializationContext);
            LoadPath();
        }

        public void PreSerialize(Context context) { SavePath(); }

        private void SavePath() {
            Entity entity = GetOrCreateEntity();
            DynamicBuffer<EPM_PathNodeData> buffer = EntityManager.GetBuffer<EPM_PathNodeData>(entity);

            buffer.Clear();

            // Every field, not just the geometry. This used to store position, tangents and the two
            // flags alone, which quietly threw away dwell, pitch, speed, look-at, lens and time of day
            // on any working path that had not been saved to the library first.
            foreach (PathNode node in m_PathTool.TravelPath.Nodes) {
                buffer.Add(new EPM_PathNodeData {
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

            DynamicBuffer<EPM_RailNodeData> rail = EntityManager.HasBuffer<EPM_RailNodeData>(entity)
                                                       ? EntityManager.GetBuffer<EPM_RailNodeData>(entity)
                                                       : EntityManager.AddBuffer<EPM_RailNodeData>(entity);

            rail.Clear();

            foreach (PathNode node in m_PathTool.RailPath.Nodes) {
                rail.Add(new EPM_RailNodeData {
                    m_Position   = node.Position,
                    m_TangentOut = node.TangentOut,
                    m_TangentIn  = node.TangentIn,
                    m_Auto       = node.Auto,
                    m_Broken     = node.Broken,
                });
            }

            m_Log.Debug($"Stored {buffer.Length} path points and {rail.Length} rail points into the save.");
        }

        private void LoadPath() {
            m_PathTool.TravelPath.Clear();
            m_PathTool.RailPath.Clear();

            if (m_Query.IsEmptyIgnoreFilter) {
                return;
            }

            Entity entity = m_Query.GetSingletonEntity();
            DynamicBuffer<EPM_PathNodeData> buffer = EntityManager.GetBuffer<EPM_PathNodeData>(entity, true);

            for (int i = 0; i < buffer.Length; i++) {
                EPM_PathNodeData data = buffer[i];

                m_PathTool.TravelPath.Nodes.Add(new PathNode(data.m_Position) {
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

            m_PathTool.TravelPath.RefreshAutoTangents();

            int rails = 0;

            // Absent on any save written before the aim rail existed, which is why it is its own
            // component: nothing to version, nothing to branch on, it is simply not there.
            if (EntityManager.HasBuffer<EPM_RailNodeData>(entity)) {
                DynamicBuffer<EPM_RailNodeData> rail = EntityManager.GetBuffer<EPM_RailNodeData>(entity, true);

                for (int i = 0; i < rail.Length; i++) {
                    EPM_RailNodeData data = rail[i];

                    m_PathTool.RailPath.Nodes.Add(new PathNode(data.m_Position) {
                        TangentOut = data.m_TangentOut,
                        TangentIn  = data.m_TangentIn,
                        Auto       = data.m_Auto,
                        Broken     = data.m_Broken,
                    });
                }

                rails = rail.Length;
                m_PathTool.RailPath.RefreshAutoTangents();
            }

        }

        private Entity GetOrCreateEntity() {
            if (!m_Query.IsEmptyIgnoreFilter) {
                return m_Query.GetSingletonEntity();
            }

            Entity entity = EntityManager.CreateEntity();
            EntityManager.AddBuffer<EPM_PathNodeData>(entity);

            return entity;
        }
    }
}
