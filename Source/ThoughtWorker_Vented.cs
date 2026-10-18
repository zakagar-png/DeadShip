using RimWorld;
using Verse;

namespace DeadShip
{
    // A small mood bonus in a vented room, bigger with fresh air from an intake. Stages are in Defs/ThoughtDefs/Vented.xml.
    public class ThoughtWorker_Vented : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            int state = MapComponent_Air.RoomState(p);
            if (state == 2)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            if (state == 1)
            {
                return ThoughtState.ActiveAtStage(0);
            }
            return ThoughtState.Inactive;
        }
    }
}
