﻿using OpenTK;
using System.Linq;
using SFGraphics.Cameras;
using SFGraphics.GLObjects.Shaders;
using System.Collections.Generic;
using SFGenericModel.Materials;

namespace CrossMod.Rendering.Models
{
    public class RModel : IRenderable
    {
        public Vector4 BoundingSphere { get; set; }

        Matrix4[] boneBinds = new Matrix4[200];
        public SFGenericModel.Materials.UniformBlock boneUniformBuffer;

        public List<RMesh> subMeshes = new List<RMesh>();

        public RModel()
        {
            var shader = ShaderContainer.GetShader("RModel");
            if (shader.LinkStatusIsOk)
                boneUniformBuffer = new SFGenericModel.Materials.UniformBlock(shader, "Bones");
        }
        public void Render(Camera camera)
        {
            Render(camera, null);
        }

        public void Render(Camera camera, RSkeleton Skeleton = null)
        {
            Shader shader = ShaderContainer.GetCurrentRModelShader();
            if (!shader.LinkStatusIsOk)
                return;

            shader.UseProgram();

            SetUniforms(shader);
            SetCameraUniforms(camera, shader);

            // Bones
            boneUniformBuffer.BindBlock(shader);
            if (Skeleton != null)
            {
                boneBinds = Skeleton.GetAnimationTransforms();
            }
            boneUniformBuffer.SetValues("transforms", boneBinds);

            DrawMeshes(camera, Skeleton, shader);
        }

        private static void SetCameraUniforms(Camera Camera, Shader currentShader)
        {
            if (RenderSettings.Instance.RenderUVs)
            {
                // TODO: Adjust scale.
                // Flip UVs vertically.
                float scale = 2;
                Matrix4 mvp = Matrix4.CreateOrthographicOffCenter(-scale, scale, scale, -scale, -scale, scale);
                currentShader.SetMatrix4x4("mvp", ref mvp);
            }
            else
            {
                Matrix4 mvp = Camera.MvpMatrix;
                currentShader.SetMatrix4x4("mvp", ref mvp);
            }

            currentShader.SetMatrix4x4("modelView", Camera.ModelViewMatrix);
            currentShader.SetVector3("cameraPos", Camera.TransformedPosition);
        }

        private static void SetUniforms(Shader currentShader)
        {
            currentShader.SetVector4("renderChannels", RenderSettings.Instance.renderChannels);
            currentShader.SetInt("renderMode", (int)RenderSettings.Instance.ShadingMode);

            currentShader.SetFloat("floatTestParam", RenderSettings.Instance.FloatTestParam);

            currentShader.SetFloat("directLightIntensity", RenderSettings.Instance.DirectLightIntensity);
            currentShader.SetFloat("iblIntensity", RenderSettings.Instance.IblIntensity);

            currentShader.SetBoolToInt("renderDiffuse", RenderSettings.Instance.EnableDiffuse);
            currentShader.SetBoolToInt("renderSpecular", RenderSettings.Instance.EnableSpecular);
            currentShader.SetBoolToInt("renderEmission", RenderSettings.Instance.EnableEmission);
            currentShader.SetBoolToInt("renderRimLighting", RenderSettings.Instance.EnableRimLighting);
            currentShader.SetBoolToInt("renderExperimental", RenderSettings.Instance.EnableExperimental);

            currentShader.SetBoolToInt("renderNormalMaps", RenderSettings.Instance.RenderNormalMaps);
            currentShader.SetBoolToInt("renderVertexColor", RenderSettings.Instance.RenderVertexColor);

            currentShader.SetBoolToInt("renderWireframe", RenderSettings.Instance.EnableWireframe);
        }

        private void DrawMeshes(Camera Camera, RSkeleton Skeleton, Shader currentShader)
        {
            var opaque = new List<RMesh>();
            var transparentDepthSorted = new List<RMesh>();

            foreach (RMesh m in subMeshes)
            {
                if (m.Material.IsTransparent)
                    transparentDepthSorted.Add(m);
                else
                    opaque.Add(m);
            }

            // TODO: Investigate how sorting works in game.
            //transparentDepthSorted = transparentDepthSorted.OrderBy(m => (Camera.TransformedPosition - m.BoundingSphere.Xyz).Length + m.BoundingSphere.W).ToList();

            // Models often share a material, so skip redundant and costly state changes.
            string previousMaterialName = "";
            GenericMaterial previousUniforms = null;

            foreach (RMesh m in opaque)
            {
                DrawMesh(m, Camera, Skeleton, currentShader, previousMaterialName, previousUniforms);
                previousMaterialName = m.Material.Name;
                previousUniforms = m.genericMaterial;
            }

            // Draw transparent meshes last for proper alpha blending.
            foreach (RMesh m in transparentDepthSorted)
            {
                DrawMesh(m, Camera, Skeleton, currentShader, previousMaterialName, previousUniforms);
                previousMaterialName = m.Material.Name;
                previousUniforms = m.genericMaterial;
            }
        }
        private static void DrawMesh(RMesh m, Camera camera, RSkeleton skeleton, Shader currentShader, string previousMaterialName, GenericMaterial previousUniforms)
        {
            if (m.Material != null && m.Material.Name != previousMaterialName)
            {
                m.SetMaterialUniforms(currentShader, previousUniforms);
                m.RenderMesh.SetRenderState(m.Material);
            }

            m.Draw(currentShader, camera, skeleton);
        }
    }
}
