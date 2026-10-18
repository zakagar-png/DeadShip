using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // The exploder's body sprite. Once its volatile sac is destroyed it is dead, and it keeps the
    // picture without the big sac (Things/Pawn/DS_Exploder/DS_Exploder_NoSac, one view).
    public class PawnRenderNode_ExploderBody : PawnRenderNode_AnimalPart_Body
    {
        private static readonly Dictionary<string, Graphic> Cache = new Dictionary<string, Graphic>();

        public PawnRenderNode_ExploderBody(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public static bool SacGone(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return false;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_MissingPart missing = hediffs[i] as Hediff_MissingPart;
                if (missing != null && missing.Part != null && missing.Part.def.defName == "DS_VolatileSac")
                {
                    return true;
                }
            }
            return false;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            if (!SacGone(pawn))
            {
                return base.GraphicFor(pawn);
            }

            Graphic graphic;
            if (!Cache.TryGetValue("NoSac", out graphic))
            {
                graphic = GraphicDatabase.Get<Graphic_Single>(
                    "Things/Pawn/DS_Exploder/DS_Exploder_NoSac",
                    ShaderDatabase.Cutout,
                    new Vector2(1.5f, 1.5f),
                    Color.white);
                Cache["NoSac"] = graphic;
            }
            return graphic;
        }
    }
}
