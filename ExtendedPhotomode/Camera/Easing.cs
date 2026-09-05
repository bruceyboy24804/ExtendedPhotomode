namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using UnityEngine;

    #endregion

    /// <summary>
    /// Reshapes a 0-to-1 progress value so a move slows at both ends.
    /// </summary>
    /// <remarks>
    /// Shared by the orbit's sweep easing and the path's, so the two feel the same at the same
    /// strength. Blended rather than switched: how much to slow down is a look, not a fact, and at 0
    /// the result is exactly the unshaped input.
    /// </remarks>
    public static class Easing {
        /// <summary>Blends <paramref name="u"/> towards a smoothstep by <paramref name="amount"/>.</summary>
        /// <param name="u">Progress, 0 to 1.</param>
        /// <param name="amount">How far to shape it, 0 (linear) to 1 (full smoothstep).</param>
        public static float Blend(float u, float amount) {
            u = Mathf.Clamp01(u);

            return Mathf.Lerp(u, u * u * (3f - 2f * u), Mathf.Clamp01(amount));
        }
    }
}
