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

            foreach (PathNode node in m_PathTool.Path.Nodes) {
                buffer.Add(new EPM_PathNodeData {
                    m_Position   = node.Position,
                    m_TangentOut = node.TangentOut,
                    m_TangentIn  = node.TangentIn,
                    m_Auto       = node.Auto,
                    m_Broken     = node.Broken,
                });
            }

            m_Log.Debug($"Stored {buffer.Length} path points into the save.");
        }

        private void LoadPath() {
            m_PathTool.Path.Clear();

            if (m_Query.IsEmptyIgnoreFilter) {
                return;
            }

            Entity entity = m_Query.GetSingletonEntity();
            DynamicBuffer<EPM_PathNodeData> buffer = EntityManager.GetBuffer<EPM_PathNodeData>(entity, true);

            for (int i = 0; i < buffer.Length; i++) {
                EPM_PathNodeData data = buffer[i];

                m_PathTool.Path.Nodes.Add(new PathNode(data.m_Position) {
                    TangentOut = data.m_TangentOut,
                    TangentIn  = data.m_TangentIn,
                    Auto       = data.m_Auto,
                    Broken     = data.m_Broken,
                });
            }

            m_PathTool.Path.RefreshAutoTangents();

            m_Log.Info($"Restored {buffer.Length} path points from the save.");
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
