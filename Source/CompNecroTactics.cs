using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DeadShip
{
    public class CompProperties_Tactics : CompProperties
    {
        // AMBUSH (lurker): in a raid, instead of charging it creeps to a hidden spot with cover near the
        // colonists, waits there, and springs when someone comes close, it is hurt, or it has waited long enough.
        public bool ambush = false;
        public float ambushMinDistance = 14f;       // spot is at least this far from the nearest colonist
        public float ambushMaxDistance = 24f;       // ...and at most this far
        public float ambushTriggerRange = 9f;       // springs when an enemy comes this close
        public int ambushApproachTicks = 1800;      // gives up creeping after this long and just attacks
        public int ambushWaitTicks = 2400;          // waits this long at the most

        // KEEP DISTANCE (puker): holds still and spits from where it is instead of walking into melee,
        // and backs away when an enemy gets within retreatRange.
        public bool keepDistance = false;
        public float holdRange = 16f;               // holds still while an enemy in clear view is within this range
        public float retreatRange = 5f;             // backs off when an enemy is this close
        public int retreatCooldownTicks = 90;
        public int maxRetreats = 5;                 // then it stands and fights

        // FINISH THE DOWNED: with no live enemy close by, it walks to a downed colonist and kills them.
        public bool finishDowned = false;
        public float finishRange = 30f;             // looks for downed enemies this far away
        public float finishLiveEnemyRange = 10f;    // ...but only while no standing enemy is this close

        public CompProperties_Tactics()
        {
            compClass = typeof(CompNecroTactics);
        }
    }

    // How a necromorph behaves in a raid, on top of the standard "attack the colony" order.
    // Done from CompTick like the spit and barb comps: it hands the pawn short Goto / Wait jobs.
    public class CompNecroTactics : ThingComp
    {
        private const int Pending = 0;
        private const int Approach = 1;
        private const int Waiting = 2;
        private const int Done = 3;

        private int state = Pending;
        private IntVec3 spot = IntVec3.Invalid;
        private int stateStartTick;
        private int nextRetreatTick;
        private int retreats;

        public CompProperties_Tactics Props
        {
            get { return (CompProperties_Tactics)props; }
        }

        // For a creature that should attack at once instead of creeping to an ambush spot.
        public void SkipAmbush()
        {
            state = Done;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref state, "ds_tacticsState", Pending);
            Scribe_Values.Look(ref spot, "ds_tacticsSpot", IntVec3.Invalid);
            Scribe_Values.Look(ref stateStartTick, "ds_tacticsStart", 0);
            Scribe_Values.Look(ref retreats, "ds_tacticsRetreats", 0);
        }

        public override void CompTick()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return;
            }
            // Holding still: undo any step the fight AI tries to take. Not while it is deliberately backing away.
            if (holdUntilTick != 0 && pawn.pather.Moving)
            {
                int tick = Find.TickManager.TicksGame;
                if (tick < holdUntilTick && tick >= retreatUntilTick)
                {
                    pawn.pather.StopDead();
                }
            }

            if (!pawn.IsHashIntervalTick(30))
            {
                return;
            }

            if (Props.ambush)
            {
                TickAmbush(pawn);
            }
            if (Props.keepDistance)
            {
                TickKeepDistance(pawn);
            }
            if (Props.finishDowned)
            {
                TickFinishDowned(pawn);
            }
        }

        // ---------------- shared helpers ----------------

        private static bool InMelee(Pawn pawn)
        {
            if (pawn.CurJobDef == JobDefOf.AttackMelee) { return true; }
            IReadOnlyList<Pawn> all = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other != pawn && !other.Dead && !other.Downed && pawn.HostileTo(other)
                    && (other.Position - pawn.Position).LengthHorizontalSquared <= 2) { return true; }
            }
            return false;
        }

        private static List<Pawn> Enemies(Pawn pawn)
        {
            List<Pawn> list = new List<Pawn>();
            IReadOnlyList<Pawn> all = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other != pawn && !other.Dead && !other.Downed && other.RaceProps.Humanlike && pawn.HostileTo(other))
                {
                    list.Add(other);
                }
            }
            return list;
        }

        private static Pawn Nearest(Pawn pawn, List<Pawn> enemies, out float distance)
        {
            Pawn best = null;
            distance = float.MaxValue;
            for (int i = 0; i < enemies.Count; i++)
            {
                float d = (enemies[i].Position - pawn.Position).LengthHorizontal;
                if (d < distance) { distance = d; best = enemies[i]; }
            }
            return best;
        }

        // Ticks until which it should stay put. The job system keeps handing a raider a "go and fight" job, so
        // CompTick also stops any movement while this is in the future (see CompTick).
        private int holdUntilTick;
        private int retreatUntilTick;

        private void Hold(Pawn pawn, int ticks)
        {
            holdUntilTick = Find.TickManager.TicksGame + ticks;
            pawn.pather.StopDead();
            Job wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = ticks;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
        }

        // ---------------- ambush ----------------

        private void TickAmbush(Pawn pawn)
        {
            int now = Find.TickManager.TicksGame;

            if (state == Done)
            {
                return;
            }

            if (state == Pending)
            {
                // Only raiders do this. A lurker spawned any other way just fights.
                if (pawn.GetLord() == null)
                {
                    state = Done;
                    return;
                }

                List<Pawn> enemies = Enemies(pawn);
                if (enemies.Count == 0)
                {
                    state = Done;
                    return;
                }

                spot = FindAmbushSpot(pawn, enemies);
                if (!spot.IsValid)
                {
                    state = Done;   // nowhere to hide: charge like the others
                    return;
                }

                state = Approach;
                stateStartTick = now;
            }

            if (Sprung(pawn))
            {
                Spring(pawn);
                return;
            }

            if (state == Approach)
            {
                if (pawn.Position.DistanceTo(spot) <= 1.5f)
                {
                    state = Waiting;
                    stateStartTick = now;
                    Hold(pawn, 90);
                    return;
                }
                if (now - stateStartTick > Props.ambushApproachTicks)
                {
                    Spring(pawn);
                    return;
                }
                if (pawn.CurJobDef != JobDefOf.Goto)
                {
                    Job go = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(spot));
                    go.locomotionUrgency = LocomotionUrgency.Walk;
                    go.expiryInterval = 600;
                    pawn.jobs.StartJob(go, JobCondition.InterruptForced);
                }
                return;
            }

            if (state == Waiting)
            {
                if (now - stateStartTick > Props.ambushWaitTicks)
                {
                    Spring(pawn);
                    return;
                }
                Hold(pawn, 90);
            }
        }

        // Someone is close, or it has been hurt.
        private bool Sprung(Pawn pawn)
        {
            if (pawn.health.summaryHealth.SummaryHealthPercent < 0.98f)
            {
                return true;
            }
            float distance;
            Nearest(pawn, Enemies(pawn), out distance);
            return distance <= Props.ambushTriggerRange;
        }

        private void Spring(Pawn pawn)
        {
            state = Done;
            if (pawn.CurJobDef == JobDefOf.Wait || pawn.CurJobDef == JobDefOf.Goto)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        }

        // A standable, reachable cell the right distance from the colonists with cover next to it and,
        // best of all, out of sight of every colonist.
        private IntVec3 FindAmbushSpot(Pawn pawn, List<Pawn> enemies)
        {
            Map map = pawn.Map;
            float nearest;
            Pawn target = Nearest(pawn, enemies, out nearest);
            if (target == null)
            {
                return IntVec3.Invalid;
            }

            List<KeyValuePair<float, IntVec3>> scored = new List<KeyValuePair<float, IntVec3>>();
            foreach (IntVec3 c in GenRadial.RadialCellsAround(target.Position, Props.ambushMaxDistance, false))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c.Fogged(map))
                {
                    continue;
                }
                float d = (c - target.Position).LengthHorizontal;
                if (d < Props.ambushMinDistance)
                {
                    continue;
                }

                int seen = 0;
                for (int i = 0; i < enemies.Count; i++)
                {
                    if ((enemies[i].Position - c).LengthHorizontalSquared <= 40f * 40f
                        && GenSight.LineOfSight(c, enemies[i].Position, map))
                    {
                        seen++;
                    }
                }

                float cover = CoverUtility.TotalSurroundingCoverScore(c, map);
                if (seen > 0 && cover < 0.3f)
                {
                    continue;
                }

                float score = cover * 10f - seen * 20f - (c - pawn.Position).LengthHorizontal * 0.05f
                              - Mathf.Abs(d - (Props.ambushMinDistance + 4f)) * 0.5f;
                scored.Add(new KeyValuePair<float, IntVec3>(score, c));
            }

            scored.Sort(delegate (KeyValuePair<float, IntVec3> a, KeyValuePair<float, IntVec3> b)
            {
                return b.Key.CompareTo(a.Key);
            });

            TraverseParms traverse = TraverseParms.For(pawn);
            for (int i = 0; i < scored.Count && i < 12; i++)
            {
                if (map.reachability.CanReach(pawn.Position, scored[i].Value, PathEndMode.OnCell, traverse))
                {
                    return scored[i].Value;
                }
            }
            return IntVec3.Invalid;
        }

        // ---------------- finish the downed ----------------

        private void TickFinishDowned(Pawn pawn)
        {
            // Hiding in ambush, or already attacking something: leave it alone.
            if (state == Waiting || state == Approach)
            {
                return;
            }
            if (pawn.CurJobDef == JobDefOf.AttackMelee)
            {
                return;
            }

            // A standing enemy close by comes first.
            float liveDistance;
            Nearest(pawn, Enemies(pawn), out liveDistance);
            if (liveDistance <= Props.finishLiveEnemyRange)
            {
                return;
            }

            Pawn victim = null;
            float best = Props.finishRange;
            IReadOnlyList<Pawn> all = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == pawn || other.Dead || !other.Downed || !other.RaceProps.Humanlike || !pawn.HostileTo(other))
                {
                    continue;
                }
                float d = (other.Position - pawn.Position).LengthHorizontal;
                if (d < best && pawn.CanReach(other, PathEndMode.Touch, Danger.Deadly))
                {
                    best = d;
                    victim = other;
                }
            }
            if (victim == null)
            {
                return;
            }

            Job kill = JobMaker.MakeJob(JobDefOf.AttackMelee, victim);
            kill.killIncappedTarget = true;
            pawn.jobs.StartJob(kill, JobCondition.InterruptForced);
        }

        // ---------------- keep distance ----------------

        private void TickKeepDistance(Pawn pawn)
        {
            int now = Find.TickManager.TicksGame;

            CompAcidSpit spit = pawn.TryGetComp<CompAcidSpit>();
            if (spit != null && spit.WindingUp)
            {
                return;     // the spit is already holding it still
            }
            if (InMelee(pawn))
            {
                return;
            }

            // Nearest enemy in clear view.
            Pawn threat = null;
            float distance = float.MaxValue;
            List<Pawn> enemies = Enemies(pawn);
            for (int i = 0; i < enemies.Count; i++)
            {
                float d = (enemies[i].Position - pawn.Position).LengthHorizontal;
                if (d < distance && d <= Props.holdRange && GenSight.LineOfSight(pawn.Position, enemies[i].Position, pawn.Map))
                {
                    distance = d;
                    threat = enemies[i];
                }
            }
            if (threat == null)
            {
                return;
            }

            // Too close: back away, a few times at most.
            if (distance <= Props.retreatRange && retreats < Props.maxRetreats && now >= nextRetreatTick)
            {
                IntVec3 cell = FindRetreatCell(pawn, threat, distance);
                if (cell.IsValid)
                {
                    retreats++;
                    nextRetreatTick = now + Props.retreatCooldownTicks;
                    holdUntilTick = 0;
                    retreatUntilTick = now + 120;
                    Job go = JobMaker.MakeJob(JobDefOf.Goto, new LocalTargetInfo(cell));
                    go.locomotionUrgency = LocomotionUrgency.Jog;
                    go.expiryInterval = 120;
                    pawn.jobs.StartJob(go, JobCondition.InterruptForced);
                    return;
                }
            }

            // In spitting range and clear view: stay put and spit rather than walking in.
            if (distance > Props.retreatRange && pawn.CurJobDef != JobDefOf.Goto)
            {
                pawn.rotationTracker.FaceTarget(threat);
                Hold(pawn, 45);
            }
        }

        private IntVec3 FindRetreatCell(Pawn pawn, Pawn threat, float distance)
        {
            Map map = pawn.Map;
            IntVec3 best = IntVec3.Invalid;
            float bestScore = float.MinValue;
            TraverseParms traverse = TraverseParms.For(pawn);

            foreach (IntVec3 c in GenRadial.RadialCellsAround(pawn.Position, 6f, false))
            {
                if (!c.InBounds(map) || !c.Standable(map))
                {
                    continue;
                }
                float d = (c - threat.Position).LengthHorizontal;
                if (d < distance + 2f || d > Props.holdRange - 1f)
                {
                    continue;
                }
                float score = d + (GenSight.LineOfSight(c, threat.Position, map) ? 2f : 0f);
                if (score > bestScore && map.reachability.CanReach(pawn.Position, c, PathEndMode.OnCell, traverse))
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }
    }
}
