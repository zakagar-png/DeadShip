using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeadShip
{
    // The Marker's call: a colonist at the bottom of their sanity walks to the nearest active Marker and cuts themselves there until they are hurt, then stands staring.
    public class JobGiver_GoToMarker : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            List<Thing> markers = MarkerUtility.ActiveAltars(pawn.Map);
            if (markers.Count == 0)
            {
                return null;
            }

            Thing nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < markers.Count; i++)
            {
                float d = (pawn.Position - markers[i].Position).LengthHorizontal;
                if (d < best)
                {
                    best = d;
                    nearest = markers[i];
                }
            }
            if (nearest == null)
            {
                return null;
            }

            if (best <= 3.5f)
            {
                if (JobDriver_MarkerSelfHarm.CanStillHarm(pawn))
                {
                    JobDef harm = DefDatabase<JobDef>.GetNamedSilentFail("DS_MarkerSelfHarm");
                    if (harm != null)
                    {
                        return JobMaker.MakeJob(harm);
                    }
                }
                Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = Rand.RangeInclusive(200, 400);
                return wait;
            }

            IntVec3 spot = CellFinder.RandomClosewalkCellNear(nearest.Position, pawn.Map, 2, null);
            if (!spot.IsValid || !pawn.CanReach(spot, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn))
            {
                return null;
            }
            Job go = JobMaker.MakeJob(JobDefOf.Goto, spot);
            go.expiryInterval = 600;
            go.locomotionUrgency = LocomotionUrgency.Walk;
            return go;
        }
    }
}
