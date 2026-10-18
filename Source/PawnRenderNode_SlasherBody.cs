using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // The slasher's body sprite. Swaps to a variant with the head and/or arms missing
    // when those body parts have been lost. With nothing missing it draws exactly as before.
    public class PawnRenderNode_SlasherBody : PawnRenderNode_AnimalPart_Body
    {
        private static readonly Dictionary<string, Graphic> Cache = new Dictionary<string, Graphic>();

        // Variants that have a full set of facing sprites (DS_Slasher_L_north/east/south/west.png, and the same for R).
        // Every other variant only has a front-facing picture.
        private static readonly HashSet<string> DirectionalKeys = new HashSet<string> { "L", "R", "LR", "H", "HR", "HL" };

        public PawnRenderNode_SlasherBody(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            string key = SlasherParts.VariantKey(pawn);
            if (key.Length == 0)
            {
                return base.GraphicFor(pawn);
            }

            Graphic graphic;
            if (!Cache.TryGetValue(key, out graphic))
            {
                string path = "Things/Pawn/DS_Slasher/DS_Slasher_" + key;
                if (DirectionalKeys.Contains(key))
                {
                    graphic = GraphicDatabase.Get<Graphic_Multi>(
                        path,
                        ShaderDatabase.Cutout,
                        new Vector2(2.25f, 2.25f),
                        Color.white);
                }
                else
                {
                    graphic = GraphicDatabase.Get<Graphic_Single>(
                        path,
                        ShaderDatabase.Cutout,
                        new Vector2(2.25f, 2.25f),
                        Color.white);
                }
                Cache[key] = graphic;
            }
            return graphic;
        }
    }
}
