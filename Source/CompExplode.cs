using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DeadShip
{
    public class CompProperties_Explode : CompProperties
    {
        // The blast: radius in cells, damage at the centre and how much armour it ignores.
        public float radius = 2.9f;
        public int damage = 28;
        public float armorPenetration = 0.4f;

        // With Combat Extended running the blast is a CE explosion, which uses CE's own damage scale
        // (damage and armour penetration in its units, not the vanilla numbers above).
        public int ceDamage = 60;
        public float ceArmorPenetration = 3f;

        // It charges, and when an enemy is this close it stops and starts a fuse (it can still be killed
        // in that time, and it still goes off). 0 = no fuse, it only explodes when it dies.
        public float fuseRange = 1.9f;
        public int fuseTicks = 40;

        public CompProperties_Explode()
        {
            compClass = typeof(CompExplode);
        }
    }

    // The exploder's blast. It goes off when it dies, however it dies (shot, cut apart, sac destroyed),
    // and also on purpose when it reaches an enemy.
    public class CompExplode : ThingComp
    {
        private int fuseEndTick = 0;
        private bool exploded;

        public CompProperties_Explode Props
        {
            get { return (CompProperties_Explode)props; }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref fuseEndTick, "ds_fuseEnd", 0);
            Scribe_Values.Look(ref exploded, "ds_exploded", false);
        }

        public override void CompTick()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Map == null || Props.fuseTicks <= 0)
            {
                return;
            }
            if (!pawn.IsHashIntervalTick(5))
            {
                return;
            }

            int now = Find.TickManager.TicksGame;

            if (fuseEndTick != 0)
            {
                FleckMaker.ThrowMicroSparks(pawn.DrawPos, pawn.Map);
                if (now >= fuseEndTick)
                {
                    pawn.Kill(null);    // dying sets off the blast (Notify_Killed)
                    return;
                }
                pawn.pather.StopDead();
                return;
            }

            if (EnemyClose(pawn) || AtBreachTarget(pawn))
            {
                fuseEndTick = now + Props.fuseTicks;
                pawn.pather.StopDead();
                Job wait = JobMaker.MakeJob(JobDefOf.Wait);
                wait.expiryInterval = Props.fuseTicks + 10;
                pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            }
        }

        // Walking up to a wall or door it means to breach: set itself off next to it.
        private bool AtBreachTarget(Pawn pawn)
        {
            Job job = pawn.CurJob;
            if (job == null || job.def != JobDefOf.Goto)
            {
                return false;
            }
            Thing t = job.targetB.Thing;
            return t != null && t.Spawned && (t.Position - pawn.Position).LengthHorizontalSquared <= 2.3f * 2.3f;
        }

        private bool EnemyClose(Pawn pawn)
        {
            float sq = Props.fuseRange * Props.fuseRange;
            IReadOnlyList<Pawn> all = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other != pawn && !other.Dead && !other.Downed && pawn.HostileTo(other)
                    && (other.Position - pawn.Position).LengthHorizontalSquared <= sq)
                {
                    return true;
                }
            }
            return false;
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (exploded || prevMap == null)
            {
                return;
            }
            exploded = true;
#if CE
            if (ModsConfig.IsActive("ceteam.combatextended"))
            {
                try
                {
                    CEExplosion.Explode(parent.Position, prevMap, Props.radius, Props.ceDamage, Props.ceArmorPenetration, parent);
                    return;
                }
                catch (System.Exception ex)
                {
                    Log.Warning("Dead Ship: the Combat Extended explosion failed, using the vanilla one. " + ex.Message);
                }
            }
#endif
            GenExplosion.DoExplosion(parent.Position, prevMap, Props.radius, DamageDefOf.Bomb, parent,
                                     Props.damage, Props.armorPenetration);
        }
    }
}
