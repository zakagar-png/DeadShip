using System.Collections.Generic;
using Verse;

namespace DeadShip
{
    // Works out which sprite variant a slasher should wear, from the body parts it has lost.
    // Key letters: H = head gone, L = the creature's LEFT arm gone, R = its RIGHT arm gone.
    // Matching sprite files: DS_Slasher_H.png, DS_Slasher_HL.png, DS_Slasher_LR.png, etc.
    public static class SlasherParts
    {
        public static string VariantKey(Pawn pawn)
        {
            if (pawn == null || pawn.health == null)
            {
                return "";
            }

            bool head = false;
            bool left = false;
            bool right = false;

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_MissingPart gone = hediffs[i] as Hediff_MissingPart;
                if (gone == null || gone.Part == null)
                {
                    continue;
                }

                string def = gone.Part.def.defName;
                if (def == "DS_Head")
                {
                    head = true;
                }
                else if (def == "DS_Arm")
                {
                    string label = gone.Part.Label;
                    if (label != null && label.Contains("left"))
                    {
                        left = true;
                    }
                    else
                    {
                        right = true;
                    }
                }
            }

            string key = "";
            if (head) { key += "H"; }
            if (left) { key += "L"; }
            if (right) { key += "R"; }
            return key;
        }
    }
}
