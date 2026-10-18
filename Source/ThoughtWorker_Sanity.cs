using RimWorld;
using Verse;

namespace DeadShip
{
    // A mood penalty that grows as sanity falls. Stages are in Defs/ThoughtDefs/Sanity.xml.
    public class ThoughtWorker_Sanity : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (p.needs == null)
            {
                return ThoughtState.Inactive;
            }
            Need_Sanity need = p.needs.TryGetNeed<Need_Sanity>();
            if (need == null)
            {
                return ThoughtState.Inactive;
            }
            float level = need.CurLevelPercentage;
            if (level < 0.05f)
            {
                return ThoughtState.ActiveAtStage(2);
            }
            if (level < 0.2f)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            if (level < 0.4f)
            {
                return ThoughtState.ActiveAtStage(0);
            }
            return ThoughtState.Inactive;
        }
    }
}
