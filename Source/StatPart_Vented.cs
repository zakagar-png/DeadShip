using RimWorld;
using Verse;

namespace DeadShip
{
    // Colonists work a little faster in a vented room: +5%, or +8% with fresh air from an intake.
    // Added to the global work speed stat by Patches/Venting.xml.
    public class StatPart_Vented : StatPart
    {
        private static float Factor(Pawn pawn)
        {
            int state = MapComponent_Air.RoomState(pawn);
            return state == 2 ? 1.08f : (state == 1 ? 1.05f : 1f);
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!req.HasThing)
            {
                return;
            }
            Pawn pawn = req.Thing as Pawn;
            if (pawn != null)
            {
                val *= Factor(pawn);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!req.HasThing)
            {
                return null;
            }
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return null;
            }
            float f = Factor(pawn);
            return f > 1f ? "Ventilated room: x" + f.ToString("F2") : null;
        }
    }
}
