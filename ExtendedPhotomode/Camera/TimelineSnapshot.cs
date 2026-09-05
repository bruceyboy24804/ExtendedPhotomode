namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System.Collections.Generic;

    using Game.CinematicCamera;

    using UnityEngine;

    #endregion

    /// <summary>One curve's worth of a <see cref="TimelineSnapshot"/>.</summary>
    /// <remarks>
    /// The keys are copied out of the <c>AnimationCurve</c> rather than the curve being held onto.
    /// <c>AnimationCurve</c> is a reference type, so keeping one would mean the snapshot mutated
    /// along with the sequence it was taken from — an undo stack of pointers to the present.
    /// </remarks>
    public struct CurveSnapshot {
        public string id;

        public float min;

        public float max;

        public Keyframe[] keys;

        public static CurveSnapshot From(CinematicCameraSequence.CinematicCameraCurveModifier modifier) {
            return new CurveSnapshot {
                id   = modifier.id,
                min  = modifier.min,
                max  = modifier.max,
                keys = modifier.curve?.keys ?? new Keyframe[0],
            };
        }

        /// <summary>Rebuilds a modifier from this snapshot.</summary>
        public CinematicCameraSequence.CinematicCameraCurveModifier ToModifier() {
            return new CinematicCameraSequence.CinematicCameraCurveModifier {
                id    = id,
                min   = min,
                max   = max,
                curve = new AnimationCurve(keys),
            };
        }
    }

    /// <summary>A whole cinematic sequence, captured so it can be restored.</summary>
    /// <remarks>
    /// A full copy rather than a diff. The edits being undone are small, but the operations that
    /// need undoing most are not — assembling a shot list rewrites every curve at once — and a diff
    /// that has to describe "all of it" is just a slower copy. Sequences are a handful of curves
    /// with tens of keys, so the whole thing is cheap.
    /// </remarks>
    public class TimelineSnapshot {
        public CurveSnapshot[] Transforms;

        public CurveSnapshot[] Modifiers;

        public float PlaybackDuration;

        public bool Loop;

        /// <summary>Copies the sequence's current state.</summary>
        public static TimelineSnapshot Capture(CinematicCameraSequence sequence) {
            if (sequence == null) {
                return null;
            }

            CurveSnapshot[] transforms = new CurveSnapshot[sequence.transforms.Length];

            for (int i = 0; i < transforms.Length; i++) {
                transforms[i] = CurveSnapshot.From(sequence.transforms[i]);
            }

            List<CinematicCameraSequence.CinematicCameraCurveModifier> source = sequence.modifiers;
            CurveSnapshot[] modifiers = new CurveSnapshot[source.Count];

            for (int i = 0; i < modifiers.Length; i++) {
                modifiers[i] = CurveSnapshot.From(source[i]);
            }

            return new TimelineSnapshot {
                Transforms       = transforms,
                Modifiers        = modifiers,
                PlaybackDuration = sequence.playbackDuration,
                Loop             = sequence.loop,
            };
        }

        /// <summary>Writes this snapshot back over a sequence.</summary>
        /// <remarks>
        /// The transform array is rebuilt at the snapshot's own length rather than assigned into the
        /// existing one. <c>Reset</c> and <c>Read</c> both replace that array wholesale, so its
        /// length is not a constant the way five fixed curves suggests.
        /// </remarks>
        public void RestoreTo(CinematicCameraSequence sequence) {
            if (sequence == null) {
                return;
            }

            CinematicCameraSequence.CinematicCameraCurveModifier[] transforms =
                new CinematicCameraSequence.CinematicCameraCurveModifier[Transforms.Length];

            for (int i = 0; i < Transforms.Length; i++) {
                transforms[i] = Transforms[i].ToModifier();
            }

            sequence.transforms = transforms;

            sequence.modifiers.Clear();

            for (int i = 0; i < Modifiers.Length; i++) {
                sequence.modifiers.Add(Modifiers[i].ToModifier());
            }

            sequence.playbackDuration = PlaybackDuration;

            // Last, because its setter runs AfterModifications when switched on, which re-runs
            // EnsureLoop and PatchRotations over the curves. Those have to be back in place first or
            // it patches the outgoing state. It self-guards against being set to what it already is.
            sequence.loop = Loop;
        }

        /// <summary>Whether two snapshots describe the same sequence.</summary>
        /// <remarks>
        /// Used to drop no-op entries. Vanilla's own <c>MoveKeyframe</c> is called on every mouse-up
        /// whether or not anything moved, so without this the undo stack fills with steps that do
        /// nothing visible — which is worse than no undo, because pressing it appears broken.
        /// </remarks>
        public bool Matches(TimelineSnapshot other) {
            if (other == null
                || other.Transforms.Length != Transforms.Length
                || other.Modifiers.Length != Modifiers.Length
                || !Mathf.Approximately(other.PlaybackDuration, PlaybackDuration)
                || other.Loop != Loop) {
                return false;
            }

            for (int i = 0; i < Transforms.Length; i++) {
                if (!SameKeys(Transforms[i], other.Transforms[i])) {
                    return false;
                }
            }

            for (int i = 0; i < Modifiers.Length; i++) {
                if (!SameKeys(Modifiers[i], other.Modifiers[i])) {
                    return false;
                }
            }

            return true;
        }

        private static bool SameKeys(CurveSnapshot a, CurveSnapshot b) {
            if (a.id != b.id || a.keys.Length != b.keys.Length) {
                return false;
            }

            for (int i = 0; i < a.keys.Length; i++) {
                Keyframe x = a.keys[i];
                Keyframe y = b.keys[i];

                if (!Mathf.Approximately(x.time, y.time)
                    || !Mathf.Approximately(x.value, y.value)
                    || !Mathf.Approximately(x.inTangent, y.inTangent)
                    || !Mathf.Approximately(x.outTangent, y.outTangent)
                    || !Mathf.Approximately(x.inWeight, y.inWeight)
                    || !Mathf.Approximately(x.outWeight, y.outWeight)) {
                    return false;
                }
            }

            return true;
        }
    }
}
