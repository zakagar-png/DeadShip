using RimWorld;
using Verse;

namespace DeadShip
{
    // Gives colonists a fear mood penalty that gets worse the closer they are to the Marker. The more fragments are loaded, the
    // further out each stage reaches.
    // The stages themselves (labels and mood values) are in Defs/ThoughtDefs/MarkerFear.xml.
    public class ThoughtWorker_NearMarker : ThoughtWorker
    {
        // Distances in cells. Index matches the stage index in the ThoughtDef:
        // stage 0 = within 6 cells (worst), stage 3 = within 30 cells (mildest).
        private static readonly float[] StageDistances = { 6f, 12f, 20f, 30f };

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!p.Spawned || p.Map == null || MarkerUtility.Susceptibility(p) <= 0f)
            {
                return ThoughtState.Inactive;
            }

            // Only a resonance altar with a fragment loaded counts. A fragment on the ground, an empty altar,
            // or a fragment carried off the map does nothing.
            var markers = MarkerUtility.ActiveAltars(p.Map);
            if (markers.Count == 0)
            {
                return ThoughtState.Inactive;
            }

            // Each source reaches out by its own fragment count, and a contained one ignores that growth and reaches only a quarter
            // of the one-fragment distances.
            float nearest = float.MaxValue;
            for (int i = 0; i < markers.Count; i++)
            {
                // A contained Marker's dread does not grow with its fragments: it reaches a quarter of the one-fragment distances.
                float reach = MarkerUtility.IsContained(markers[i])
                    ? MarkerUtility.ContainedReach
                    : MarkerUtility.RangeFactor(MarkerUtility.FragmentsLoaded(markers[i]));
                reach *= MarkerResearch.DreadMultiplier();
                float distance = (p.Position - markers[i].Position).LengthHorizontal / reach;
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }

            for (int stage = 0; stage < StageDistances.Length; stage++)
            {
                if (nearest <= StageDistances[stage])
                {
                    // Memory suppressants push the dread one step further away.
                    if (MarkerUtility.IsSuppressed(p))
                    {
                        stage++;
                        if (stage >= StageDistances.Length)
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
