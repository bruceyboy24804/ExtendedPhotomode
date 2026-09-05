namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System.Collections.Generic;

    using UnityEngine;

    #endregion

    /// <summary>How the camera is aimed as it travels a path.</summary>
    /// <remarks>
    /// Shaped like <c>UnityEngine.Camera.GateFitMode</c> — see <see cref="KeyframeEase"/> for why the
    /// zero-valued <see cref="None"/> is there and must stay first.
    /// </remarks>
    public enum PathLookMode {
        None = 0,

        Forward = 1,

        Fixed = 2,

        Target = 3,

        /// <summary>Aim at the matching point on a second drawn path.</summary>
        Rail = 4,
    }

    /// <summary>A camera move defined by control points the user places in the world, flown as a smooth curve.</summary>
    /// <remarks>
    /// A cubic Bezier chain through the control points, with per-node tangent handles; left on auto
    /// they reproduce a uniform Catmull-Rom spline exactly.
    ///
    /// Sampling is by ARC LENGTH, not by curve parameter. Stepping the parameter uniformly bunches
    /// samples where the curve bends, which makes the camera change speed for no visible reason.
    /// </remarks>
    public class CameraPath {
        private const int kLengthSamplesPerSegment = 16;

        public const float kMinMetresPerKey = 1f;

        public const float kDefaultMetresPerKey = 25f;

        /// <summary>How far before and after an obstruction the climb over it is spread, in metres.</summary>
        private const float kClearanceRamp = 60f;

        public List<PathNode> Nodes { get; } = new List<PathNode>();

        public float Duration { get; set; } = 30f;

        public float MetresPerKey { get; set; } = kDefaultMetresPerKey;

        public PathLookMode LookMode { get; set; } = PathLookMode.Forward;

        public float Pitch { get; set; }

        public float FixedYaw { get; set; }

        public Vector3 Target { get; set; }

        /// <summary>The path this one aims at, when <see cref="LookMode"/> is <c>Rail</c>.</summary>
        /// <remarks>
        /// The professional two-rail camera rig: one path for the body, a second for the focus, flown
        /// in parallel. It is a whole <see cref="CameraPath"/> rather than a bare list of points so the
        /// aim travels a smooth curve like the camera does — a rail sampled as straight segments makes
        /// the aim snap direction at every one of its points, which is visible as a twitch even when
        /// the camera itself is moving smoothly.
        /// </remarks>
        public CameraPath Rail { get; set; }

        /// <summary>Gets or sets how far ahead along the path the camera aims, in metres.</summary>
        /// <remarks>
        /// Zero aims at the neighbouring samples, which is what makes a tight curve read as jittery:
        /// the aim swings by the full turn between one pair of samples and the next. Aiming further
        /// along averages that out, the way a driver looks into a bend rather than at the bonnet.
        /// </remarks>
        public float LookAhead { get; set; }

        /// <summary>Gets or sets how strongly the whole move eases in and out, 0 to 1.</summary>
        public float Ease { get; set; }

        /// <summary>Gets or sets whether the last point joins back to the first.</summary>
        /// <remarks>
        /// Adds one more segment rather than duplicating a node, so the join is a real curve segment
        /// with continuous tangents — placing a final point on top of the first instead leaves a
        /// visible kink, because the two end nodes each get an end node's half-length tangent.
        /// </remarks>
        public bool Closed { get; set; }

        /// <summary>Gets or sets how the sampled heights relate to the ground.</summary>
        public PathTerrainMode TerrainMode { get; set; } = PathTerrainMode.Free;

        /// <summary>Gets or sets the height above ground the terrain modes keep, in metres.</summary>
        public float TerrainClearance { get; set; } = 20f;

        /// <summary>Gets or sets what the path does about objects standing in its way.</summary>
        public PathClearanceMode ClearanceMode { get; set; } = PathClearanceMode.Off;

        /// <summary>Gets or sets how far above an obstruction the path is lifted, in metres.</summary>
        public float ObstacleClearance { get; set; } = 15f;

        /// <summary>Gets or sets how obstruction heights are measured, or null to skip the test.</summary>
        /// <remarks>
        /// Injected for the same reason <see cref="GroundHeight"/> is: this class holds no ECS, and the
        /// object quadtree lives in a system. Takes the whole sample list at once rather than a point
        /// at a time so the implementation can make one traversal instead of hundreds.
        /// </remarks>
        public System.Action<IReadOnlyList<Vector3>, List<float>> ObstacleHeights { get; set; }

        /// <summary>Which samples were obstructed on the last solve, for the tool to draw.</summary>
        public List<bool> Obstructed { get; } = new List<bool>();

        /// <summary>Gets or sets how ground height is read, or null to leave heights alone.</summary>
        /// <remarks>
        /// Injected rather than looked up. This class is deliberately free of ECS — it is solved in
        /// tests and from the tool alike — so the one thing it cannot do for itself is sample terrain.
        /// A null delegate disables <see cref="TerrainMode"/> entirely rather than throwing.
        /// </remarks>
        public System.Func<Vector3, float> GroundHeight { get; set; }

        /// <summary>How many curve segments the path has; one more than the gaps when it is closed.</summary>
        public int SegmentCount => Closed ? Nodes.Count : Nodes.Count - 1;

        // A closed path needs three points to enclose anything; two would double back on themselves.
        public bool IsValid => Nodes.Count >= (Closed ? 3 : 2);

        public void Clear() { Nodes.Clear(); }

        /// <summary>The average of the control points — what the whole-path transforms pivot about.</summary>
        /// <remarks>
        /// The centroid rather than the middle of the bounding box, so a long tail of closely spaced
        /// points pulls the pivot towards the part of the path that actually has detail in it. Rotating
        /// about a bounding-box centre swings a lopsided path much further than it looks like it should.
        /// </remarks>
        public Vector3 Centre {
            get {
                if (Nodes.Count == 0) {
                    return Vector3.zero;
                }

                Vector3 sum = Vector3.zero;

                foreach (PathNode node in Nodes) {
                    sum += node.Position;
                }

                return sum / Nodes.Count;
            }
        }

        /// <summary>Moves the whole path, keeping its shape.</summary>
        /// <remarks>
        /// Per-point look-at targets travel with it, and so does everything else that is a world point.
        /// A path is a composition, not just a line: moving the move but leaving the subjects it frames
        /// behind produces a shot that aims at nothing.
        /// </remarks>
        public void Translate(Vector3 delta) {
            foreach (PathNode node in Nodes) {
                node.Position += delta;

                if (node.LookAt.HasValue) {
                    node.LookAt = node.LookAt.Value + delta;
                }
            }
        }

        /// <summary>Turns the whole path about its centre, in degrees around the world Y axis.</summary>
        public void Rotate(float degrees) {
            Vector3    centre = Centre;
            Quaternion turn   = Quaternion.Euler(0f, degrees, 0f);

            foreach (PathNode node in Nodes) {
                node.Position = centre + turn * (node.Position - centre);

                // Tangents are offsets from the point, so they rotate but do not translate. Leaving
                // them alone would keep every curve bending the way the old orientation did.
                node.TangentOut = turn * node.TangentOut;
                node.TangentIn  = turn * node.TangentIn;

                if (node.LookAt.HasValue) {
                    node.LookAt = centre + turn * (node.LookAt.Value - centre);
                }
            }
        }

        /// <summary>Grows or shrinks the path about its centre, in plan only.</summary>
        /// <remarks>
        /// Deliberately XZ, leaving height untouched. A uniform scale would drag the whole path towards
        /// or away from the ground as it resized, so a path drawn at a good altitude stops being at one
        /// the moment it is made wider — which is never what "make this bigger" means here.
        /// </remarks>
        public void Scale(float factor) {
            if (factor <= 0f) {
                return;
            }

            Vector3 centre = Centre;

            foreach (PathNode node in Nodes) {
                node.Position   = ScaleAbout(node.Position, centre, factor);
                node.TangentOut = ScaleFlat(node.TangentOut, factor);
                node.TangentIn  = ScaleFlat(node.TangentIn, factor);

                if (node.LookAt.HasValue) {
                    node.LookAt = ScaleAbout(node.LookAt.Value, centre, factor);
                }
            }
        }

        /// <summary>Flips the path about its centre, across the X or the Z axis.</summary>
        /// <param name="acrossX">True to mirror the X coordinates, false to mirror Z.</param>
        public void Mirror(bool acrossX) {
            Vector3 centre = Centre;

            foreach (PathNode node in Nodes) {
                node.Position   = MirrorAbout(node.Position, centre, acrossX);
                node.TangentOut = MirrorFlat(node.TangentOut, acrossX);
                node.TangentIn  = MirrorFlat(node.TangentIn, acrossX);

                if (node.LookAt.HasValue) {
                    node.LookAt = MirrorAbout(node.LookAt.Value, centre, acrossX);
                }
            }
        }

        /// <summary>Adds a point at the middle of every segment, doubling the control resolution.</summary>
        /// <remarks>
        /// The new points land ON the existing curve, at the parameter midpoint, so subdividing does
        /// not change the shape at all — it only gives you more places to grab. Tangents are left to
        /// the auto solver afterwards, which reproduces the same curve for interior points.
        /// </remarks>
        public void Subdivide() {
            if (!IsValid) {
                return;
            }

            var grown = new List<PathNode>(Nodes.Count * 2);

            for (int segment = 0; segment < SegmentCount; segment++) {
                grown.Add(Nodes[segment]);
                grown.Add(new PathNode(Evaluate(segment, 0.5f)));
            }

            // An open path's last node begins no segment, so the loop above never reaches it.
            if (!Closed) {
                grown.Add(Nodes[Nodes.Count - 1]);
            }

            Nodes.Clear();
            Nodes.AddRange(grown);
            RefreshAutoTangents();
        }

        /// <summary>Drops points that the curve barely needs, within a tolerance in metres.</summary>
        /// <remarks>
        /// A point is redundant when removing it moves the line through its neighbours by less than the
        /// tolerance — the standard perpendicular-distance test. Deliberately measured against the
        /// straight chord rather than the curve: it is a coarse pass meant to thin an over-clicked path
        /// or an import that produced a node per keyframe, not a shape-preserving decimation.
        /// <para>
        /// Points carrying properties of their own are never dropped. Losing a point loses its dwell,
        /// speed, lens and target with it, and that is authored work rather than incidental geometry.
        /// </para>
        /// </remarks>
        public int Simplify(float tolerance) {
            if (Nodes.Count < 3) {
                return 0;
            }

            int removed = 0;

            for (int i = Nodes.Count - 2; i > 0; i--) {
                PathNode node = Nodes[i];

                if (node.Dwell > 0f || node.LookAt.HasValue || node.Fov.HasValue ||
                    node.TimeOfDay.HasValue || node.Pitch.HasValue || node.Broken ||
                    !Mathf.Approximately(node.Speed, 1f)) {
                    continue;
                }

                if (DistanceToChord(Nodes[i - 1].Position, Nodes[i + 1].Position, node.Position)
                    > tolerance) {
                    continue;
                }

                Nodes.RemoveAt(i);
                removed++;
            }

            if (removed > 0) {
                RefreshAutoTangents();
            }

            return removed;
        }

        /// <summary>Respaces the points evenly along the curve, keeping the shape and the count.</summary>
        /// <remarks>
        /// Positions are resampled by ARC LENGTH, so points end up genuinely equidistant rather than
        /// equally spaced in curve parameter — the same distinction that makes the solver sample the
        /// way it does. Per-point properties travel with their index, which is what makes this safe to
        /// run on an authored path: point three keeps its dwell, it simply sits somewhere tidier.
        /// </remarks>
        public void Respace() {
            if (!IsValid) {
                return;
            }

            int count = Nodes.Count;

            List<Vector3> even = SamplePositions(Mathf.Max(MeasureLength() / Mathf.Max(count - 1, 1),
                                                           kMinMetresPerKey));

            if (even.Count < 2) {
                return;
            }

            for (int i = 0; i < count; i++) {
                float t     = (count > 1) ? (float)i / (count - 1) : 0f;
                int   index = Mathf.Clamp(Mathf.RoundToInt(t * (even.Count - 1)), 0, even.Count - 1);

                Nodes[i].Position = even[index];
                Nodes[i].Auto     = true;
            }

            RefreshAutoTangents();
        }

        /// <summary>How fast the camera travels on average, in metres per second.</summary>
        /// <remarks>
        /// Worth showing before generating. A hundred metres in two seconds is 180km/h, which reads as
        /// a mistake on screen, and nothing else in the tool tells you that until you watch it.
        /// </remarks>
        public float AverageSpeed => (Duration > 0.01f) ? MeasureLength() / Duration : 0f;

        private static float DistanceToChord(Vector3 from, Vector3 to, Vector3 point) {
            Vector3 line   = to - from;
            float   lengthSq = line.sqrMagnitude;

            if (lengthSq < 0.0001f) {
                return Vector3.Distance(point, from);
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - from, line) / lengthSq);
            return Vector3.Distance(point, from + line * t);
        }

        private static Vector3 ScaleAbout(Vector3 point, Vector3 centre, float factor) {
            return new Vector3(centre.x + (point.x - centre.x) * factor, point.y,
                               centre.z + (point.z - centre.z) * factor);
        }

        private static Vector3 ScaleFlat(Vector3 offset, float factor) {
            return new Vector3(offset.x * factor, offset.y, offset.z * factor);
        }

        private static Vector3 MirrorAbout(Vector3 point, Vector3 centre, bool acrossX) {
            return acrossX ? new Vector3(2f * centre.x - point.x, point.y, point.z)
                           : new Vector3(point.x, point.y, 2f * centre.z - point.z);
        }

        private static Vector3 MirrorFlat(Vector3 offset, bool acrossX) {
            return acrossX ? new Vector3(-offset.x, offset.y, offset.z)
                           : new Vector3(offset.x, offset.y, -offset.z);
        }

        public (Vector3 a, Vector3 b, Vector3 c, Vector3 d) GetSegment(int segment) {
            PathNode from = Nodes[segment];
            PathNode to   = Nodes[(segment + 1) % Nodes.Count];

            return (from.Position, from.HandleOut, to.HandleIn, to.Position);
        }

        public Vector3 Evaluate(int segment, float t) {
            (Vector3 a, Vector3 b, Vector3 c, Vector3 d) = GetSegment(segment);
            return Bezier(a, b, c, d, t);
        }

        public void RefreshAutoTangents() {
            int count = Nodes.Count;
            int last  = count - 1;

            for (int i = 0; i <= last; i++) {
                PathNode node = Nodes[i];

                if (!node.Auto) {
                    continue;
                }

                // On a closed path the ends have real neighbours on both sides, so they take the same
                // full-length tangent as any interior point. Handing them an end node's halved tangent
                // is exactly what puts a kink at the join.
                bool ends = !Closed && (i == 0 || i == last);

                Vector3 before = Closed ? Nodes[(i - 1 + count) % count].Position
                                        : Nodes[Mathf.Max(i - 1, 0)].Position;
                Vector3 after = Closed ? Nodes[(i + 1) % count].Position
                                       : Nodes[Mathf.Min(i + 1, last)].Position;

                Vector3 out_ = (after - before) / (ends ? 3f : 6f);

                node.TangentOut = out_;
                node.TangentIn  = -out_;
            }
        }

        public float MeasureLength() {
            if (!IsValid) {
                return 0f;
            }

            float   length   = 0f;
            Vector3 previous = Nodes[0].Position;

            for (int segment = 0; segment < SegmentCount; segment++) {
                for (int step = 1; step <= kLengthSamplesPerSegment; step++) {
                    Vector3 current = Evaluate(segment, (float)step / kLengthSamplesPerSegment);
                    length  += Vector3.Distance(previous, current);
                    previous = current;
                }
            }

            return length;
        }

        public List<CameraSample> Solve() { return Solve(out _, out _); }

        /// <summary>Solves the path, and reports the per-sample lens and light values alongside it.</summary>
        /// <param name="focalLengths">Focal length per sample, or NaN where no point asked for one.</param>
        /// <param name="hours">Time of day per sample, or NaN where no point asked for one.</param>
        /// <remarks>
        /// Reported separately rather than added to <see cref="CameraSample"/>, because those two ride
        /// the timeline as modifier curves on vanilla's own properties rather than as camera transform
        /// keys — and the orbit and dolly generators share the sample type without wanting either.
        /// </remarks>
        public List<CameraSample> Solve(out List<float> focalLengths, out List<float> hours) {
            var samples = new List<CameraSample>();

            focalLengths = new List<float>();
            hours        = new List<float>();

            if (!IsValid) {
                return samples;
            }

            List<Vector3> positions = SamplePositions(MetresPerKey, out List<float> globals);

            ClearObstacles(positions);

            int     last  = positions.Count - 1;
            int[]   holds = FindHoldSamples(globals);
            float         waited    = 0f;
            float[]       pace      = BuildPace(globals);

            for (int i = 0; i < positions.Count; i++) {
                // Easing shapes the pace the speeds produced, so the two compose: a point marked slow
                // stays proportionally slow, and the whole move still eases at its ends.
                float f    = Easing.Blend(pace[i], Ease);
                float time = Duration * f + waited;

                focalLengths.Add(BlendOptional(globals[i], node => node.Fov));
                hours.Add(BlendOptional(globals[i], node => node.TimeOfDay));

                var sample = new CameraSample {
                    Time     = time,
                    Position = positions[i],
                    Rotation = SolveRotation(positions, globals, i),
                };

                samples.Add(sample);

                float dwell = (holds[i] >= 0) ? Mathf.Max(Nodes[holds[i]].Dwell, 0f) : 0f;

                if (dwell <= 0f) {
                    continue;
                }

                // A hold is the same pose twice, that many seconds apart. Identical neighbours give the
                // smoothed tangents nothing to slope towards, so the camera genuinely stops.
                sample.Time = time + dwell;
                samples.Add(sample);
                waited += dwell;

                // The lens and light lists are read by index against samples, so a hold has to append
                // to them too or every entry past the first dwelling point lines up with the wrong key.
                focalLengths.Add(focalLengths[focalLengths.Count - 1]);
                hours.Add(hours[hours.Count - 1]);
            }

            Unwrap(samples);
            return samples;
        }

        /// <summary>
        /// Normalised time per sample, weighted by the speed the points ask for.
        /// </summary>
        /// <remarks>
        /// Each step costs 1/speed, so a slow stretch consumes more of the shot and a fast one less.
        /// Normalising by the total is what keeps Duration meaning what it says — the speeds are
        /// relative to each other, not absolute, so marking every point 0.5 changes nothing.
        /// </remarks>
        private float[] BuildPace(List<float> globals) {
            var pace = new float[globals.Count];

            if (globals.Count < 2) {
                return pace;
            }

            float total = 0f;

            for (int i = 1; i < globals.Count; i++) {
                float speed = Mathf.Max(SpeedAt((globals[i] + globals[i - 1]) * 0.5f), 0.01f);

                total  += 1f / speed;
                pace[i] = total;
            }

            if (total <= 0f) {
                return pace;
            }

            for (int i = 0; i < pace.Length; i++) {
                pace[i] /= total;
            }

            return pace;
        }

        /// <summary>Which two nodes a place on the node chain sits between, and how far along it is.</summary>
        /// <remarks>
        /// Every per-node property blends through here, so wrapping is solved once. An open path clamps
        /// at both ends — past the last node there is no "after" to reach for — while a closed one wraps,
        /// since global runs to <c>Nodes.Count</c> there and the node after the last really is the first.
        /// </remarks>
        private void Neighbours(float global, out int before, out int after, out float t) {
            int count = Nodes.Count;

            if (Closed) {
                int floor = Mathf.FloorToInt(global);

                before = ((floor % count) + count) % count;
                after  = (before + 1) % count;
                t      = global - floor;
                return;
            }

            before = Mathf.Clamp(Mathf.FloorToInt(global), 0, count - 1);
            after  = Mathf.Clamp(before + 1, 0, count - 1);
            t      = global - before;
        }

        private float SpeedAt(float global) {
            Neighbours(global, out int before, out int after, out float t);
            return Mathf.Lerp(Nodes[before].Speed, Nodes[after].Speed, t);
        }

        /// <summary>
        /// Blends a property that only some points set, returning NaN where none nearby do.
        /// </summary>
        /// <remarks>
        /// A point that leaves the value unset takes its neighbour's rather than snapping the lens or
        /// the light back to a default halfway along — so setting it on two points ramps between them,
        /// and setting it on one holds it for the whole shot.
        /// </remarks>
        private float BlendOptional(float global, System.Func<PathNode, float?> read) {
            Neighbours(global, out int before, out int after, out float t);

            float? a = read(Nodes[before]);
            float? b = read(Nodes[after]);

            if (!a.HasValue && !b.HasValue) {
                return float.NaN;
            }

            if (!a.HasValue) {
                return b.Value;
            }

            if (!b.HasValue) {
                return a.Value;
            }

            return Mathf.Lerp(a.Value, b.Value, t);
        }

        // Which node, if any, each sample is the closest one to. -1 for the rest.
        private int[] FindHoldSamples(List<float> globals) {
            var holds = new int[globals.Count];

            for (int i = 0; i < holds.Length; i++) {
                holds[i] = -1;
            }

            for (int node = 0; node < Nodes.Count; node++) {
                if (Nodes[node].Dwell <= 0f) {
                    continue;
                }

                int   best     = -1;
                float distance = float.MaxValue;

                for (int i = 0; i < globals.Count; i++) {
                    // On a closed path the first node is at both ends of the global range, so a dwell
                    // on it would otherwise only ever be found at the start.
                    float delta = Mathf.Abs(globals[i] - node);

                    if (Closed) {
                        delta = Mathf.Min(delta, Mathf.Abs(globals[i] - (node + Nodes.Count)));
                    }

                    if (delta < distance) {
                        distance = delta;
                        best     = i;
                    }
                }

                if (best >= 0) {
                    holds[best] = node;
                }
            }

            return holds;
        }

        // The pitch to hold at a point on the node chain, blended between its neighbouring nodes so a
        // pitch change tilts across the segment instead of snapping at the node.
        private float PitchAt(float global) {
            Neighbours(global, out int before, out int after, out float t);

            return Mathf.Lerp(Nodes[before].Pitch ?? Pitch, Nodes[after].Pitch ?? Pitch, t);
        }

        /// <summary>The point a given fraction of the way along the node chain.</summary>
        /// <param name="progress">0 at the first node, 1 at the last — or back at the first when closed.</param>
        public Vector3 PositionAtProgress(float progress) {
            if (Nodes.Count == 0) {
                return Vector3.zero;
            }

            if (!IsValid) {
                return Nodes[0].Position;
            }

            float global  = Mathf.Clamp01(progress) * SegmentCount;
            int   segment = Mathf.Min((int)global, SegmentCount - 1);

            return Evaluate(segment, global - segment);
        }

        public List<Vector3> SamplePositions(float metresPerSample) {
            return SamplePositions(metresPerSample, out _);
        }

        /// <summary>Samples the path, reporting where along the node chain each sample fell.</summary>
        /// <param name="metresPerSample">Roughly how far apart samples should be.</param>
        /// <param name="globals">Node-chain position per sample: 0 at the first node, 1 at the second.</param>
        /// <remarks>
        /// The global parameter is what lets per-node properties reach the solver at all. Samples are
        /// spaced by arc length and do not land on nodes, so "which node is this sample near" has no
        /// answer without carrying the parameter back out.
        /// </remarks>
        public List<Vector3> SamplePositions(float metresPerSample, out List<float> globals) {
            float spacing = Mathf.Max(metresPerSample, kMinMetresPerKey);
            float length  = MeasureLength();
            int   count   = Mathf.Max(2, Mathf.CeilToInt(length / spacing) + 1);

            var positions = new List<Vector3>(count);
            int segments  = SegmentCount;

            globals = new List<float>(count);

            for (int i = 0; i < count; i++) {
                float global  = (float)i / (count - 1) * segments;
                int   segment = Mathf.Min((int)global, segments - 1);

                globals.Add(global);
                positions.Add(ClampToTerrain(Evaluate(segment, global - segment)));
            }

            return positions;
        }

        /// <summary>Lifts the path over anything standing in its way, and records where it had to.</summary>
        /// <remarks>
        /// The naive fix — raise each obstructed sample to clear what it hit — produces a staircase.
        /// Each sample jumps the moment it crosses a building's edge and drops again at the far side,
        /// so the camera lurches over every rooftop and the result reads worse than the collision did.
        /// <para>
        /// So the required height is computed first for every sample, then spread outwards: a sample's
        /// final height is the highest requirement within a falling-off window either side of it. That
        /// makes the camera begin climbing BEFORE the tower and finish descending after it, which is
        /// both what a real aerial does and what stops the lift being visible as a correction.
        /// </para>
        /// <para>
        /// Warn mode runs the same measurement and records the result without moving anything, so the
        /// tool can colour the offending stretch and leave the fix to a human.
        /// </para>
        /// </remarks>
        private void ClearObstacles(List<Vector3> positions) {
            Obstructed.Clear();

            if (ObstacleHeights == null || ClearanceMode == PathClearanceMode.Off ||
                ClearanceMode == PathClearanceMode.None || positions.Count == 0) {
                return;
            }

            var tops = new List<float>(positions.Count);

            ObstacleHeights(positions, tops);

            if (tops.Count != positions.Count) {
                return;
            }

            var required = new float[positions.Count];

            for (int i = 0; i < positions.Count; i++) {
                float needed = tops[i] + ObstacleClearance;
                bool  hit    = tops[i] > float.MinValue && positions[i].y < needed;

                Obstructed.Add(hit);
                required[i] = hit ? needed : float.MinValue;
            }

            if (ClearanceMode != PathClearanceMode.Lift) {
                return;
            }

            // The ramp is measured in samples because samples are arc-length spaced, so a fixed count
            // is a fixed distance — the run-up scales with the path rather than with its point count.
            int reach = Mathf.Max(1, Mathf.CeilToInt(kClearanceRamp /
                                                     Mathf.Max(MetresPerKey, kMinMetresPerKey)));

            for (int i = 0; i < positions.Count; i++) {
                float lift = float.MinValue;

                for (int j = Mathf.Max(i - reach, 0); j <= Mathf.Min(i + reach, positions.Count - 1); j++) {
                    if (required[j] <= float.MinValue) {
                        continue;
                    }

                    // Falls off with distance, so the climb eases in rather than stepping up at the
                    // edge of the window.
                    float falloff = 1f - (Mathf.Abs(j - i) / (float)(reach + 1));

                    lift = Mathf.Max(lift, Mathf.Lerp(positions[i].y, required[j], falloff));
                }

                if (lift > positions[i].y) {
                    Vector3 raised = positions[i];

                    raised.y     = lift;
                    positions[i] = raised;
                }
            }
        }

        /// <summary>Lifts a sampled position clear of the ground, according to <see cref="TerrainMode"/>.</summary>
        /// <remarks>
        /// Applied to the samples rather than to the control points, because the points are already
        /// placed relative to the ground — it is the curve *between* them that dives into a ridge or
        /// sails over a valley, and the samples are the only place that shape is visible.
        /// <para>
        /// <see cref="MeasureLength"/> deliberately does not clamp. It only decides how many samples to
        /// take, so a length that ignores the terrain costs a slightly different keyframe count and
        /// nothing else — while clamping there would mean sampling the curve twice on every solve.
        /// </para>
        /// </remarks>
        private Vector3 ClampToTerrain(Vector3 position) {
            if (GroundHeight == null || TerrainMode == PathTerrainMode.Free ||
                TerrainMode == PathTerrainMode.None) {
                return position;
            }

            float floor = GroundHeight(position) + TerrainClearance;

            position.y = (TerrainMode == PathTerrainMode.Follow) ? floor : Mathf.Max(position.y, floor);
            return position;
        }

        /// <summary>The aim at every sample, for drawing the camera along the path without solving a shot.</summary>
        /// <remarks>
        /// Shares <see cref="SolveRotation"/> with the solver rather than approximating it, so a frustum
        /// gizmo shows the rotation the generated shot will genuinely use — including look-ahead, the
        /// aim mode, per-point pitch and per-point look-at targets. A separate approximation here would
        /// be a drawing that lies about the shot.
        /// </remarks>
        public List<Vector3> SolveRotations(List<Vector3> positions, List<float> globals) {
            var rotations = new List<Vector3>(positions.Count);

            for (int i = 0; i < positions.Count; i++) {
                rotations.Add(SolveRotation(positions, globals, i));
            }

            return rotations;
        }

        private Vector3 SolveRotation(List<Vector3> positions, List<float> globals, int index) {
            float pitch = PitchAt(globals[index]);

            // A point's own look-at wins over the path's aim mode, so a single point can break out of
            // Forward to catch something as the camera passes it.
            if (TryLookAtAt(globals[index], out Vector3 target)) {
                return AimAt(positions[index], target);
            }

            if (LookMode == PathLookMode.Fixed) {
                return new Vector3(pitch, FixedYaw, 0f);
            }

            if (LookMode == PathLookMode.Target) {
                return AimAt(positions[index], Target);
            }

            if (LookMode == PathLookMode.Rail && Rail != null && Rail.IsValid) {
                // Matched by fraction of the way along, not by distance travelled or by point index.
                // The two paths are rarely the same length or the same point count, and what a rig
                // means by "the camera is here, so look there" is proportional progress: the start of
                // the rail belongs to the start of the move, and the end to the end.
                float progress = (SegmentCount > 0) ? globals[index] / SegmentCount : 0f;

                return AimAt(positions[index], Rail.PositionAtProgress(progress));
            }

            // Samples are spaced by arc length, so a look-ahead distance converts to a sample count
            // by dividing by that spacing — no need to walk the curve again.
            int reach = Mathf.Max(1, Mathf.RoundToInt(LookAhead / Mathf.Max(MetresPerKey, kMinMetresPerKey)));

            int     before = Mathf.Max(index - reach, 0);
            int     after  = Mathf.Min(index + reach, positions.Count - 1);
            Vector3 travel = positions[after] - positions[before];

            if (travel.sqrMagnitude < 0.0001f) {
                return new Vector3(pitch, FixedYaw, 0f);
            }

            return new Vector3(pitch, Mathf.Atan2(travel.x, travel.z) * Mathf.Rad2Deg, 0f);
        }

        // The aim point at a place on the node chain, blended so the camera swings between two
        // subjects across the segment instead of snapping at the node.
        private bool TryLookAtAt(float global, out Vector3 target) {
            Neighbours(global, out int before, out int after, out float t);

            Vector3? a = Nodes[before].LookAt;
            Vector3? b = Nodes[after].LookAt;

            if (!a.HasValue && !b.HasValue) {
                target = Vector3.zero;
                return false;
            }

            target = Vector3.Lerp(a ?? b.Value, b ?? a.Value, t);
            return true;
        }

        private Vector3 AimAt(Vector3 from, Vector3 lookAt) {
            return CameraAim.Euler(from, lookAt, new Vector3(Pitch, FixedYaw, 0f));
        }

        private static void Unwrap(List<CameraSample> samples) {
            for (int i = 1; i < samples.Count; i++) {
                float previous = samples[i - 1].Rotation.y;
                float delta    = Mathf.DeltaAngle(previous, samples[i].Rotation.y);

                CameraSample sample = samples[i];
                sample.Rotation.y = previous + delta;
                samples[i]        = sample;
            }
        }

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t) {
            float u  = 1f - t;
            float u2 = u * u;
            float t2 = t * t;

            return (u2 * u) * a + (3f * u2 * t) * b + (3f * u * t2) * c + (t2 * t) * d;
        }
    }
}
