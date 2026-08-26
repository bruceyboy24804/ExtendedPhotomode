namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Components;
    using ExtendedPhotomode.Tools;

    using Colossal.UI.Binding;

    using Game.Tools;

    using ModsCommon.Systems;

    using Unity.Collections;
    using Unity.Entities;

    #endregion

    /// <summary>One entry in the saved path library, as the UI sees it.</summary>
    public struct PathLibraryEntry {
        public int index;

        public string name;

        public int points;
    }

    /// <summary>Keeps a library of named camera paths in the city save, and exposes it to the UI.</summary>
    /// <remarks>
    /// Separate from <see cref="EPM_PathStoreSystem"/>, which persists the one path currently being
    /// drawn. That is the working copy and survives a reload on its own; this is the shelf you
    /// deliberately put a finished path on so it can be brought back later.
    /// </remarks>
    public partial class EPM_PathLibrarySystem : CommonUISystemBase {
        public const string kPathsBinding = "savedPaths";

        public const string kSaveTrigger = "savePath";

        public const string kLoadTrigger = "loadPath";

        public const string kDeleteTrigger = "deletePath";

        public const string kRenameTrigger = "renamePath";

        public const string kToolActiveBinding = "pathToolActive";

        private EPM_PathToolSystem                    m_PathTool;
        private ToolSystem                            m_ToolSystem;
        private EntityQuery                           m_Query;
        private GetterValueBinding<PathLibraryEntry[]> m_PathsBinding;

        protected override string ModId => Mod.Instance.Id;

        protected override void OnCreate() {
            base.OnCreate();
            m_PathTool    = World.GetOrCreateSystemManaged<EPM_PathToolSystem>();
            m_ToolSystem  = World.GetOrCreateSystemManaged<ToolSystem>();
            m_Query       = GetEntityQuery(ComponentType.ReadOnly<EPM_SavedPath>());

            CreateBinding(kToolActiveBinding, () => m_ToolSystem.activeTool == m_PathTool);

            m_PathsBinding = CreateBinding(kPathsBinding, BuildList, false);
            CreateTrigger<string>(kSaveTrigger, SavePath);
            CreateTrigger<int>(kLoadTrigger, LoadPath);
            CreateTrigger<int>(kDeleteTrigger, DeletePath);
            CreateTrigger<int, string>(kRenameTrigger, RenamePath);
        }

        private PathLibraryEntry[] BuildList() {
            var entries = new List<PathLibraryEntry>();

            using (NativeArray<Entity> entities = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    EPM_SavedPath saved = EntityManager.GetComponentData<EPM_SavedPath>(entity);

                    entries.Add(new PathLibraryEntry {
                        index  = saved.m_Id,
                        name   = saved.m_Name.ToString(),
                        points = EntityManager.GetBuffer<EPM_PathNodeData>(entity, true).Length,
                    });
                }
            }

            entries.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return entries.ToArray();
        }

        private void SavePath(string name) {
            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0) {
                m_Log.Warn("A saved path needs a name.");
                return;
            }

            if (m_PathTool.Path.Nodes.Count < 2) {
                m_Log.Warn("Nothing to save — draw a path with at least two points first.");
                return;
            }

            Entity entity = FindByName(trimmed);

            if (entity == Entity.Null) {
                entity = EntityManager.CreateEntity();
                EntityManager.AddBuffer<EPM_PathNodeData>(entity);
                EntityManager.AddComponentData(entity, new EPM_SavedPath {
                    m_Id   = NextId(),
                    m_Name = new FixedString128Bytes(trimmed),
                });
            }

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

            m_PathsBinding.Update();

            m_Log.Info($"Saved path \"{trimmed}\" with {buffer.Length} points.");
        }

        private void LoadPath(int id) {
            Entity entity = FindById(id);

            if (entity == Entity.Null) {
                m_Log.Warn($"No saved path with id {id}.");
                return;
            }

            DynamicBuffer<EPM_PathNodeData> buffer = EntityManager.GetBuffer<EPM_PathNodeData>(entity, true);

            m_PathTool.Path.Clear();

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
            m_Log.Info($"Loaded path with {buffer.Length} points.");
        }

        private void DeletePath(int id) {
            Entity entity = FindById(id);

            if (entity != Entity.Null) {
                EntityManager.DestroyEntity(entity);
                m_PathsBinding.Update();
                m_Log.Info($"Deleted saved path {id}.");
            }
        }

        private void RenamePath(int id, string name) {
            string trimmed = (name ?? string.Empty).Trim();
            Entity entity  = FindById(id);

            if (entity == Entity.Null || trimmed.Length == 0) {
                return;
            }

            EPM_SavedPath saved = EntityManager.GetComponentData<EPM_SavedPath>(entity);
            saved.m_Name        = new FixedString128Bytes(trimmed);

            EntityManager.SetComponentData(entity, saved);
            m_PathsBinding.Update();
        }

        private Entity FindById(int id) {
            using (NativeArray<Entity> entities = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    if (EntityManager.GetComponentData<EPM_SavedPath>(entity).m_Id == id) {
                        return entity;
                    }
                }
            }

            return Entity.Null;
        }

        private Entity FindByName(string name) {
            using (NativeArray<Entity> entities = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    EPM_SavedPath saved = EntityManager.GetComponentData<EPM_SavedPath>(entity);

                    if (string.Equals(saved.m_Name.ToString(), name, StringComparison.OrdinalIgnoreCase)) {
                        return entity;
                    }
                }
            }

            return Entity.Null;
        }

        private int NextId() {
            int highest = 0;

            using (NativeArray<Entity> entities = m_Query.ToEntityArray(Allocator.Temp)) {
                foreach (Entity entity in entities) {
                    highest = Math.Max(highest, EntityManager.GetComponentData<EPM_SavedPath>(entity).m_Id);
                }
            }

            return highest + 1;
        }
    }
}
