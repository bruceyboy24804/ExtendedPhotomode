namespace ExtendedPhotomode.Rendering {
    #region Using Statements

    using System.Collections.Generic;

    using UnityEngine;
    using UnityEngine.Rendering.HighDefinition;

    #endregion

    /// <summary>Draws solid spheres in the world, for marking points a flat overlay shape cannot represent.</summary>
    /// <remarks>
    /// The overlay's <c>CustomMeshType</c> offers only Cylinder, Arrow and Plane, so a sphere has to
    /// be generated. It is drawn with <see cref="Graphics.DrawMesh(Mesh, Matrix4x4, Material, int)"/>
    /// and a per-instance TRS matrix rather than through the overlay buffer: the overlay's filled-mesh
    /// path draws at the identity matrix, which would mean rebuilding world-space geometry every time
    /// a point moves. One unit sphere drawn many times costs nothing by comparison.
    /// A material is cached per colour instead of using a <c>MaterialPropertyBlock</c>. There are only
    /// a handful of colours, and per-instance property blocks interact badly enough with HDRP's
    /// batching elsewhere in this game that avoiding them is the cheaper choice.
    /// </remarks>
    public static class EPM_SphereGizmo {
        private const int kSegments = 16;

        private const int kRings = 10;

        private static Mesh s_Mesh;

        private static readonly Dictionary<Color, Material> s_Materials = new Dictionary<Color, Material>();

        public static void Draw(Vector3 position, float diameter, Color color) {
            Material material = GetMaterial(color);

            if (material == null) {
                return;
            }

            Graphics.DrawMesh(GetMesh(),
                              Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * diameter),
                              material, 0, null, 0, null,
                              UnityEngine.Rendering.ShadowCastingMode.Off, false);
        }

        private static Mesh GetMesh() {
            if (s_Mesh != null) {
                return s_Mesh;
            }

            s_Mesh = BuildSphere(kSegments, kRings);
            return s_Mesh;
        }

        private static Material GetMaterial(Color color) {
            if (s_Materials.TryGetValue(color, out Material existing)) {
                return existing;
            }

            Shader shader = Shader.Find("HDRP/Unlit");

            if (shader == null) {
                return null;
            }

            var material = new Material(shader) {
                hideFlags         = HideFlags.DontSave,
                enableInstancing  = true,
            };

            material.SetColor("_UnlitColor", color);
            material.SetColor("_BaseColor", color);

            HDMaterial.ValidateMaterial(material);

            s_Materials[color] = material;
            return material;
        }

        private static Mesh BuildSphere(int segments, int rings) {
            var vertices = new List<Vector3>((segments + 1) * (rings + 1));
            var normals  = new List<Vector3>(vertices.Capacity);
            var triangles = new List<int>(segments * rings * 6);

            for (int ring = 0; ring <= rings; ring++) {
                float v     = (float)ring / rings;
                float phi   = v * Mathf.PI;
                float y     = Mathf.Cos(phi) * 0.5f;
                float scale = Mathf.Sin(phi) * 0.5f;

                for (int segment = 0; segment <= segments; segment++) {
                    float u     = (float)segment / segments;
                    float theta = u * Mathf.PI * 2f;
                    var   point = new Vector3(Mathf.Cos(theta) * scale, y, Mathf.Sin(theta) * scale);

                    vertices.Add(point);
                    normals.Add(point.normalized);
                }
            }

            int stride = segments + 1;

            for (int ring = 0; ring < rings; ring++) {
                for (int segment = 0; segment < segments; segment++) {
                    int current = ring * stride + segment;
                    int next    = current + stride;

                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);

                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            var mesh = new Mesh { name = "EPM_Sphere", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
