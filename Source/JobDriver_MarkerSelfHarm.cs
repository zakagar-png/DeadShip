using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeadShip
{
    // Part of the Marker's call: a colonist standing at the Marker cuts themselves on their own limbs. It is never meant to kill:
    // the cuts are small, only on arms and legs, and they stop once the colonist is hurt or bleeding badly.
    public class JobDriver_MarkerSelfHarm : JobDriver
    {
        private static readonly string[] Limbs = { "Arm", "Hand", "Leg", "Foot" };

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                pawn.pather.StopDead();
            };
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.defaultDuration = 400;
            toil.tickAction = delegate
            {
                if (pawn.IsHashIntervalTick(100))
                {
                    Cut();
                }
            };
            yield return toil;
        }

        // True when the colonist is still healthy enough to be hurt a little more.
        public static bool CanStillHarm(Pawn p)
        {
            return p.health.summaryHealth.SummaryHealthPercent > 0.6f && p.health.hediffSet.BleedRateTotal < 0.25f;
        }

        private void Cut()
        {
            if (!CanStillHarm(pawn))
            {
                return;
            }
            List<BodyPartRecord> options = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.depth == BodyPartDepth.Outside && System.Array.IndexOf(Limbs, part.def.defName) >= 0)
                {
                    options.Add(part);
                }
            }
            if (options.Count == 0)
            {
                return;
            }
            BodyPartRecord target = options.RandomElement();
            DamageInfo info = new DamageInfo(DamageDefOf.Cut, Rand.RangeInclusive(2, 4), 0f, -1f, null, target);
            pawn.TakeDamage(info);
        }
    }
}
