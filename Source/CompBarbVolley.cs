using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DeadShip
{
    public class CompProperties_BarbVolley : CompProperties
    {
        // Longest and shortest distance it will fire from. Closer than minRange it lashes in melee.
        public float range = 20f;
        public float minRange = 3f;

        // Time standing still before the volley, and time between volleys.
        public int warmupTicks = 25;
        public int cooldownTicks = 150;

        // Barbs per volley, damage of one barb and how much armour each ignores.
        public int barbs = 3;
        public float damage = 6f;
        public float armorPenetration = 0.35f;

        // Chance one barb hits: hitChanceNear at minRange falling to hitChanceFar at full range.
        // Cover lowers it further (coverFactor 1 = same as a bullet, 0 = ignored).
        public float hitChanceNear = 0.85f;
        public float hitChanceFar = 0.4f;
        public float coverFactor = 1f;

        // Flying barb: speed in cells per second, and size of the picture.
        public float projectileSpeed = 28f;
        public float projectileScale = 0.5f;

        // With Combat Extended running, each barb is a real CE projectile (this def, added by
        // Patches/CE_Barb.xml) fired with a random spread of this many degrees, and CE decides
        // what it hits (cover, armour, body height). The hit-chance numbers above are then unused.
        public string ceProjectileDef = "DS_Projectile_Barb";
        public float ceSpreadDegrees = 2.5f;

        // The barbs are fired from these body parts. With none left it cannot fire.
        public string firingPartDef = "DS_Tentacle";

        public CompProperties_BarbVolley()
        {
            compClass = typeof(CompBarbVolley);
        }
    }

    // A volley of tentacle barbs, written as plain code like the puker's spit so it does not depend
    // on projectiles (which Combat Extended replaces). Each barb flies as a picture and lands separately.
    public class CompBarbVolley : ThingComp
    {
        private class PendingBarb
        {
            public Pawn target;
            public int tick;
            public bool hit;
        }

        private int nextVolleyTick;
        private int fireAtTick;
        private readonly List<PendingBarb> pending = new List<PendingBarb>();

        public CompProperties_BarbVolley Props
        {
            get { return (CompProperties_BarbVolley)props; }
        }

        public override void CompTick()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return;
            }

            if (!pawn.IsHashIntervalTick(10))
            {
                return;
            }

            int now = Find.TickManager.TicksGame;

            // Barbs in flight land when they arrive.
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (now >= pending[i].tick)
                {
                    PendingBarb barb = pending[i];
                    pending.RemoveAt(i);
                    Impact(pawn, barb);
                }
            }

            if (fireAtTick != 0)
            {
                if (MeleeBusy(pawn))
                {
                    fireAtTick = 0;
                    return;
                }
                if (now >= fireAtTick)
                {
                    fireAtTick = 0;
                    nextVolleyTick = now + Props.cooldownTicks;
                    Fire(pawn);
                }
                return;
            }

            if (now < nextVolleyTick || !CanFire(pawn) || MeleeBusy(pawn))
            {
                return;
            }

            Pawn aimAt = FindTarget(pawn);
            if (aimAt == null)
            {
                return;
            }

            // Stand still while it takes aim.
            fireAtTick = now + Props.warmupTicks;
            pawn.pather.StopDead();
            pawn.rotationTracker.FaceTarget(aimAt);
            Job wait = JobMaker.MakeJob(JobDefOf.Wait, aimAt);
            wait.expiryInterval = Props.warmupTicks;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
        }

        // It can fire while at least one firing part (tentacle) is still attached.
        // One attack at a time: true while the creature is mid-melee or has an enemy in reach.
        private static bool MeleeBusy(Pawn pawn)
        {
            if (pawn.CurJobDef == JobDefOf.AttackMelee) { return true; }
            Stance_Busy busy = pawn.stances.curStance as Stance_Busy;
            if (busy != null && busy.verb != null && busy.verb.IsMeleeAttack) { return true; }
            IReadOnlyList<Pawn> all = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other != pawn && !other.Dead && !other.Downed && pawn.HostileTo(other)
                    && (other.Position - pawn.Position).LengthHorizontalSquared <= 2) { return true; }
            }
            return false;
        }

        private bool CanFire(Pawn pawn)
        {
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def.defName == Props.firingPartDef)
                {
                    return true;
                }
            }
            return false;
        }

        // The nearest living enemy in clear view, inside firing range but not close enough to just fight.
        private Pawn FindTarget(Pawn pawn)
        {
            IReadOnlyList<Pawn> all = pawn.Map.mapPawns.AllPawnsSpawned;
            Pawn best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == pawn || other.Dead || other.Downed)
                {
                    continue;
                }
                if (!pawn.HostileTo(other))
                {
                    continue;
                }

                float distance = (other.Position - pawn.Position).LengthHorizontal;
                if (distance > Props.range || distance < Props.minRange || distance >= bestDistance)
                {
                    continue;
                }
                if (!GenSight.LineOfSight(pawn.Position, other.Position, pawn.Map))
                {
                    continue;
                }

                best = other;
                bestDistance = distance;
            }

            return best;
        }

        private void Fire(Pawn pawn)
        {
            if (pawn.Dead || !pawn.Spawned || pawn.Map == null || !CanFire(pawn))
            {
                return;
            }

            if (MeleeBusy(pawn))
            {
                return;
            }

            Pawn target = FindTarget(pawn);
            if (target == null)
            {
                return;
            }
            pawn.rotationTracker.FaceTarget(target);

            // Combat Extended: fire real CE projectiles instead of the picture-only barbs below.
            if (TryFireCE(pawn, target))
            {
                return;
            }

            Map map = pawn.Map;
            Vector3 from = pawn.DrawPos;
            int now = Find.TickManager.TicksGame;
            float cover = CoverUtility.CalculateOverallBlockChance(target, pawn.Position, map);
            float span = Mathf.Max(0.01f, Props.range - Props.minRange);

            for (int b = 0; b < Props.barbs; b++)
            {
                Vector3 to = target.DrawPos;

                // Each barb rolls separately. Further away and behind cover is harder.
                float t01 = Mathf.Clamp01(((to - from).magnitude - Props.minRange) / span);
                float chance = Mathf.Lerp(Props.hitChanceNear, Props.hitChanceFar, t01);
                chance *= 1f - Mathf.Clamp01(cover * Props.coverFactor);
                bool hit = Rand.Chance(chance);
                if (!hit)
                {
                    Vector3 offset = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
                    if (offset.sqrMagnitude < 0.01f) { offset = new Vector3(1f, 0f, 0f); }
                    to += offset.normalized * Rand.Range(0.8f, 2.5f);
                }

                Vector3 along = to - from;
                along.y = 0f;
                float distance = along.magnitude;
                float speed = (Props.projectileSpeed > 0f ? Props.projectileSpeed : 28f) * (1f + 0.07f * b);
                float flightSeconds = distance / speed;

                float angle = along.AngleFlat();
                string direction;
                if (angle >= 315f || angle < 45f) { direction = "up"; }
                else if (angle < 135f) { direction = "right"; }
                else if (angle < 225f) { direction = "down"; }
                else { direction = "left"; }

                FleckDef def = DefDatabase<FleckDef>.GetNamedSilentFail("DS_Barb_" + direction);
                if (def != null)
                {
                    FleckCreationData data = FleckMaker.GetDataStatic(from, map, def, Props.projectileScale);
                    data.velocityAngle = angle;
                    data.velocitySpeed = speed;
                    data.solidTimeOverride = flightSeconds;
                    map.flecks.CreateFleck(data);
                }

                PendingBarb barb = new PendingBarb();
                barb.target = target;
                barb.hit = hit;
                barb.tick = now + Mathf.CeilToInt(flightSeconds * 60f);
                pending.Add(barb);
            }
        }

        private static bool loggedFallback;
        private static bool loggedCE;

        private bool TryFireCE(Pawn pawn, Pawn target)
        {
#if CE
            if (!ModsConfig.IsActive("ceteam.combatextended"))
            {
                return false;
            }
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(Props.ceProjectileDef);
            if (def == null)
            {
                if (!loggedFallback)
                {
                    loggedFallback = true;
                    Log.Warning("Dead Ship: Combat Extended is running but the lurker barbs projectile def '" + Props.ceProjectileDef + "' was not found, so the picture-only version is used.");
                }
                return false;
            }
            bool any = false;
            for (int b = 0; b < Props.barbs; b++)
            {
                if (CELauncher.TryLaunch(pawn, target, def, Props.ceSpreadDegrees))
                {
                    any = true;
                    if (!loggedCE)
                    {
                        loggedCE = true;
                        Log.Message("Dead Ship: the lurker fired a Combat Extended projectile (" + def.defName + ").");
                    }
                }
            }
            return any;
#else
            return false;
#endif
        }

        // A barb arrives: a piercing wound if it hit.
        private void Impact(Pawn shooter, PendingBarb barb)
        {
            Pawn target = barb.target;
            if (!barb.hit || target == null || target.Dead || !target.Spawned || target.Map == null)
            {
                return;
            }

            FleckMaker.ThrowMicroSparks(target.DrawPos, target.Map);
            target.TakeDamage(new DamageInfo(DamageDefOf.Stab, Props.damage, Props.armorPenetration, -1f, shooter));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextVolleyTick, "nextVolleyTick", 0);
            Scribe_Values.Look(ref fireAtTick, "fireAtTick", 0);
        }
    }
}
