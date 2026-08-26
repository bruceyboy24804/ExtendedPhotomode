namespace ExtendedPhotomode.Rendering {
    #region Using Statements

    using System.Collections.Generic;

    using UnityEngine;
    using UnityEngine.Rendering.HighDefinition;

    #endregion

    /// <summary>Draws a translucent tube along a run of world points.</summary>
    /// <remarks>
    /// A tube rather than a camera-facing strip. A ribbon has to be rebuilt every frame because it is
    /// only correct from one viewpoint, and it still reads as a flat band when the path turns beneath
    /// you. A tube is genuinely three-dimensional, so it looks the same from anywhere and only needs
    /// rebuilding when the path itself changes.
    /// The cross-section is carried along the curve with a rotation-minimising frame — each ring is
    /// oriented from the previous one rather than from a fixed world axis. Using a fixed reference
    /// makes the tube spin about its own axis wherever the path climbs or dives, and degenerate
    /// entirely where it runs vertically.
    /// </remarks>
    public sealed class EPM_TubeGizmo {
        public const float kNormalAlpha = 128f / 255f;

        private const int kSides = 8;

        private readonly List<Vector3> m_Vertices  = new List<Vector3>();
        private readonly List<Vector3> m_Normals   = new List<Vector3>();
        private readonly List<int>     m_Triangles = new List<int>();

        private Mesh     m_Mesh;
        private Material m_Material;

        public void Draw(IReadOnlyList<Vector3> points, Color color, float diameter) {
            if (points == null || points.Count < 2) {
                return;
            }

            Material material = GetMaterial();

            if (material == null) {
                return;
            }

            Rebuild(points, diameter * 0.5f);

            color.a = kNormalAlpha;
            material.SetColor("_UnlitColor", color);
            material.SetColor("_BaseColor", color);

            Graphics.DrawMesh(m_Mesh, Matrix4x4.identity, material, 0, null, 0, null,
                              UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        private void Rebuild(IReadOnlyList<Vector3> points, float radius) {
            m_Vertices.Clear();
            m_Normals.Clear();
            m_Triangles.Clear();

            Vector3 forward = Direction(points, 0);
            Vector3 right   = Vector3.Cross(forward, Vector3.up);

            if (right.sqrMagnitude < 0.0001f) {
                right = Vector3.Cross(forward, Vector3.right);
            }

            right = right.normalized;

            for (int i = 0; i < points.Count; i++) {
                forward = Direction(points, i);

                right = Vector3.ProjectOnPlane(right, forward);
                right = right.sqrMagnitude < 0.0001f
                            ? Vector3.Cross(forward, Vector3.up).normalized
                            : right.normalized;

                Vector3 up = Vector3.Cross(right, forward);

                for (int side = 0; side < kSides; side++) {
                    float angle  = side / (float)kSides * Mathf.PI * 2f;
                    Vector3 normal = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);

                    m_Vertices.Add(points[i] + normal * radius);
                    m_Normals.Add(normal);
                }
            }

            for (int i = 0; i < points.Count - 1; i++) {
                int ring = i * kSides;
                int next = ring + kSides;

                for (int side = 0; side < kSides; side++) {
                    int a = ring + side;
                    int b = ring + (side + 1) % kSides;
                    int c = next + side;
                    int d = next + (side + 1) % kSides;

                    m_Triangles.Add(a);
                    m_Triangles.Add(c);
                    m_Triangles.Add(b);

                    m_Triangles.Add(b);
                    m_Triangles.Add(c);
                    m_Triangles.Add(d);
                }
            }

            if (m_Mesh == null) {
                m_Mesh = new Mesh { name = "EPM_Tube", hideFlags = HideFlags.DontSave };
                m_Mesh.MarkDynamic();
            }

            m_Mesh.Clear();
            m_Mesh.SetVertices(m_Vertices);
            m_Mesh.SetNormals(m_Normals);
            m_Mesh.SetTriangles(m_Triangles, 0);
            m_Mesh.RecalculateBounds();
        }

        private static Vector3 Direction(IReadOnlyList<Vector3> points, int index) {
            int     before = Mathf.Max(index - 1, 0);
            int     after  = Mathf.Min(index + 1, points.Count - 1);
            Vector3 delta  = points[after] - points[before];

            return delta.sqrMagnitude < 0.0001f ? Vector3.forward : delta.normalized;
        }

        private Material GetMaterial() {
            if (m_Material != null) {
                return m_Material;
            }

            Shader shader = Shader.Find("HDRP/Unlit");

            if (shader == null) {
                return null;
            }

            m_Material = new Material(shader) { hideFlags = HideFlags.DontSave };

            HDMaterial.SetSurfaceType(m_Material, transparent: true);

            HDMaterial.ValidateMaterial(m_Material);

            m_Material.SetFloat("_ZWrite", 0f);
            m_Material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m_Material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m_Material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return m_Material;
        }
    }
}
