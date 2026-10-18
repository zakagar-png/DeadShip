using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DeadShip
{
    public class CompProperties_Leap : CompProperties
    {
        // Leaps at an enemy between these distances (tiles).
        public float minRange = 2.5f;
        public float maxRange = 5.5f;
        public int cooldownTicks = 420;

        // Hediff put on a pinned victim, and the flying stand-in used for the jump.
        public string pinHediff = "DS_Pinned";

        // It springs with this body part; without it the leaper cannot leap.
        public string springPartDef = "DS_Tail";

        public CompProperties_Leap()
        {
            compClass = typeof(CompLeap);
        }
    }

    // The leaper's pounce. It jumps onto a victim from a few tiles away and pins them: while it lives
    // and stays up, the victim cannot walk. Kill the leaper (or down it) to free them.
    public class CompLeap : ThingComp
    {
        private int nextLeapTick;
        private Pawn pinned;

        public CompProperties_Leap Props
        {
            get { return (CompProperties_Leap)props; }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextLeapTick, "ds_nextLeap", 0);
            Scribe_References.Look(ref pinned, "ds_pinned");
        }

        public override void CompTick()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            if (pinned != null)
            {
                if (pawn.Dead || pawn.Downed || pinned.Dead || pinned.Downed || !pinned.Spawned
                    || pinned.Map != pawn.Map || (pinned.Position - pawn.Position).LengthHorizontalSquared > 8)
                {
                    Release();
                }
                else
                {
                    // Held down: no walking.
                    pinned.pather.StopDead();
                    pawn.pather.StopDead();
                    if (pawn.CurJobDef != JobDefOf.AttackMelee && pawn.IsHashIntervalTick(15))
                    {
                        Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, pinned);
                        pawn.jobs.StartJob(attack, JobCondition.InterruptForced);
                    }
                    return;
                }
            }

            if (pawn.Dead || pawn.Downed || !pawn.IsHashIntervalTick(10))
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (now < nextLeapTick)
            {
                return;
            }

            if (!HasSpringPart(pawn))
            {
                return;
            }

            Pawn victim = FindTarget(pawn);
            if (victim == null)
            {
                return;
            }

            IntVec3 land = LandingCell(pawn, victim);
            if (!land.IsValid)
            {
                return;
            }

            nextLeapTick = now + Props.cooldownTicks;
            pawn.pather.StopDead();
            pawn.rotationTracker.FaceTarget(victim);
            Map map = pawn.Map;
            FleckMaker.ThrowDustPuff(pawn.Position, map, 1.2f);

            // The leap itself: it is moved straight to the landing cell and its drawn position is left to catch up,
            // so it visibly springs across. (The game's flying-pawn code made it vanish in play, so it is not used.)
            pawn.Position = land;
            pawn.Notify_Teleported(false, false);
            Pin(pawn, victim);
        }

        private bool HasSpringPart(Pawn pawn)
        {
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def.defName == Props.springPartDef)
                {
                    return true;
                }
            }
            return false;
        }

        private Pawn FindTarget(Pawn pawn)
        {
            IReadOnlyList<Pawn> all = pawn.Map.mapPawns.AllPawnsSpawned;
            Pawn best = null;
            float bestDist = float.MaxValue;
            float min = Props.minRange;
            float max = Props.maxRange;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == pawn || other.Dead || other.Downed || !pawn.HostileTo(other))
                {
                    continue;
                }
                if (other.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail(Props.pinHediff)))
                {
                    continue;
                }
                float d = (other.Position - pawn.Position).LengthHorizontal;
                if (d < min || d > max || d >= bestDist)
                {
                    continue;
                }
                if (!GenSight.LineOfSight(pawn.Position, other.Position, pawn.Map))
                {
                    continue;
                }
                best = other;
                bestDist = d;
            }
            return best;
        }

        // A free cell next to the victim, nearest to the leaper.
        private static IntVec3 LandingCell(Pawn pawn, Pawn victim)
        {
            IntVec3 best = IntVec3.Invalid;
            float bestDist = float.MaxValue;
            Map map = pawn.Map;
            for (int i = 0; i < 8; i++)
            {
                IntVec3 c = victim.Position + GenAdj.AdjacentCells[i];
                if (!c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map) != null)
                {
                    continue;
                }
                if (c.GetFirstPawn(map) != null)
                {
                    continue;
                }
                float d = (c - pawn.Position).LengthHorizontalSquared;
                if (d < bestDist)
                {
                    best = c;
                    bestDist = d;
                }
            }
            return best;
        }

        private void Pin(Pawn pawn, Pawn target)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(Props.pinHediff);
            if (def != null && !target.health.hediffSet.HasHediff(def))
            {
                target.health.AddHediff(HediffMaker.MakeHediff(def, target));
            }
            pinned = target;
            target.pather.StopDead();
            if (target.jobs != null)
            {
                target.jobs.EndCurrentJob(JobCondition.InterruptForced, true, false);
            }
            Messages.Message(target.LabelShortCap + " is pinned by a leaper!", target, MessageTypeDefOf.ThreatBig, false);
            FleckMaker.ThrowDustPuff(target.Position, target.Map, 1.5f);
            Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            pawn.jobs.StartJob(attack, JobCondition.InterruptForced);
        }

        private void Release()
        {
            if (pinned != null)
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(Props.pinHediff);
                if (def != null)
                {
                    Hediff h = pinned.health.hediffSet.GetFirstHediffOfDef(def);
                    if (h != null)
                    {
                        pinned.health.RemoveHediff(h);
                    }
                }
            }
            pinned = null;
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            Release();
        }
    }
}
