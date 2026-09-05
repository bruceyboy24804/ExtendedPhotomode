namespace ExtendedPhotomode.Components {
    #region Using Statements

    using Colossal.Serialization.Entities;

    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;

    #endregion

    /// <summary>One shot in the sequence: what it is, how long it runs, and what it was aimed at.</summary>
    /// <remarks>
    /// <para>
    /// A shot entity is deliberately shaped like a saved path entity — a header plus the same
    /// <see cref="EPM_PathNodeData"/> and <see cref="EPM_RailNodeData"/> buffers. That is what lets a
    /// path shot carry its own drawn curves rather than depending on whatever happens to be drawn when
    /// the sequence is assembled, and it means the two features share their serialisation rather than
    /// each inventing one.
    /// </para>
    /// <para>
    /// The settings captured here are the ones that DEFINE the shot, not every setting in the mod.
    /// Snapshotting everything would freeze unrelated preferences into each shot — a change to the
    /// keyframe spacing you wanted everywhere would apply to nothing already recorded. What is stored
    /// is what makes this shot this shot.
    /// </para>
    /// <para>
    /// Order is an explicit integer rather than the entity order. Entities are recreated on load in no
    /// guaranteed sequence, and a shot list whose order depends on that is one that reshuffles itself
    /// between sessions — the same lesson the path library learned about addressing rows by index.
    /// </para>
    /// </remarks>
    public struct EPM_Shot : IComponentData, ISerializable {
        /// <summary>2 added <see cref="m_InSequence"/>.</summary>
        public const int kVersion = 2;

        public int m_Id;

        public int m_Order;

        /// <summary>Whether this shot is part of the assembled sequence, or waiting in the bin.</summary>
        /// <remarks>
        /// A shot exists whether or not it is in the cut. The timeline is a pure function of the
        /// ordered shots that ARE in it — <c>Assemble</c> regenerates every curve from them — so
        /// dropping a shot onto the timeline or pulling it back off is only ever this flag changing
        /// and the sequence being rebuilt. There is no per-shot curve surgery, and there is no state
        /// that can disagree with what is on the timeline.
        /// <para>
        /// Shots from before this field existed load as <c>true</c>: a saved list was, by definition,
        /// a list of shots meant to be assembled, and defaulting them out of the sequence would empty
        /// everyone's timeline on upgrade.
        /// </para>
        /// </remarks>
        public bool m_InSequence;

        public FixedString128Bytes m_Name;

        /// <summary>The <c>ShotType</c> this shot generates.</summary>
        public int m_Type;

        public float m_Duration;

        /// <summary>Where the shot was centred, and whether it was centred at all.</summary>
        public float3 m_Target;

        public bool m_HasTarget;

        public bool m_Closed;

        /// <summary>Seconds spent travelling from the previous shot's end pose into this one's start.</summary>
        /// <remarks>
        /// Belongs to the shot it leads INTO, not the one it leaves. A transition is part of arriving
        /// somewhere, so deleting a shot should take its own approach with it — storing it on the
        /// outgoing shot instead leaves an orphaned move to nowhere.
        /// </remarks>
        public float m_TransitionIn;

        #region Captured settings

        public int m_OrbitRadius;

        public int m_OrbitEndRadius;

        public int m_OrbitHeight;

        public int m_OrbitEndHeight;

        public int m_OrbitSweep;

        public float m_OrbitSweepEase;

        public int m_OrbitDegreesPerKey;

        public bool m_OrbitLookAtTarget;

        public int m_DollyStart;

        public int m_DollyEnd;

        public int m_DollyKeys;

        public int m_PathMetresPerKey;

        public int m_PathPitch;

        public int m_PathLookAhead;

        public float m_PathEase;

        public int m_PathLook;

        public int m_PathTerrain;

        public int m_PathClearance;

        #endregion

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter {
            writer.Write(kVersion);
            writer.Write(m_Id);
            writer.Write(m_Order);
            writer.Write(m_Name.ToString());
            writer.Write(m_Type);
            writer.Write(m_Duration);
            writer.Write(m_Target);
            writer.Write(m_HasTarget);
            writer.Write(m_Closed);
            writer.Write(m_TransitionIn);

            writer.Write(m_OrbitRadius);
            writer.Write(m_OrbitEndRadius);
            writer.Write(m_OrbitHeight);
            writer.Write(m_OrbitEndHeight);
            writer.Write(m_OrbitSweep);
            writer.Write(m_OrbitSweepEase);
            writer.Write(m_OrbitDegreesPerKey);
            writer.Write(m_OrbitLookAtTarget);

            writer.Write(m_DollyStart);
            writer.Write(m_DollyEnd);
            writer.Write(m_DollyKeys);

            writer.Write(m_PathMetresPerKey);
            writer.Write(m_PathPitch);
            writer.Write(m_PathLookAhead);
            writer.Write(m_PathEase);
            writer.Write(m_PathLook);
            writer.Write(m_PathTerrain);
            writer.Write(m_PathClearance);

            // Version 2. Appended rather than placed with m_Order where it belongs logically, because
            // no existing version's field order may change.
            writer.Write(m_InSequence);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader {
            reader.Read(out int version);
            reader.Read(out m_Id);
            reader.Read(out m_Order);

            reader.Read(out string name);
            m_Name = new FixedString128Bytes(name ?? string.Empty);

            reader.Read(out m_Type);
            reader.Read(out m_Duration);
            reader.Read(out m_Target);
            reader.Read(out m_HasTarget);
            reader.Read(out m_Closed);
            reader.Read(out m_TransitionIn);

            reader.Read(out m_OrbitRadius);
            reader.Read(out m_OrbitEndRadius);
            reader.Read(out m_OrbitHeight);
            reader.Read(out m_OrbitEndHeight);
            reader.Read(out m_OrbitSweep);
            reader.Read(out m_OrbitSweepEase);
            reader.Read(out m_OrbitDegreesPerKey);
            reader.Read(out m_OrbitLookAtTarget);

            reader.Read(out m_DollyStart);
            reader.Read(out m_DollyEnd);
            reader.Read(out m_DollyKeys);

            reader.Read(out m_PathMetresPerKey);
            reader.Read(out m_PathPitch);
            reader.Read(out m_PathLookAhead);
            reader.Read(out m_PathEase);
            reader.Read(out m_PathLook);
            reader.Read(out m_PathTerrain);
            reader.Read(out m_PathClearance);

            // Appended in version 2, and read only when the saved data actually has it. Older shots
            // default into the sequence — see m_InSequence for why that is the safe direction.
            if (version >= 2) {
                reader.Read(out m_InSequence);
            } else {
                m_InSequence = true;
            }
        }
    }
}
