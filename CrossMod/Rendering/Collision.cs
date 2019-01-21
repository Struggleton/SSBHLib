﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SFGraphics.Cameras;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SFShapes;
using CrossMod.Nodes;

namespace CrossMod.Rendering
{
    public class Attack : Collision
    {
        float Damage { get; set; }
        int Angle { get; set; }

        public Attack(ulong bone, float damage, int angle, float size, Vector3 pos) : base(bone, size, pos)
        {
            Damage = damage;
            Angle = angle;
        }
        public Attack(ulong bone, float damage, int angle, float size, Vector3 pos, Vector3 pos2) : base(bone, size, pos, pos2)
        {
            Damage = damage;
            Angle = angle;
        }

        public override void Render(Camera camera)
        {
            if (!Enabled)
                return;
            //TODO: this whole thing
            ShapeDrawing.drawSphere(Pos, Size, 30);
            
            base.Render(camera);
        }
    }

    public class Collision : IRenderable
    {
        public ulong Bone { get; set; }
        public float Size { get; set; }
        public Vector3 Pos { get; set; }
        public Vector3 Pos2 { get; set; }
        public Shape ShapeType { get; set; }
        public bool Enabled { get; set; }

        public Collision(ulong bone, float size, Vector3 pos)
        {
            Bone = bone;
            Pos = pos;
            Pos2 = Vector3.Zero;
            Size = size;
            ShapeType = Shape.sphere;
            Enabled = true;
        }
        public Collision(ulong bone, float size, Vector3 pos, Vector3 pos2)
        {
            Bone = bone;
            Pos = pos;
            Pos2 = pos2;
            Size = size;
            ShapeType = Shape.capsule;
            Enabled = true;
        }

        /// <summary>
        /// Renders the outline of the collision
        /// </summary>
        /// <param name="camera"></param>
        public virtual void Render(Camera camera)
        {
            if (!Enabled)
                return;
            //TODO: also this whole thing
            ShapeDrawing.drawCircleOutline(Pos, Size, 30);
        }

        public enum Shape
        {
            sphere,
            aabb,
            capsule
        }
    }
}
