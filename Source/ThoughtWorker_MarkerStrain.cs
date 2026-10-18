using RimWorld;
using Verse;

namespace DeadShip
{
    // A dread that is felt everywhere on the map and grows with every fragment loaded, however far from the Marker.
    // The stages (labels and mood values) are in Defs/ThoughtDefs/MarkerFear.xml.
    public class ThoughtWorker_MarkerStrain : ThoughtWorker
    {
        // Fragments needed for each stage.
        private static readonly int[] StageFragments = { 1, 4, 8, 12, 16 };

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!p.Spawned || p.Map == null || MarkerUtility.Susceptibility(p) <= 0f)
            {
                return ThoughtState.Inactive;
            }

            int total = MarkerUtility.FearFragments(p.Map);
            for (int stage = StageFragments.Length - 1; stage >= 0; stage--)
            {
                if (total >= StageFragments[stage])
                {
                    // Memory suppressants take the edge off: two stages milder.
                    if (MarkerUtility.IsSuppressed(p))
                    {
                        stage -= 2;
                        if (stage < 0)
                        {
                            return ThoughtState.Inactive;
                        }
                    }
                    return ThoughtState.ActiveAtStage(stage);
                }
            }
            return ThoughtState.Inactive;
        }
    }
}
