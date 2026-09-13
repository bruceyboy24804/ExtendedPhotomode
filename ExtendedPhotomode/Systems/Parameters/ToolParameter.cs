namespace ExtendedPhotomode.Systems {
    #region Using Statements

    using System;

    using ExtendedPhotomode.Camera;

    using UnityEngine;

    #endregion

    /// <summary>One tool value, declared once: where it lives, what it may be, and who it applies to.</summary>
    /// <remarks>
    /// <para>
    /// Modelled on the Network Tools mod's <c>ParameterBase</c> family, with one difference forced by
    /// where our values live. Its parameters OWN their value; ours are already stored on
    /// <see cref="Setting"/>, which the game persists and the options screen edits — so these carry a
    /// getter and setter onto that instead of a field of their own. Two homes for one value is how
    /// they drift apart.
    /// </para>
    /// <para>
    /// The point is that a range exists in exactly one place. Before this, <c>orbitRadius</c> knew its
    /// own 10..1000 in the setter switch, and the panel row repeated it in TypeScript, and the world
    /// handle that drags it had to pick a third — three copies free to disagree, and the way a drag
    /// reaches a value you cannot then type.
    /// </para>
    /// <para>
    /// Everything is a float because the UI channel that carries these is a float channel: an int is
    /// rounded on the way in, a bool is <c>&gt; 0.5</c>, an enum is its member value. Adding typed
    /// subclasses would mean teaching the wire format about types it has managed without so far.
    /// </para>
    /// </remarks>
    public sealed class ToolParameter {
        private readonly Func<float>   m_Get;
        private readonly Action<float> m_Set;

        /// <summary>Key the UI addresses this by. Must match the field name on the TS side.</summary>
        public string Key { get; }

        public float Min { get; }

        public float Max { get; }

        /// <summary>Whether the value is rounded to a whole number on the way in.</summary>
        /// <remarks>
        /// True for everything the settings store as an int, which is most of them. The eases are the
        /// exception: truncating a 0-to-1 control to whole numbers leaves it with two positions.
        /// </remarks>
        public bool Whole { get; }

        /// <summary>Which shot types this applies to, or <c>None</c> for all of them.</summary>
        /// <remarks>
        /// Network Tools tags each parameter with the modes it belongs to and lets the UI filter
        /// itself, rather than keeping a parallel list of predicates the panel has to stay aligned
        /// with. That alignment is exactly what our photo mode tab does by index today, and is the
        /// most likely reason a user reported a panel with most of its sections missing.
        /// </remarks>
        public ShotType Modes { get; }

        public ToolParameter(string key, Func<float> get, Action<float> set, float min, float max,
                             bool whole = true, ShotType modes = ShotType.None) {
            Key    = key;
            m_Get  = get;
            m_Set  = set;
            Min    = min;
            Max    = max;
            Whole  = whole;
            Modes  = modes;
        }

        /// <summary>Reads the value from wherever it actually lives.</summary>
        public float Get() { return m_Get(); }

        /// <summary>Clamps a value into range and stores it.</summary>
        /// <remarks>
        /// The clamp is here rather than at each caller, so the panel, the world handles and anything
        /// else that writes this all land on the same answer. A caller that wants to know what will
        /// happen can ask <see cref="Coerce"/> without writing.
        /// </remarks>
        public void Set(float value) { m_Set(Coerce(value)); }

        /// <summary>What <see cref="Set"/> would store for a value, without storing it.</summary>
        public float Coerce(float value) {
            float clamped = Mathf.Clamp(value, Min, Max);

            return Whole ? Mathf.Round(clamped) : clamped;
        }

        /// <summary>Whether this parameter is in play for a shot type.</summary>
        public bool AppliesTo(ShotType shot) { return Modes == ShotType.None || Modes == shot; }
    }
}
