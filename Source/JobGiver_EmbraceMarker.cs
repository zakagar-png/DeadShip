using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeadShip
{
    // The end of a long fall: a colonist whose sanity stayed at the bottom for too long walks to the nearest active Marker
    // and dies there. It cannot be talked out of it; the only way out is the Marker going away.
    public class JobGiver_EmbraceMarker : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            List<Thing> markers = MarkerUtility.ActiveAltars(pawn.Map);
            if (markers.Count == 0)
            {
                // Nothing left to walk to: the call fades.
                if (pawn.MentalState != null) pawn.MentalState.RecoverFromState();
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
            if (nearest == null) return null;

            if (best <= 3.5f)
            {
                JobDef end = DefDatabase<JobDef>.GetNamedSilentFail("DS_MarkerEmbrace");
                return end == null ? null : JobMaker.MakeJob(end);
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

    // Stands at the Marker for a moment, then dies.
    public class JobDriver_MarkerEmbrace : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil wait = new Toil();
            wait.initAction = delegate
            {
                pawn.pather.StopDead();
            };
            wait.defaultCompleteMode = ToilCompleteMode.Delay;
            wait.defaultDuration = 300;
            yield return wait;

            Toil end = new Toil();
            end.initAction = delegate
            {
                Messages.Message(pawn.LabelShort + " has died at the Marker.", pawn, MessageTypeDefOf.PawnDeath, false);
                pawn.Kill(null);
            };
            end.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return end;
        }
    }
}
