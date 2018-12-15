﻿using CrossMod.Rendering;
using CrossMod.Rendering.Models;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SSBHLib;
using SSBHLib.Formats;
using SSBHLib.Tools;
using System;
using System.Collections.Generic;
using System.IO;

namespace CrossMod.Nodes
{
    [FileTypeAttribute(".numshb")]
    public class NUMSHB_Node : FileNode, IRenderableNode
    {
        public MESH mesh;

        public NUMSHB_Node()
        {
            ImageKey = "model";
            SelectedImageKey = "model";
        }

        public override void Open(string Path)
        {
            if (SSBH.TryParseSSBHFile(Path, out ISSBH_File ssbhFile))
            {
                if (ssbhFile is MESH)
                {
                    mesh = (MESH)ssbhFile;
                }
            }
        }

        public IRenderable GetRenderableNode()
        {
            return GetRenderableNode(null);
        }

        public IRenderable GetRenderableNode(RSkeleton Skeleton = null)
        {
            System.Diagnostics.Debug.WriteLine("Create render meshes");
            RModel model = new RModel();

            List<int> bufferOffsets = new List<int>(mesh.VertexBuffers.Length);
            int bufferOffset = 0;

            // TODO: If there are enough elements, estimating capacity may improve performance.
            List<byte> vertexBuffer = new List<byte>();

            // Merge buffers into one because OpenGL supports a single array buffer.
            foreach (MESH_Buffer meshBuffer in mesh.VertexBuffers)
            {
                vertexBuffer.AddRange(meshBuffer.Buffer);

                bufferOffsets.Add(bufferOffset);
                bufferOffset += meshBuffer.Buffer.Length;
            }

            // Read the mesh information into the Rendering Mesh.
            foreach (MESH_Object meshObject in mesh.Objects)
            {
                RMesh rMesh = new RMesh
                {
                    Name = meshObject.Name,
                    SingleBindName = meshObject.ParentBoneName,
                    IndexCount = meshObject.IndexCount,
                    IndexOffset = (int)meshObject.ElementOffset
                };

                var vertexAccessor = new SSBHVertexAccessor(mesh);

                var indices = vertexAccessor.ReadIndices(0, meshObject.IndexCount, meshObject);
                // TODO: SFGraphics doesn't support the other index types yet.
                var intIndices = new List<int>();
                foreach (var index in indices)
                {
                    intIndices.Add((int)index);
                }

                var positions = vertexAccessor.ReadAttribute("Position0", 0, meshObject.VertexCount, meshObject);
                var normals = vertexAccessor.ReadAttribute("Normal0", 0, meshObject.VertexCount, meshObject);
                var tangents = vertexAccessor.ReadAttribute("Tangent0", 0, meshObject.VertexCount, meshObject);
                var map1Values = vertexAccessor.ReadAttribute("map1", 0, meshObject.VertexCount, meshObject);

                Vector3[] generatedBitangents = GenerateBitangents(intIndices, positions, map1Values);

                var boneIndices = new IVec4[positions.Length];
                var boneWeights = new Vector4[positions.Length];

                var riggingAccessor = new SSBHRiggingAccessor(mesh);
                SSBHVertexInfluence[] influences = riggingAccessor.ReadRiggingBuffer(meshObject.Name, (int)meshObject.SubMeshIndex);
                Dictionary<string, int> indexByBoneName = new Dictionary<string, int>();
                if (Skeleton != null)
                {
                    for (int i = 0; i < Skeleton.Bones.Count; i++)
                    {
                        indexByBoneName.Add(Skeleton.Bones[i].Name, i);
                    }
                }

                foreach (SSBHVertexInfluence influence in influences)
                {
                    if (boneWeights[influence.VertexIndex].X == 0)
                    {
                        boneIndices[influence.VertexIndex].X = indexByBoneName[influence.BoneName];
                        boneWeights[influence.VertexIndex].X = influence.Weight;
                    }
                    else if (boneWeights[influence.VertexIndex].Y == 0)
                    {
                        boneIndices[influence.VertexIndex].Y = indexByBoneName[influence.BoneName];
                        boneWeights[influence.VertexIndex].Y = influence.Weight;
                    }
                    else if (boneWeights[influence.VertexIndex].Z == 0)
                    {
                        boneIndices[influence.VertexIndex].Z = indexByBoneName[influence.BoneName];
                        boneWeights[influence.VertexIndex].Z = influence.Weight;
                    }
                    else if (boneWeights[influence.VertexIndex].W == 0)
                    {
                        boneIndices[influence.VertexIndex].W = indexByBoneName[influence.BoneName];
                        boneWeights[influence.VertexIndex].W = influence.Weight;
                    }
                }

                var vertices = new List<CustomVertex>();
                for (int i = 0; i < positions.Length; i++)
                {
                    var position = GetVector4(positions[i]).Xyz;
                    var normal = GetVector4(normals[i]).Xyz;
                    var tangent = GetVector4(tangents[i]).Xyz;
                    var map1 = GetVector4(map1Values[i]).Xy;
                    var bones = boneIndices[i];
                    var weights = boneWeights[i];

                    Vector3 bitangent = GetBitangent(generatedBitangents, i, normal);

                    vertices.Add(new CustomVertex(position, normal, tangent, bitangent, map1, bones, weights));
                }

                rMesh.RenderMesh = new RenderMesh(vertices, intIndices);

                model.subMeshes.Add(rMesh);

                if (meshObject.DrawElementType == 1)
                    rMesh.DrawElementType = DrawElementsType.UnsignedInt;

                AddVertexAttributes(rMesh, bufferOffsets, meshObject);

                // Add rigging if the skeleton exists.
                if (Skeleton != null)
                {
                    // TODO: This step is slow.
                    AddRiggingBufferData(vertexBuffer, Skeleton, meshObject, rMesh);
                }
            }

            SetVertexAndIndexBuffers(model, vertexBuffer);

            return model;
        }

        private static Vector3 GetBitangent(Vector3[] generatedBitangents, int i, Vector3 normal)
        {
            // Account for mirrored normal maps.
            var bitangent = SFGraphics.Utils.VectorUtils.Orthogonalize(generatedBitangents[i], normal);
            bitangent *= -1;
            return bitangent;
        }

        private static Vector3[] GenerateBitangents(List<int> intIndices, SSBHVertexAttribute[] positions, SSBHVertexAttribute[] uvs)
        {
            var generatedBitangents = new Vector3[positions.Length];
            for (int i = 0; i < intIndices.Count; i += 3)
            {
                SFGraphics.Utils.VectorUtils.GenerateTangentBitangent(GetVector4(positions[intIndices[i]]).Xyz, GetVector4(positions[intIndices[i + 1]]).Xyz, GetVector4(positions[intIndices[i + 2]]).Xyz,
                    GetVector4(uvs[intIndices[i]]).Xy, GetVector4(uvs[intIndices[i + 1]]).Xy, GetVector4(uvs[intIndices[i + 2]]).Xy, out Vector3 tangent, out Vector3 bitangent);

                generatedBitangents[intIndices[i]] += bitangent;
                generatedBitangents[intIndices[i + 1]] += bitangent;
                generatedBitangents[intIndices[i + 2]] += bitangent;
            }

            return generatedBitangents;
        }

        private static Vector4 GetVector4(SSBHVertexAttribute values)
        {
            return new Vector4(values.X, values.Y, values.Z, values.W);
        }

        private void SetVertexAndIndexBuffers(RModel model, List<byte> vertexBuffer)
        {
            // Create and prepare the buffers for rendering
            model.indexBuffer = new SFGraphics.GLObjects.BufferObjects.BufferObject(BufferTarget.ElementArrayBuffer);
            model.indexBuffer.SetData(mesh.PolygonBuffer, BufferUsageHint.StaticDraw);

            model.vertexBuffer = new SFGraphics.GLObjects.BufferObjects.BufferObject(BufferTarget.ArrayBuffer);
            model.vertexBuffer.SetData(vertexBuffer.ToArray(), BufferUsageHint.StaticDraw);
        }

        private void AddRiggingBufferData(List<byte> vertexBuffer, RSkeleton Skeleton, MESH_Object meshObject, RMesh mesh)
        {
            // This is such a messy way of prepping it...
            Dictionary<string, int> indexByBoneName = new Dictionary<string, int>();
            if (Skeleton != null)
            {
                for (int i = 0; i < Skeleton.Bones.Count; i++)
                {
                    indexByBoneName.Add(Skeleton.Bones[i].Name, i);
                }
            }

            // Get the influences.
            SSBHRiggingAccessor riggingAccessor = new SSBHRiggingAccessor(this.mesh);
            SSBHVertexInfluence[] influences = riggingAccessor.ReadRiggingBuffer(meshObject.Name, (int)meshObject.SubMeshIndex);

            // Create a bank for writing.
            Vector4[] bones = new Vector4[meshObject.VertexCount];
            Vector4[] boneWeights = new Vector4[meshObject.VertexCount];
            foreach (SSBHVertexInfluence vertexInfluence in influences)
            {
                AddWeight(ref bones[vertexInfluence.VertexIndex], ref boneWeights[vertexInfluence.VertexIndex], (ushort)indexByBoneName[vertexInfluence.BoneName], vertexInfluence.Weight);
            }

            byte[] riggingData = GetRiggingData(meshObject, bones, boneWeights);

            // Add attributes for the new data
            mesh.VertexAttributes.Add(new CustomVertexAttribute()
            {
                Name = "boneIndices",
                Size = 4,
                IType = VertexAttribIntegerType.UnsignedShort,
                Offset = vertexBuffer.Count,
                Stride = 4 * 6,
                Integer = true
            });

            mesh.VertexAttributes.Add(new CustomVertexAttribute()
            {
                Name = "boneWeights",
                Size = 4,
                Type = VertexAttribPointerType.Float,
                Offset = vertexBuffer.Count + 8,
                Stride = 4 * 6
            });

            // Add rigging buffer onto the end of vertex buffer
            vertexBuffer.AddRange(riggingData);
        }

        private static byte[] GetRiggingData(MESH_Object meshObject, Vector4[] bones, Vector4[] boneWeights)
        {
            byte[] riggingData;

            // Build a byte buffer for the data.
            using (MemoryStream riggingBuffer = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(riggingBuffer))
                {
                    for (int i = 0; i < meshObject.VertexCount; i++)
                    {
                        for (int j = 0; j < 4; j++)
                            writer.Write((ushort)bones[i][j]);
                        for (int j = 0; j < 4; j++)
                            writer.Write(boneWeights[i][j]);
                    }
                }
                riggingData = riggingBuffer.GetBuffer();
            }

            return riggingData;
        }

        private static void AddVertexAttributes(RMesh mesh, List<int> bufferOffsets, MESH_Object meshObject)
        {
            // Vertex Attributes
            foreach (MESH_Attribute meshAttribute in meshObject.Attributes)
            {
                CustomVertexAttribute customAttribute = new CustomVertexAttribute
                {
                    Name = meshAttribute.AttributeStrings[0].Name,
                    Normalized = false,
                    Stride = meshAttribute.BufferIndex == 1 ? meshObject.Stride2 : meshObject.Stride,
                    Offset = bufferOffsets[meshAttribute.BufferIndex] + (meshAttribute.BufferIndex == 0 ? meshObject.VertexOffset : meshObject.VertexOffset2) + meshAttribute.BufferOffset,
                    Size = 3
                };

                // TODO: There may be another way to determine size.
                if (customAttribute.Name.Equals("map1") || customAttribute.Name.Contains("uvSet"))
                {
                    customAttribute.Size = 2;
                }
                if (customAttribute.Name.Contains("colorSet"))
                {
                    customAttribute.Size = 4;
                }

                customAttribute.Type = GetAttributeType(meshAttribute);
                mesh.VertexAttributes.Add(customAttribute);

                System.Diagnostics.Debug.WriteLine($"{customAttribute.Name} {customAttribute.Size} {customAttribute.Type}");
            }
        }

        private static VertexAttribPointerType GetAttributeType(MESH_Attribute meshAttribute)
        {
            switch (meshAttribute.DataType)
            {
                case 0:
                    return VertexAttribPointerType.Float;
                case 2:
                    return VertexAttribPointerType.Byte; // color
                case 5:
                    return VertexAttribPointerType.HalfFloat;
                case 8:
                    return VertexAttribPointerType.HalfFloat;
                default:
                    return VertexAttribPointerType.Float;
            }
        }

        private void AddWeight(ref Vector4 bones, ref Vector4 boneWeights, ushort bone, float weight)
        {
            if (boneWeights.X == 0)
            {
                bones.X = bone;
                boneWeights.X = weight;
            }
            else if (boneWeights.Y == 0)
            {
                bones.Y = bone;
                boneWeights.Y = weight;
            }
            else if (boneWeights.Z == 0)
            {
                bones.Z = bone;
                boneWeights.Z = weight;
            }
            else if (boneWeights.W == 0)
            {
                bones.W = bone;
                boneWeights.W = weight;
            }
        }
    }
}
