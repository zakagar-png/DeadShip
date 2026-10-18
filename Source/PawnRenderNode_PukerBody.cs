using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // The puker's body sprite. Swaps to a variant picture when it has lost its head and/or
    // arms (DS_Puker_H_*.png, _L_, _R_, _LR_, each with north/east/south/west).
    // Combinations that have no picture yet fall back to the nearest one that exists.
    public class PawnRenderNode_PukerBody : PawnRenderNode_AnimalPart_Body
    {
        private static readonly Dictionary<string, Graphic> Cache = new Dictionary<string, Graphic>();

        // Variants that have a full set of facing sprites.
        private static readonly HashSet<string> Available = new HashSet<string> { "H", "HL", "HR", "L", "R", "LR", "HLR", "G", "GB", "GL", "GR", "GLR" };

        public PawnRenderNode_PukerBody(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        // The gut sac is destroyed: the puker dies, and shows the empty-cavity picture.
        private static bool GutGone(Pawn pawn)
        {
            if (pawn == null || pawn.health == null) { return false; }
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_MissingPart gone = hediffs[i] as Hediff_MissingPart;
                if (gone != null && gone.Part != null && gone.Part.def.defName == "DS_Gut") { return true; }
            }
            return false;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            string key = SlasherParts.VariantKey(pawn);
            if (GutGone(pawn))
            {
                // Dead from the gut sac. Head gone: torso only (G). Otherwise head on, with whichever arms are left.
                string arms = key.Replace("H", "");
                key = key.Contains("H") ? "G" : "G" + (arms.Length == 0 ? "B" : arms);
            }
            if (!Available.Contains(key))
            {
                // No picture for this exact combination: show the headless one if the head is gone.
                key = key.Contains("H") ? "H" : "";
            }
            if (key.Length == 0)
            {
                return base.GraphicFor(pawn);
            }

            Graphic graphic;
            if (!Cache.TryGetValue(key, out graphic))
            {
                string path = "Things/Pawn/DS_Puker/DS_Puker_" + key;
                if (key == "HLR" || key.StartsWith("G"))
                {
                    // Dead states (head and both arms gone, or the gut sac gone): one front-facing picture is enough.
                    graphic = GraphicDatabase.Get<Graphic_Single>(path, ShaderDatabase.Cutout, new Vector2(1.5f, 1.5f), Color.white);
                }
                else
                {
                    graphic = GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.Cutout, new Vector2(1.5f, 1.5f), Color.white);
                }
                Cache[key] = graphic;
            }
            return graphic;
        }
    }
}
