using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeadShip
{
    // A mood penalty while a colonist can see necromorphs. It grows with how many there are (a brute counts as three).
    // Stages are in Defs/ThoughtDefs/SeeNecromorph.xml.
    public class ThoughtWorker_SeeNecromorph : ThoughtWorker
    {
        private const float SightRange = 24f;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!p.Spawned || p.Map == null || !p.RaceProps.Humanlike || p.kindDef == null || p.kindDef.defName.StartsWith("DS_"))
            {
                return ThoughtState.Inactive;
            }

            int weight = 0;
            IReadOnlyList<Pawn> pawns = p.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other.Dead || other.kindDef == null || !other.kindDef.defName.StartsWith("DS_"))
                {
                    continue;
                }
                if ((other.Position - p.Position).LengthHorizontal > SightRange)
                {
                    continue;
                }
                if (!GenSight.LineOfSight(p.Position, other.Position, p.Map))
                {
                    continue;
                }
                weight += other.kindDef.defName == "DS_Brute" ? 3 : 1;
            }

            if (weight <= 0)
            {
                return ThoughtState.Inactive;
            }
            if (weight >= 8)
            {
                return ThoughtState.ActiveAtStage(3);
            }
            if (weight >= 4)
            {
                return ThoughtState.ActiveAtStage(2);
            }
            if (weight >= 2)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            return ThoughtState.ActiveAtStage(0);
        }
    }
}
