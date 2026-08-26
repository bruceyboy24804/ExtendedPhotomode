namespace ExtendedPhotomode.Systems.Generators {
    #region Using Statements

    using ExtendedPhotomode.Camera;
    using ExtendedPhotomode.Tools;

    #endregion

    /// <summary>The path drawn with the path tool, from the Path Shot settings.</summary>
    /// <remarks>
    /// Delegates to the path tool's own generate, which the Ctrl+Shift+P hotkey also calls. The button
    /// and the hotkey have to reach the same code, or the two drift the moment a path setting is
    /// added.
    /// </remarks>
    public sealed class PathShotGenerator : GenerateShotBase {
        public override ShotType Type => ShotType.Path;

        public override bool TryGenerate() {
            return World.GetOrCreateSystemManaged<EPM_PathToolToggleSystem>().GeneratePath();
        }
    }
}
