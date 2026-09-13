namespace ExtendedPhotomode.Tools {
    #region Using Statements

    using System.Collections.Generic;

    using Colossal.Mathematics;

    using ExtendedPhotomode.Camera;

    using Game.Rendering;
    using Game.Simulation;

    using Unity.Mathematics;

    using UnityEngine;

    #endregion

    /// <summary>The handles that edit a number by sliding in and out rather than by being placed.</summary>
    /// <remarks>
    /// Four values that used to exist only as panel rows: a point's height, its lens, the spacing
    /// between generated keys, and an orbit's radius. Each is a distance or a magnitude, so each has
    /// an axis it can honestly be dragged along — which is the whole reason they can leave the panel
    /// while dwell, speed and time of day cannot.
    /// </remarks>
    public partial class EPM_PathToolSystem {
        private const int kAxisHeight = 0;

        private const int kAxisLens = 1;

        private const int kAxisSpacing = 2;

        private const int kAxisRadius = 3;

        private const int kAxisBias = 4;

        /// <summary>How far out the lens grip sits at the shortest focal length.</summary>
        /// <remarks>
        /// The lens has no natural length in the world, so the axis is given one: the grip slides
        /// between these along the view direction, and the focal length is read off where it sits.
        /// Running it along the frustum's own axis is what makes the gesture mean something — you are
        /// pulling the cone longer and narrower, which is what a longer lens does.
        /// </remarks>
        private const float kLensNear = 10f;

        private const float kLensFar = 60f;

        private const float kMinFocalLength = 8f;

        private const float kMaxFocalLength = 400f;

        /// <summary>Lift applied to grips that sit on a line, so they clear it.</summary>
        private const float kGripLift = 2f;

        private readonly List<AxisHandle> m_AxisHandles = new List<AxisHandle>();

        private int m_HoveredAxis = -1;

        private int m_DraggedAxis = -1;

        /// <summary>Difference between where the grip was and where the cursor grabbed it.</summary>
        private float m_AxisGrabOffset;

        /// <summary>Cursor position and value when a steep grip was taken, for screen-space dragging.</summary>
        private Vector2 m_AxisGrabScreen;

        private float m_AxisGrabDistance;

        /// <summary>Direction a free grip is currently pulled in, so its arm follows the cursor.</summary>
        /// <remarks>
        /// Held on the tool rather than on the handle because the handles are rebuilt from their
        /// values every frame — a direction stored on one would be recomputed away the moment the
        /// grip moved, and the arm would snap back to its resting direction mid-drag.
        /// </remarks>
        private Vector3 m_FreeAxisDirection = Vector3.forward;

        /// <summary>Smoothed distance the grip is settling towards, and its velocity.</summary>
        /// <remarks>
        /// A raycast-driven grip is only as steady as the cursor, and a cursor is not steady: at a
        /// shallow camera angle a pixel of mouse movement is metres of world travel, so the raw value
        /// jitters even while the hand is still. MathUtils.SmoothDamp is critically damped, so it
        /// settles without the overshoot a lerp towards a moving target gives.
        /// </remarks>
        private float m_AxisSmoothed;

        private float m_AxisVelocity;

        /// <summary>How long the grip takes to catch up, in seconds.</summary>
        /// <remarks>
        /// Short enough that the grip still feels attached to the cursor. Any longer and the smoothing
        /// stops reading as steadiness and starts reading as lag, which is worse than the jitter.
        /// </remarks>
        private const float kAxisSmoothTime = 0.04f;

        /// <summary>Builds the axis handles that apply to whatever is being edited right now.</summary>
        /// <remarks>
        /// Rebuilt every frame rather than kept, for the same reason the shot handles are: the values
        /// they represent change under them constantly, and a handle holding a stale distance would
        /// draw in the wrong place until something happened to refresh it.
        /// </remarks>
        private void CollectAxisHandles() {
            m_AxisHandles.Clear();

            if (EditingShot) {
                CollectOrbitAxisHandles();
                return;
            }

            // Spacing and bend belong to the WHOLE PATH, not to any point, so they are collected
            // before the selection guard below. They used to sit after it, which meant drawing a
            // path and not happening to have a point selected left both invisible and unpickable —
            // gated on a condition that has nothing to do with either of them.
            // Spacing, along the path from its start. The grip sits exactly where the SECOND key
            // falls, so its distance from the origin IS one key gap, drawn to scale — drag it and you
            // are dragging where the next key lands.
            //
            // Built the way Network Tools' slope easing handles are: an axis between two real world
            // endpoints, with the value read off by projecting onto it. That replaced an earlier
            // version of this handle that slid along an invented 8..40m travel with a magic mapping at
            // each end; two real endpoints need no such constants, and the grip means something on
            // sight instead of being a dial that happens to be in the world.
            if (m_KeySamples.Count >= 3) {
                Vector3 along = PathDirectionAtStart();

                m_AxisHandles.Add(new AxisHandle {
                    Id       = kAxisSpacing,
                    Origin   = m_KeySamples[0],
                    Axis     = along,
                    Distance  = Mathf.Clamp(Mod.Instance.Settings.PathMetresPerKey, 5f, 200f),
                    Lift      = kGripLift,
                    // 5m, the step the panel row moves in.
                    Increment = 5f,
                    Hint      = PathHints.SpaceKeys,
                });

                // The curvature bias, shown as what it actually does: the tightest spacing the path
                // reaches. A 0..1 bias has no length, so a grip for it would need an invented travel
                // at each end — the very thing removed from the spacing grip. Its EFFECT does have a
                // length, and it is the same length in the same units as the grip above, measured at
                // the sharpest corner instead of at the start.
                //
                // Two grips, one reading "keys this far apart on the straights" and the other "and
                // this close through the corners", with the bias derived from the pair. Neither is a
                // dial, and the number the panel row holds never has to be looked at.
                float sharpest = Path.SharpestGlobal();

                if (sharpest >= 0f) {
                    Vector3 corner = NearestSampleTo(sharpest);
                    float   step   = Mod.Instance.Settings.PathMetresPerKey
                                   * Path.StepScaleAtGlobal(sharpest);

                    m_AxisHandles.Add(new AxisHandle {
                        Id        = kAxisBias,
                        Origin    = corner,
                        Axis      = CurveDirectionAt(sharpest),
                        Distance  = step,
                        Lift      = kGripLift,
                        Increment = 5f,
                        Hint      = PathHints.BendKeys,
                    });
                }
            }

            // Height and lens belong to the SELECTED POINT, so they appear only when there is one.
            int index = SelectedPoint;

            if (index < 0 || index >= Path.Nodes.Count) {
                return;
            }

            PathNode node = Path.Nodes[index];

            // Height, straight up from the ground under the point. The origin being the GROUND rather
            // than the point is what makes the connector show the height it is editing: the dashed
            // line is the altitude, drawn to scale.
            TerrainHeightData heights = m_TerrainSystem.GetHeightData();
            Vector3           ground  = Lift(ref heights, node.Position);

            m_AxisHandles.Add(new AxisHandle {
                Id       = kAxisHeight,
                Origin   = ground,
                Axis     = Vector3.up,
                Distance  = node.Position.y - ground.y,
                // The same step the elevation keys move in, so the two agree on what a round height is.
                Increment = Mathf.Max(Mod.Instance.Settings.PathHeightStep, 0.1f),
                Hint      = PathHints.RaisePoint,
            });

            // Lens, along the direction the camera looks from this point.
            m_AxisHandles.Add(new AxisHandle {
                Id       = kAxisLens,
                Origin   = node.Position,
                Axis     = LookDirectionAt(index),
                Distance = LensToDistance(node.Fov),
                Hint     = PathHints.SetLens,
            });

        }

        private void CollectOrbitAxisHandles() {
            if (!m_Subject.PinnedTarget.HasValue || ActiveEditor == null || !ActiveEditor.HasKeySpacing) {
                return;
            }

            Vector3 centre = m_Subject.PinnedTarget.Value;
            Setting settings = Mod.Instance.Settings;

            // Radius, out from the centre along the shot's opening bearing. The start handle can still
            // be dragged freely; this one is the same value with the sideways component removed, for
            // when you want to change ONLY how far out the camera sits.
            float   bearing   = OrbitBearing();
            Vector3 outward   = new Vector3(Mathf.Sin(bearing * Mathf.Deg2Rad), 0f,
                                            Mathf.Cos(bearing * Mathf.Deg2Rad));
            Vector3 ringLevel = centre + (Vector3.up * settings.OrbitHeight);

            m_AxisHandles.Add(new AxisHandle {
                Id       = kAxisRadius,
                Origin   = ringLevel,
                Axis     = outward,
                Distance  = settings.OrbitRadius,
                Increment = 5f,
                Hint      = PathHints.SetRadius,
            });

            if (m_ShotPreview.Count >= 3) {
                Vector3 first  = m_ShotPreview[0].Position;
                Vector3 second = m_ShotPreview[1].Position;
                Vector3 along  = second - first;

                along.y = 0f;

                // The chord between the first two keys, so the grip again sits on the key whose
                // position the value decides. Degrees rather than metres, so the distance is scaled
                // by the chord one degree subtends at this radius.
                if (along.sqrMagnitude > 0.01f) {
                    m_AxisHandles.Add(new AxisHandle {
                        Id       = kAxisSpacing,
                        Origin   = first,
                        Axis     = along.normalized,
                        Distance = along.magnitude,
                        Lift     = kGripLift,
                        Hint     = PathHints.SpaceKeys,
                    });
                }
            }
        }

        private int FindHoveredAxisHandle() {
            if (!PathPicking.TryGetMouseRay(out float3 origin, out float3 direction)) {
                return -1;
            }

            int   best    = -1;
            float nearest = float.MaxValue;

            foreach (AxisHandle handle in m_AxisHandles) {
                if (PathPicking.TryHitSphere(origin, direction, handle.Position, AxisHandle.kPickRadius,
                                             out float t) && t < nearest) {
                    nearest = t;
                    best    = handle.Id;
                }
            }

            return best;
        }

        /// <summary>Applies a drag of the grip with the given id to its value.</summary>
        private void DragAxisHandle() {
            if (!TryGetAxisHandle(m_DraggedAxis, out AxisHandle handle)) {
                return;
            }

            float distance;

            if (handle.Free) {
                // The whole distance to the cursor, wherever it is, exactly as the timeline's easing
                // handles read their weight. No grab offset: a free grip is expected to jump under
                // the cursor, which is what makes it feel like it is being held rather than nudged.
                if (!AxisHandle.TryFreeDrag(handle.Origin, out Vector3 pulled, out float reach)) {
                    return;
                }

                m_FreeAxisDirection = pulled;
                distance            = reach;
            } else if (AxisHandle.IsVertical(handle.Axis)) {
                // Height, measured as travel across the screen since the grab rather than as a
                // position read fresh each frame. Nothing on the ground can say how high 20m is, and
                // the ray-against-line answer this replaced was worthless looking level at the mast:
                // ray and axis nearly parallel, so the grip bolted to the horizon on a pixel of
                // movement. Dragging the way the mast points on screen now raises it, from anywhere.
                if (!AxisHandle.TryScreenDelta(handle.Origin, handle.Axis, m_AxisGrabScreen,
                                               out float travelled)) {
                    return;
                }

                distance = m_AxisGrabDistance + travelled;
            } else {
                if (!AxisHandle.TryDistanceAlong(handle.Origin, handle.Axis, out float raw)) {
                    return;
                }

                distance = raw - m_AxisGrabOffset;
            }

            // Smoothed before snapping, never after: smoothing a value that has already been
            // quantised would drag it slowly BETWEEN the steps it is supposed to sit on, so the grip
            // would spend most of its time off them.
            m_AxisSmoothed = MathUtils.SmoothDamp(m_AxisSmoothed, distance, ref m_AxisVelocity,
                                                  kAxisSmoothTime, float.MaxValue,
                                                  UnityEngine.Time.unscaledDeltaTime);

            // Snapped here, once, before any handle turns it into its own units — so every handle
            // gets the same behaviour and none has to remember to do it.
            distance = handle.Snap(m_AxisSmoothed);

            Setting settings = Mod.Instance.Settings;

            switch (m_DraggedAxis) {
                case kAxisHeight: {
                    int index = SelectedPoint;

                    if (index >= 0 && index < Path.Nodes.Count) {
                        AdjustHeight((handle.Origin.y + distance) - Path.Nodes[index].Position.y);
                    }

                    break;
                }

                case kAxisLens: {
                    int index = SelectedPoint;

                    if (index >= 0 && index < Path.Nodes.Count) {
                        Path.Nodes[index].Fov = DistanceToLens(distance);
                    }

                    break;
                }

                case kAxisSpacing:
                    if (EditingShot) {
                        // The grip's distance is a chord along the ring, so it converts back to an
                        // angle through the radius rather than being remapped by an invented range.
                        float radius = Mathf.Max(settings.OrbitRadius, 1f);
                        float degrees = Mathf.Clamp01(distance / (2f * radius));

                        // Snapped in DEGREES, not in the chord metres the grip actually travels.
                        // The handle's natural space is the angle: a round 5m of chord is a different
                        // number of degrees at every radius, so quantising the distance would give
                        // round positions and ragged values — the opposite of what snapping is for.
                        float angle = 2f * Mathf.Asin(degrees) * Mathf.Rad2Deg;

                        settings.OrbitDegreesPerKey =
                            Mathf.Clamp(Mathf.RoundToInt(angle / 5f) * 5, 5, 90);
                    } else {
                        // Metres between keys, straight off the axis: no mapping at all, because the
                        // handle already measures the thing in the units it is stored in.
                        settings.PathMetresPerKey = Mathf.Clamp(Mathf.RoundToInt(distance), 5, 200);
                    }

                    settings.ApplyAndSave();
                    break;

                case kAxisBias: {
                    float sharpest = Path.SharpestGlobal();

                    // A straight path has no bend to tighten, so there is no bias that would move
                    // this grip — the handle simply does nothing rather than picking a value.
                    if (sharpest >= 0f && Path.TryBiasForStep(sharpest, distance, out float bias)) {
                        Path.CurvatureBias = bias;
                        RefreshSamples();
                    }

                    break;
                }

                case kAxisRadius:
                    // Never past the origin: a negative radius is the same ring read backwards, and
                    // letting the grip cross the centre flips the shot inside out mid-drag.
                    settings.OrbitRadius = Mathf.Max(Mathf.RoundToInt(distance), 10);
                    settings.ApplyAndSave();
                    break;
            }
        }

        private bool TryGetAxisHandle(int id, out AxisHandle found) {
            foreach (AxisHandle handle in m_AxisHandles) {
                if (handle.Id == id) {
                    found = handle;
                    return true;
                }
            }

            found = default;
            return false;
        }

        private void DrawAxisHandles(ref OverlayRenderSystem.Buffer buffer) {
            foreach (AxisHandle handle in m_AxisHandles) {
                bool hot = handle.Id == m_HoveredAxis || handle.Id == m_DraggedAxis;

                handle.Draw(ref buffer, hot ? kHoverColor : kHandleColor, kStemColor);
            }
        }

        /// <summary>Maps a focal length onto the grip's distance along the view axis, and back.</summary>
        /// <remarks>
        /// Logarithmic, because focal length is: the step from 12mm to 24mm changes the shot far more
        /// than 300mm to 312mm does, so a linear grip would spend most of its travel in lenses nobody
        /// picks between. On a log scale each equal drag is an equal change in what you see.
        /// </remarks>
        private static float LensToDistance(float? focalLength) {
            float millimetres = Mathf.Clamp(focalLength ?? kDefaultFocalLength, kMinFocalLength,
                                            kMaxFocalLength);
            float t = Mathf.InverseLerp(Mathf.Log(kMinFocalLength), Mathf.Log(kMaxFocalLength),
                                        Mathf.Log(millimetres));

            return Mathf.Lerp(kLensNear, kLensFar, t);
        }

        private static float DistanceToLens(float distance) {
            float t = Mathf.Clamp01(Mathf.InverseLerp(kLensNear, kLensFar, distance));

            return Mathf.Round(Mathf.Exp(Mathf.Lerp(Mathf.Log(kMinFocalLength),
                                                    Mathf.Log(kMaxFocalLength), t)));
        }

        /// <summary>Which way the camera looks at a point, for the lens axis to run along.</summary>
        private Vector3 LookDirectionAt(int index) {
            if (m_NodeSamples.Count > index && index >= 0 &&
                m_SampleRotations.Count > m_NodeSamples[index]) {
                return Quaternion.Euler(m_SampleRotations[m_NodeSamples[index]]) * Vector3.forward;
            }

            return Vector3.forward;
        }

        /// <summary>The path's direction at a node-chain parameter, flattened.</summary>
        private Vector3 CurveDirectionAt(float global) {
            int   segment = Mathf.Clamp(Mathf.FloorToInt(global), 0, Mathf.Max(Path.SegmentCount - 1, 0));
            float t       = Mathf.Clamp01(global - segment);

            Vector3 ahead  = Path.Evaluate(segment, Mathf.Min(t + 0.02f, 1f));
            Vector3 behind = Path.Evaluate(segment, Mathf.Max(t - 0.02f, 0f));
            Vector3 along  = ahead - behind;

            along.y = 0f;

            return (along.sqrMagnitude < 0.0001f) ? Vector3.forward : along.normalized;
        }

        /// <summary>The path's own direction where it starts, for the spacing grip to run along.</summary>
        /// <remarks>
        /// Flattened, because the grip measures a horizontal gap and a steeply climbing path would
        /// otherwise put the grip well above the curve it is measuring.
        /// </remarks>
        private Vector3 PathDirectionAtStart() {
            if (m_KeySamples.Count < 2) {
                return Vector3.forward;
            }

            Vector3 forward = m_KeySamples[1] - m_KeySamples[0];

            forward.y = 0f;

            return (forward.sqrMagnitude < 0.01f) ? Vector3.forward : forward.normalized;
        }

        private float OrbitBearing() {
            return m_Subject.PinnedStartAngle ?? 0f;
        }
    }
}
