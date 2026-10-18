using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DeadShip
{
    // The dead zone: necromorphs are drawn to an active Marker or altar, but cannot stand within a few cells of it.
    // They gather at the edge of the zone, and any that end up inside are driven out.
    public static class DeadZone
    {
        // How many cells out from the Marker the zone reaches.
        public const float Radius = 6f;

        public static bool IsNecro(Pawn p)
        {
            return p != null && p.def != null && p.def.defName.StartsWith("DS_");
        }

        // The active Marker/altar whose zone contains this cell, or null.
        public static Thing ZoneAt(Map map, IntVec3 c)
        {
            List<Thing> sources = MarkerUtility.ActiveAltars(map);
            for (int i = 0; i < sources.Count; i++)
            {
                if ((c - sources[i].Position).LengthHorizontal <= Radius) return sources[i];
            }
            return null;
        }

        public static Thing NearestSource(Map map, IntVec3 from)
        {
            List<Thing> sources = MarkerUtility.ActiveAltars(map);
            Thing best = null;
            float bd = 999999f;
            for (int i = 0; i < sources.Count; i++)
            {
                float d = (from - sources[i].Position).LengthHorizontalSquared;
                if (d < bd) { bd = d; best = sources[i]; }
            }
            return best;
        }

        // A standable cell just outside the zone, as near as possible to the point 'toward'.
        public static IntVec3 EdgeCell(Pawn pawn, Thing source, IntVec3 toward)
        {
            Map map = pawn.Map;
            Vector3 dir = (toward - source.Position).ToVector3();
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = new Vector3(1f, 0f, 0f);
            dir.Normalize();
            IntVec3 want = source.Position + new IntVec3(Mathf.RoundToInt(dir.x * (Radius + 1.5f)), 0, Mathf.RoundToInt(dir.z * (Radius + 1.5f)));
            int n = GenRadial.NumCellsInRadius(8f);
            for (int i = 0; i < n; i++)
            {
                IntVec3 c = want + GenRadial.RadialPattern[i];
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                if ((c - source.Position).LengthHorizontal <= Radius + 0.5f) continue;
                if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        public static Job GoJob(Pawn pawn, IntVec3 cell)
        {
            if (!cell.IsValid || cell == pawn.Position) return null;
            Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.expiryInterval = 200;
            job.checkOverrideOnExpire = true;
            return job;
        }

        // Nothing to hunt: head for the edge of the nearest zone.
        public static Job DrawnToMarker(Pawn pawn)
        {
            Thing src = NearestSource(pawn.Map, pawn.Position);
            if (src == null) return null;
            if ((pawn.Position - src.Position).LengthHorizontal <= Radius + 3f) return null;
            return GoJob(pawn, EdgeCell(pawn, src, pawn.Position));
        }
    }

    // Drives necromorphs out of the dead zone.
    public class MapComponent_DeadZone : MapComponent
    {
        public MapComponent_DeadZone(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % 15 != 0) return;
            List<Thing> sources = MarkerUtility.ActiveAltars(map);
            if (sources.Count == 0) return;
            IReadOnlyList<Pawn> all = map.mapPawns.AllPawnsSpawned;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                Pawn p = all[i];
                if (!DeadZone.IsNecro(p) || p.Dead || p.Downed) continue;
                Thing src = DeadZone.ZoneAt(map, p.Position);
                if (src == null) continue;
                IntVec3 cell = DeadZone.EdgeCell(p, src, p.Position);
                Job job = DeadZone.GoJob(p, cell);
                if (job != null) p.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }
    }
}
