using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeadShip
{
    // True for necromorphs that must NOT break structures (everything except the exploder and brute).
    public class ThinkNode_ConditionalNecroPassive : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn p)
        {
            string n = p.def.defName;
            return n.StartsWith("DS_") && n != "DS_Exploder" && n != "DS_Brute";
        }
    }

    // True for the exploder and brute, which break walls and doors.
    public class ThinkNode_ConditionalNecroBreacher : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn p)
        {
            string n = p.def.defName;
            return n == "DS_Exploder" || n == "DS_Brute";
        }
    }

    // True for any necromorph.
    public class ThinkNode_ConditionalIsNecro : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn p)
        {
            return p.def.defName.StartsWith("DS_");
        }
    }

    public static class NecroPathing
    {
        // Necromorphs always know where the colonists are, even through walls.
        public static Pawn NearestColonist(Pawn pawn, bool needReach)
        {
            IReadOnlyList<Pawn> all = pawn.Map.mapPawns.AllPawnsSpawned;
            Pawn best = null;
            float bestDist = 999999f;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn o = all[i];
                if (o == pawn || o.Dead || !o.HostileTo(pawn)) continue;
                if (o.Faction == null || !o.Faction.IsPlayer) continue;
                float d = (o.Position - pawn.Position).LengthHorizontalSquared;
                if (d >= bestDist) continue;
                if (needReach && !pawn.CanReach(o, PathEndMode.Touch, Danger.Deadly)) continue;
                bestDist = d;
                best = o;
            }
            return best;
        }

        public static Job Approach(Pawn pawn, Pawn target)
        {
            // A colonist inside the dead zone can't be reached: wait at its edge, as near as possible.
            Thing zone = DeadZone.ZoneAt(pawn.Map, target.Position);
            if (zone != null)
            {
                IntVec3 edge = DeadZone.EdgeCell(pawn, zone, target.Position);
                if (edge.IsValid) return DeadZone.GoJob(pawn, edge);
                return null;
            }
            float d = (target.Position - pawn.Position).LengthHorizontalSquared;
            if (d <= 2.3f * 2.3f && pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly))
            {
                // Already next to it: hit it, rather than a Goto that ends the same tick.
                Job atk = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                atk.expiryInterval = 200;
                atk.checkOverrideOnExpire = true;
                return atk;
            }
            Job job = JobMaker.MakeJob(JobDefOf.Goto, target);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.expiryInterval = 200;
            job.checkOverrideOnExpire = true;
            return job;
        }

        // The reachable cell closest to the target (it can't get to them, so get as near as possible).
        public static Job GetNear(Pawn pawn, Pawn target)
        {
            IntVec3 best = IntVec3.Invalid;
            int n = GenRadial.NumCellsInRadius(45f);
            for (int i = 0; i < n; i++)
            {
                IntVec3 c = target.Position + GenRadial.RadialPattern[i];
                if (!c.InBounds(pawn.Map) || !c.Standable(pawn.Map)) continue;
                if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
                best = c;
                break;
            }
            if (!best.IsValid || best == pawn.Position) return null;
            if ((best - pawn.Position).LengthHorizontalSquared < 4f) return null;
            Job job = JobMaker.MakeJob(JobDefOf.Goto, best);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.expiryInterval = 300;
            job.checkOverrideOnExpire = true;
            return job;
        }

        // A wall or door joined (sides or corners) to at least minCount other wall and door cells,
        // so a lone wall or little fence placed as a decoy doesn't count.
        public static bool PartOfStructure(Thing start, int minCount)
        {
            Map map = start.Map;
            HashSet<IntVec3> seen = new HashSet<IntVec3>();
            Queue<IntVec3> q = new Queue<IntVec3>();
            seen.Add(start.Position);
            q.Enqueue(start.Position);
            while (q.Count > 0)
            {
                IntVec3 c = q.Dequeue();
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        IntVec3 n = new IntVec3(c.x + dx, 0, c.z + dz);
                        if (!n.InBounds(map) || seen.Contains(n)) continue;
                        Building e = n.GetEdifice(map);
                        if (e == null || !IsWallOrDoor(e)) continue;
                        seen.Add(n);
                        if (seen.Count >= minCount) return true;
                        q.Enqueue(n);
                    }
                }
            }
            return false;
        }

        public static bool IsWallOrDoor(Thing t)
        {
            Building b = t as Building;
            if (b == null) return false;
            if (b.def.IsDoor) return true;
            if (b.def.building != null && b.def.building.isNaturalRock) return false;
            return b.def.passability == Traversability.Impassable && b.def.fillPercent >= 0.99f;
        }
    }

    // Walks toward the colonists without ever attacking walls, doors or other buildings. If they can't be
    // reached it goes to the nearest spot it can.
    public class JobGiver_GotoHostileNoBreak : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null) return null;
            Pawn reachable = NecroPathing.NearestColonist(pawn, true);
            if (reachable != null) return NecroPathing.Approach(pawn, reachable);
            Pawn any = NecroPathing.NearestColonist(pawn, false);
            if (any == null) return DeadZone.DrawnToMarker(pawn);
            // Walled in: look for a way through the air ducts.
            if (MapComponent_Air.TryCrawl(pawn)) return null;
            return NecroPathing.GetNear(pawn, any);
        }
    }

    // Exploder and brute: go for the colonists, and if a wall or door is in the way, attack that (and
    // nothing else). Anything else in the way means getting as near as possible instead.
    public class JobGiver_BreachWallsDoors : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null) return null;
            Pawn reachable = NecroPathing.NearestColonist(pawn, true);
            if (reachable != null) return NecroPathing.Approach(pawn, reachable);
            Pawn any = NecroPathing.NearestColonist(pawn, false);
            if (any == null) return DeadZone.DrawnToMarker(pawn);

            // First wall or door on the straight line to the nearest colonist.
            IntVec3 prev = pawn.Position;
            foreach (IntVec3 c in GenSight.PointsOnLineOfSight(pawn.Position, any.Position))
            {
                if (!c.InBounds(pawn.Map)) break;
                Building e = c.GetEdifice(pawn.Map);
                if (e != null && NecroPathing.IsWallOrDoor(e)
                    && (pawn.def.defName != "DS_Exploder" || NecroPathing.PartOfStructure(e, 12)))
                {
                    if (pawn.def.defName == "DS_Exploder")
                    {
                        // Walks up to it and blows itself up (CompExplode lights the fuse next to its target).
                        Job go = JobMaker.MakeJob(JobDefOf.Goto, prev);
                        go.targetB = e;
                        go.locomotionUrgency = LocomotionUrgency.Sprint;
                        go.expiryInterval = 300;
                        go.checkOverrideOnExpire = true;
                        return go;
                    }
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, e);
                    job.expiryInterval = 300;
                    job.checkOverrideOnExpire = true;
                    return job;
                }
                prev = c;
            }
            return NecroPathing.GetNear(pawn, any);
        }
    }
}
