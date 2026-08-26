namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using ExtendedPhotomode.Camera;

    using ModsCommon.Utils;

    using Unity.Entities;

    #endregion

    /// <summary>
    /// Base for one kind of generated shot. A subclass is all it takes to add one.
    /// </summary>
    /// <remarks>
    /// Every generator writes into vanilla's own <see cref="Game.CinematicCamera.CinematicCameraSequence"/>
    /// through <see cref="EPM_ShotSequenceSystem"/>, so what a subclass supplies is only the geometry —
    /// playback, scrubbing and saving are vanilla's and stay that way.
    /// <para>
    /// Subclasses are discovered by reflection in <see cref="Discover"/> and indexed by
    /// <see cref="Type"/>, so adding a shot means adding a <see cref="ShotType"/> entry and a class.
    /// There is no list to remember to update, which is the whole point: the Generate button, its
    /// dropdown and its dispatch all key off the same enum.
    /// </para>
    /// </remarks>
    public abstract class GenerateShotBase {
        protected World World { get; private set; }

        protected EPM_ShotSequenceSystem Shots { get; private set; }

        protected EPM_ShotSubjectSystem Subject { get; private set; }

        protected PrefixedLogger Log { get; private set; }

        protected static Setting Settings => Mod.Instance.Settings;

        protected static bool Replaces => Settings.OrbitReplacesSequence;

        public abstract ShotType Type { get; }

        public abstract bool TryGenerate();

        public void Bind(World world) {
            World = world;
            Shots   = world.GetOrCreateSystemManaged<EPM_ShotSequenceSystem>();
            Subject = world.GetOrCreateSystemManaged<EPM_ShotSubjectSystem>();
            Log   = new PrefixedLogger(GetType().Name);
        }

        public static Dictionary<ShotType, GenerateShotBase> Discover(World world) {
            var found = new Dictionary<ShotType, GenerateShotBase>();

            IEnumerable<System.Type> types = Assembly.GetExecutingAssembly()
                                                     .GetTypes()
                                                     .Where(t => !t.IsAbstract &&
                                                                 typeof(GenerateShotBase).IsAssignableFrom(t));

            foreach (System.Type type in types) {
                if (!(Activator.CreateInstance(type) is GenerateShotBase generator)) {
                    continue;
                }

                generator.Bind(world);

                if (found.ContainsKey(generator.Type)) {
                    generator.Log.Warn($"{generator.Type} already has a generator; ignoring {type.Name}.");
                    continue;
                }

                found.Add(generator.Type, generator);
            }

            return found;
        }
    }
}
