using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // The lurker's body sprite. Swaps to a picture with fewer tentacles as they are cut off:
    // DS_Lurker_T1_* (one gone) and DS_Lurker_T2_* (two gone). With all three gone it is dead and keeps the T2 picture.
    public class PawnRenderNode_LurkerBody : PawnRenderNode_AnimalPart_Body
    {
        private static readonly Dictionary<string, Graphic> Cache = new Dictionary<string, Graphic>();

        public PawnRenderNode_LurkerBody(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree)
        {
        }

        public static int TentaclesGone(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return 0;
            }

            int gone = 0;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_MissingPart missing = hediffs[i] as Hediff_MissingPart;
                if (missing != null && missing.Part != null && missing.Part.def.defName == "DS_Tentacle")
                {
                    gone++;
                }
            }
            return gone;
        }

        public override Graphic GraphicFor(Pawn pawn)
        {
            int gone = TentaclesGone(pawn);
            if (gone <= 0)
            {
                return base.GraphicFor(pawn);
            }

            string key = gone == 1 ? "T1" : "T2";
            Graphic graphic;
            if (!Cache.TryGetValue(key, out graphic))
            {
                graphic = GraphicDatabase.Get<Graphic_Multi>(
                    "Things/Pawn/DS_Lurker/DS_Lurker_" + key,
                    ShaderDatabase.Cutout,
                    new Vector2(1.5f, 1.5f),
                    Color.white);
                Cache[key] = graphic;
            }
            return graphic;
        }
    }
}
