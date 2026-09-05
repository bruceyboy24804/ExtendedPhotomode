namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System.Collections.Generic;

    #endregion

    /// <summary>Undo and redo for the drawn path.</summary>
    /// <remarks>
    /// Whole-list snapshots rather than a command per edit. A path is at most a few hundred small
    /// nodes, so a snapshot costs nothing worth measuring, and it is the only approach that cannot go
    /// wrong: a command stack has to describe the inverse of every operation, and the moment one of
    /// them is described incorrectly — mirroring, resampling, a transform that also refreshes tangents
    /// — undo silently corrupts the path instead of failing.
    /// <para>
    /// Snapshots are deep copies. <see cref="PathNode"/> is a class, so keeping the list alone would
    /// keep references to the live nodes, and every stored state would follow the edits made after it.
    /// </para>
    /// </remarks>
    public class PathHistory {
        /// <summary>How many steps back the history goes before the oldest is dropped.</summary>
        private const int kMaxDepth = 64;

        private readonly List<List<PathNode>> m_Undo = new List<List<PathNode>>();
        private readonly List<List<PathNode>> m_Redo = new List<List<PathNode>>();

        public bool CanUndo => m_Undo.Count > 0;

        public bool CanRedo => m_Redo.Count > 0;

        /// <summary>Stores the path as it is now, before something changes it.</summary>
        /// <remarks>
        /// Called before the edit, not after — the stack holds states to go back TO. Recording clears
        /// the redo stack, which is the standard rule: once you branch, the future you abandoned is no
        /// longer reachable and offering to redo into it would restore a path that never existed.
        /// </remarks>
        public void Record(CameraPath path) {
            if (path == null) {
                return;
            }

            m_Undo.Add(Snapshot(path));
            m_Redo.Clear();

            if (m_Undo.Count > kMaxDepth) {
                m_Undo.RemoveAt(0);
            }
        }

        /// <summary>Steps back one edit.</summary>
        public bool Undo(CameraPath path) { return Step(path, m_Undo, m_Redo); }

        /// <summary>Steps forward one undone edit.</summary>
        public bool Redo(CameraPath path) { return Step(path, m_Redo, m_Undo); }

        /// <summary>Forgets everything, for when the path is replaced wholesale.</summary>
        /// <remarks>
        /// Loading a library path or importing from the timeline is not an edit to the current path,
        /// it is a different path — so undoing across that boundary would splice the nodes of one into
        /// the identity of the other.
        /// </remarks>
        public void Clear() {
            m_Undo.Clear();
            m_Redo.Clear();
        }

        private static bool Step(CameraPath path, List<List<PathNode>> from, List<List<PathNode>> to) {
            if (path == null || from.Count == 0) {
                return false;
            }

            // The state being left has to go on the opposite stack first, or the step is one-way.
            to.Add(Snapshot(path));

            List<PathNode> restored = from[from.Count - 1];

            from.RemoveAt(from.Count - 1);

            path.Nodes.Clear();
            path.Nodes.AddRange(restored);

            return true;
        }

        private static List<PathNode> Snapshot(CameraPath path) {
            var copy = new List<PathNode>(path.Nodes.Count);

            foreach (PathNode node in path.Nodes) {
                copy.Add(node.Clone());
            }

            return copy;
        }
    }
}
