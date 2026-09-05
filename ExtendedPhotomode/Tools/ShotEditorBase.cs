namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Systems;

    using Game.Rendering;

    using Unity.Entities;

    using UnityEngine;

    #endregion

    /// <summary>One draggable point that defines part of a shot.</summary>
    public struct ShotHandle {
        /// <summary>Identifies the handle to the editor that produced it.</summary>
        public int Id;

        public Vector3 Position;

        /// <summary>Locale key describing what dragging it does.</summary>
        public string Hint;

        /// <summary>Whether it sits on the ground, and so should be dragged across the terrain.</summary>
        public bool OnGround;
    }

    /// <summary>Direct manipulation for one kind of shot, in the world rather than in number fields.</summary>
    /// <remarks>
    /// <para>
    /// Drawn paths were always authored by clicking in the world, but orbits and dolly zooms were
    /// numbers on a panel: type a radius, generate, look, adjust, repeat. The tool already has ray
    /// picking, gizmos and camera frustums, so the only thing missing was somewhere to put the shot
    /// types that were not paths.
    /// </para>
    /// <para>
    /// Deliberately shaped like <see cref="GenerateShotBase"/> — one subclass per <see cref="ShotType"/>,
    /// found by reflection, indexed by the same enum. Adding a shot is then an enum entry, a generator
    /// and an editor, with no list anywhere to remember to update.
    /// </para>
    /// <para>
    /// Handles are the whole abstraction. A shot is a target plus a few scalars, and every one of those
    /// scalars is a distance, a bearing or a height from that target — so a handful of draggable points
    /// expresses all of them, and the editor's only real job is converting a dragged position back into
    /// the numbers the solver wants.
    /// </para>
    /// </remarks>
    public abstract class ShotEditorBase {
        protected World World { get; private set; }

        protected EPM_ShotSubjectSystem Subject { get; private set; }

        protected static Setting Settings => Mod.Instance.Settings;

        /// <summary>Which shot type this editor authors.</summary>
        public abstract ShotType Type { get; }

        /// <summary>The handles this shot currently offers, in a stable order.</summary>
        public abstract void CollectHandles(List<ShotHandle> into);

        /// <summary>Applies a handle being dragged to a new world position.</summary>
        public abstract void MoveHandle(int id, Vector3 world);

        /// <summary>Raises or lowers a handle, for the tool's height keys.</summary>
        public abstract void RaiseHandle(int id, float delta);

        /// <summary>Solves the shot for preview, so the tool can draw it the way it draws a path.</summary>
        /// <param name="into">Filled with the shot's keyframes, cleared first.</param>
        /// <returns>False when the shot is not yet complete enough to solve.</returns>
        /// <remarks>
        /// The tool owns the drawing vocabulary — the travelled line, the ground shadow, the camera
        /// frustums — because all three should look identical whichever shot type produced them. An
        /// editor supplies the geometry and adds only what is peculiar to its own shot.
        /// </remarks>
        public abstract bool TryPreview(List<CameraSample> into);

        /// <summary>Draws whatever is peculiar to this shot, beyond the travelled line.</summary>
        public abstract void Draw(ref OverlayRenderSystem.Buffer buffer);

        /// <summary>The colour this shot's travelled line is drawn in.</summary>
        public abstract Color LineColor { get; }

        /// <summary>Places the shot's target where the cursor is, for a click on empty ground.</summary>
        public virtual void PlaceTarget(Vector3 world) { Subject.PinnedTarget = world; }

        public void Bind(World world) {
            World   = world;
            Subject = world.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
        }

        /// <summary>Finds every editor in the assembly and indexes it by shot type.</summary>
        public static Dictionary<ShotType, ShotEditorBase> Discover(World world) {
            var found = new Dictionary<ShotType, ShotEditorBase>();

            IEnumerable<System.Type> types = Assembly.GetExecutingAssembly()
                                                     .GetTypes()
                                                     .Where(t => !t.IsAbstract &&
                                                                 typeof(ShotEditorBase).IsAssignableFrom(t));

            foreach (System.Type type in types) {
                var editor = (ShotEditorBase)System.Activator.CreateInstance(type);

                editor.Bind(world);
                found[editor.Type] = editor;
            }

            return found;
        }

        /// <summary>The point at a bearing and distance from a centre, at a given height above it.</summary>
        /// <remarks>
        /// Bearing is measured the way the solvers measure it — clockwise from north — so a handle
        /// placed here lands exactly where the generated shot will start, rather than mirrored.
        /// </remarks>
        protected static Vector3 Ring(Vector3 centre, float bearing, float radius, float height) {
            float radians = bearing * Mathf.Deg2Rad;

            return new Vector3(centre.x + Mathf.Sin(radians) * radius, centre.y + height,
                               centre.z + Mathf.Cos(radians) * radius);
        }

        /// <summary>Splits a world point into its bearing and horizontal distance from a centre.</summary>
        protected static void Polar(Vector3 centre, Vector3 world, out float bearing, out float radius) {
            Vector3 delta = world - centre;

            bearing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            radius  = new Vector2(delta.x, delta.z).magnitude;
        }
    }
}
