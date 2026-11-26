using System.Collections.Generic;
using UnityEngine;

namespace WolfAndRabbits.Client.Visuals
{
    public static class StylizedMeshFactory
    {
        private static Shader _litFogShader;
        private static Shader _glowUnlitShader;

        private static Mesh _cone6Mesh;
        private static Mesh _octaCrystalMesh;
        private static Mesh _slashArcMesh;
        private static Mesh _groundQuadMesh;

        public static Shader LitFogShader
        {
            get
            {
                if (_litFogShader == null)
                {
                    _litFogShader = Resources.Load<Shader>("Shaders/WarcraftStylizedFog");
                    if (_litFogShader == null)
                        _litFogShader = Shader.Find("WolfAndRabbits/WarcraftStylizedFog");
                    if (_litFogShader == null)
                        _litFogShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (_litFogShader == null)
                        _litFogShader = Shader.Find("Standard");
                }
                return _litFogShader;
            }
        }

        public static Shader GlowUnlitShader
        {
            get
            {
                if (_glowUnlitShader == null)
                {
                    _glowUnlitShader = Resources.Load<Shader>("Shaders/WarcraftGlowUnlit");
                    if (_glowUnlitShader == null)
                        _glowUnlitShader = Shader.Find("WolfAndRabbits/WarcraftGlowUnlit");
                    if (_glowUnlitShader == null)
                        _glowUnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                }
                return _glowUnlitShader;
            }
        }

        public static Mesh Cone6Mesh => _cone6Mesh != null ? _cone6Mesh : (_cone6Mesh = BuildFacetedCone(7, 1f, 0.5f));
        public static Mesh OctaCrystalMesh => _octaCrystalMesh != null ? _octaCrystalMesh : (_octaCrystalMesh = BuildCrystalMesh());
        public static Mesh SlashArcMesh => _slashArcMesh != null ? _slashArcMesh : (_slashArcMesh = BuildSlashArcMesh());
        public static Mesh GroundQuadMesh => _groundQuadMesh != null ? _groundQuadMesh : (_groundQuadMesh = BuildHorizontalQuad());

        public static Material CreateStylizedMaterial(
            Color baseColor,
            Texture2D tex = null,
            float smoothness = 0.35f,
            float rimStrength = 0.32f,
            Color? rimColor = null,
            Color? emission = null,
            float windSway = 0f,
            bool useVertexColor = false,
            float fogInfluence = 1f)
        {
            Material mat = new Material(LitFogShader);
            mat.SetColor("_BaseColor", baseColor);
            if (tex != null)
                mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_RimStrength"))
                mat.SetFloat("_RimStrength", rimStrength);
            if (mat.HasProperty("_RimColor"))
                mat.SetColor("_RimColor", rimColor ?? new Color(0.68f, 0.88f, 1f, 1f));
            if (mat.HasProperty("_EmissionColor"))
            {
                Color em = emission ?? Color.black;
                mat.SetColor("_EmissionColor", em);
                if (em.maxColorComponent > 0.01f)
                    mat.EnableKeyword("_EMISSION");
            }
            if (mat.HasProperty("_WindSway"))
                mat.SetFloat("_WindSway", windSway);
            if (mat.HasProperty("_UseVertexColor"))
                mat.SetFloat("_UseVertexColor", useVertexColor ? 1f : 0f);
            if (mat.HasProperty("_FogInfluence"))
                mat.SetFloat("_FogInfluence", fogInfluence);
            return mat;
        }

        public static Material CreateGlowMaterial(Color hdrColor, Texture2D tex = null, float pulseSpeed = 0f, float pulseAmount = 0f)
        {
            Material mat = new Material(GlowUnlitShader);
            mat.SetColor("_BaseColor", hdrColor);
            if (tex != null)
                mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_PulseSpeed"))
                mat.SetFloat("_PulseSpeed", pulseSpeed);
            if (mat.HasProperty("_PulseAmount"))
                mat.SetFloat("_PulseAmount", pulseAmount);
            return mat;
        }

        public static GameObject CreatePart(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material mat,
            Vector3 localEuler = default,
            bool castShadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = castShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return go;
        }

        public static GameObject CreateMeshPart(
            string name,
            Mesh mesh,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material mat,
            Vector3 localEuler = default,
            bool castShadows = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localEulerAngles = localEuler;
            go.transform.localScale = localScale;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            return go;
        }

        private static Mesh BuildFacetedCone(int sides, float height, float radius)
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            Vector3 apex = new Vector3(0f, height, 0f);
            Vector3 baseCenter = Vector3.zero;

            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);

                int b = verts.Count;
                verts.Add(apex);
                verts.Add(p1);
                verts.Add(p0);
                uvs.Add(new Vector2(0.5f, 1f));
                uvs.Add(new Vector2(1f, 0f));
                uvs.Add(new Vector2(0f, 0f));
                tris.Add(b);
                tris.Add(b + 1);
                tris.Add(b + 2);

                int c = verts.Count;
                verts.Add(baseCenter);
                verts.Add(p0);
                verts.Add(p1);
                uvs.Add(new Vector2(0.5f, 0.5f));
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(1f, 0f));
                tris.Add(c);
                tris.Add(c + 1);
                tris.Add(c + 2);
            }

            Mesh m = new Mesh { name = "StylizedCone" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildCrystalMesh()
        {
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            int sides = 6;
            Vector3 top = new Vector3(0f, 0.72f, 0f);
            Vector3 bot = new Vector3(0f, -0.58f, 0f);
            float rad = 0.34f;

            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f;
                float a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * rad, 0.08f, Mathf.Sin(a0) * rad);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * rad, 0.08f, Mathf.Sin(a1) * rad);

                int idx = verts.Count;
                verts.Add(top); verts.Add(p1); verts.Add(p0);
                uvs.Add(new Vector2(0.5f, 1f)); uvs.Add(new Vector2(1f, 0.5f)); uvs.Add(new Vector2(0f, 0.5f));
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);

                idx = verts.Count;
                verts.Add(bot); verts.Add(p0); verts.Add(p1);
                uvs.Add(new Vector2(0.5f, 0f)); uvs.Add(new Vector2(0f, 0.5f)); uvs.Add(new Vector2(1f, 0.5f));
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            }

            Mesh m = new Mesh { name = "StylizedCrystal" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildSlashArcMesh()
        {
            int segs = 12;
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            float startAng = -75f * Mathf.Deg2Rad;
            float endAng = 75f * Mathf.Deg2Rad;

            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                float ang = Mathf.Lerp(startAng, endAng, t);
                float thickness = Mathf.Sin(t * Mathf.PI) * 0.42f;
                float rOuter = 1.25f;
                float rInner = rOuter - thickness;

                verts.Add(new Vector3(Mathf.Sin(ang) * rInner, 0f, Mathf.Cos(ang) * rInner));
                verts.Add(new Vector3(Mathf.Sin(ang) * rOuter, 0f, Mathf.Cos(ang) * rOuter));
                uvs.Add(new Vector2(t, 0f));
                uvs.Add(new Vector2(t, 1f));

                if (i < segs)
                {
                    int b = i * 2;
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 3);
                    tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                }
            }

            Mesh m = new Mesh { name = "SlashArc" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh BuildHorizontalQuad()
        {
            Mesh m = new Mesh { name = "GroundQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(-0.5f, 0f, 0.5f)
            };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.uv = new[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }
    }
}
