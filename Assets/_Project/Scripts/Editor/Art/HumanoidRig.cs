using System.Collections.Generic;
using UnityEngine;

namespace Game.EditorTools.Art
{
    /// <summary>Joint angles and offsets for one frame of a humanoid. Angles in degrees, CCW positive, character faces right.</summary>
    public struct RigPose
    {
        public float Bob;        // hip height offset (pixels)
        public float Shift;      // hip x offset (pixels)
        public float Torso;      // lean; negative = forward
        public float Head;
        public float LegFront;   // positive = foot forward
        public float LegBack;
        public float ArmFront;   // 0 = hanging down, positive = hand forward, 180 = straight up
        public float ArmBack;
        public float WeaponTilt; // weapon angle relative to the front arm's line
        public float? WeaponAbsolute; // overrides the tilt: absolute weapon angle (0 = pointing up)
        public float Cape;       // negative = trailing behind
        public float BodyAngle;  // whole-body rotation (rolls, falls)
        public Vector2 BodyPivot;
        public Vector2 BodyOffset;
        public bool Smear;
        public float SmearFrom;  // front-arm angles the smear sweeps between
        public float SmearTo;
    }

    /// <summary>Assembles a frame from body parts and joint angles (front arm holds the weapon).</summary>
    public sealed class HumanoidRig
    {
        public PixelPart Head, Torso, LegFront, LegBack, ArmFront, ArmBack, Weapon, Cape;
        /// <summary>Held in the back hand (shields), drawn between the back arm and the body.</summary>
        public PixelPart Offhand;
        public float OffhandTilt;

        public Vector2 Hip = new(32f, 20f);
        public float LegSpread = 1.5f;
        public Vector2 NeckOnTorso = new(0.5f, 7f);       // from hip, in torso space
        public Vector2 ShoulderFront = new(2f, 5.5f);
        public Vector2 ShoulderBack = new(-2f, 6f);
        public Vector2 CapeOnTorso = new(-1f, 6.5f);
        public float ArmLength = 8f;
        public float WeaponReach = 21f;                    // grip → tip, for smears
        public int WeaponZ = 7;                            // 7 = behind the front arm, 9 = in front of it
        public Color32 SmearColor = PixelMaterial.Hex("#c9d2dc");

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Absolute weapon angle when it extends straight out of the fist.</summary>
        private static float WeaponAngle(float arm, float tilt) => arm + 180f + tilt;

        public Frame Build(RigPose p)
        {
            var frame = new Frame { BodyAngle = p.BodyAngle, BodyPivot = p.BodyPivot, BodyOffset = p.BodyOffset };
            Vector2 hip = Hip + new Vector2(p.Shift, p.Bob);
            Vector2 OnTorso(Vector2 local) => hip + Rotate(local, p.Torso);

            frame.Add(LegBack, hip + new Vector2(-LegSpread, 0f), p.LegBack, 2, -1);
            frame.Add(LegFront, hip + new Vector2(LegSpread, 0f), p.LegFront, 3);
            frame.Add(Torso, hip, p.Torso, 5);
            frame.Add(Head, OnTorso(NeckOnTorso), p.Torso + p.Head, 6);

            Vector2 shoulderBack = OnTorso(ShoulderBack);
            frame.Add(ArmBack, shoulderBack, p.ArmBack, 1, -1);
            if (Offhand != null)
            {
                Vector2 backHand = shoulderBack + Rotate(new Vector2(0f, -ArmLength), p.ArmBack);
                // In front of the body when raised (blocking), behind it when hanging.
                frame.Add(Offhand, backHand, p.Torso + OffhandTilt, p.ArmBack > 20f ? 7 : 4);
            }
            if (Cape != null) frame.Add(Cape, OnTorso(CapeOnTorso), p.Torso + p.Cape, 0);

            Vector2 shoulder = OnTorso(ShoulderFront);
            Vector2 hand = shoulder + Rotate(new Vector2(0f, -ArmLength), p.ArmFront);
            frame.Add(ArmFront, shoulder, p.ArmFront, 8);
            if (Weapon != null) frame.Add(Weapon, hand, p.WeaponAbsolute ?? WeaponAngle(p.ArmFront, p.WeaponTilt), WeaponZ);

            if (p.Smear)
            {
                // The tip direction (world angle) is the arm angle plus a constant, so the sweep equals the arm's travel.
                float tipFrom = 90f + WeaponAngle(p.SmearFrom, p.WeaponTilt);
                frame.Smears.Add(new Smear
                {
                    Center = shoulder,
                    InnerRadius = ArmLength + WeaponReach * 0.55f,
                    OuterRadius = ArmLength + WeaponReach,
                    FromAngle = tipFrom,
                    Sweep = p.SmearTo - p.SmearFrom,
                    Color = SmearColor,
                });
            }
            return frame;
        }

        public Clip Clip(string name, float fps, bool loop, IEnumerable<RigPose> poses)
        {
            var clip = new Clip(name, fps, loop);
            foreach (var pose in poses) clip.Frames.Add(Build(pose));
            return clip;
        }
    }
}
