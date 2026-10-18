using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace DeadShip
{
    public class CompProperties_AcidSpit : CompProperties
    {
        // Longest and shortest distance it will spit from. Closer than minRange it just fights in melee.
        public float range = 18f;
        public float minRange = 3f;

        // Time standing still before the spit lands, and time between spits.
        public int warmupTicks = 45;
        public int cooldownTicks = 240;

        // Burn damage of one spit, how much armour it ignores, and how many hit points of
        // one worn piece of armour it eats away on top of the wound.
        public float damage = 9f;
        public float armorPenetration = 0.2f;
        public float armorWear = 12f;

        // Chance the spit hits: hitChanceNear right at minRange, falling in a straight line to
        // hitChanceFar at full range. A miss splashes down 1 to 3 cells away from the target.
        public float hitChanceNear = 0.9f;
        public float hitChanceFar = 0.45f;

        // How much cover (sandbags, walls, rocks between spitter and target) lowers the hit chance.
        // 1 = same effect as for a bullet, 0 = cover is ignored.
        public float coverFactor = 1f;

        // With Combat Extended running, the spit is a real CE projectile (this def, added by
        // Patches/CE_Barb.xml) with this much aim error in degrees. CE then decides what it hits (cover, armour),
        // and the hit-chance numbers above are unused. The puddle and armour wear still happen where it lands.
        public string ceProjectileDef = "DS_Projectile_AcidSpit";
        public float ceSpreadDegrees = 2f;

        // How fast the visible acid blob flies, in cells per second. The hit lands when it arrives.
        public float projectileSpeed = 18f;
        public float projectileScale = 0.7f;

        // The part that does the spitting. If it is gone the creature can no longer spit.
        public string spitPartDef = "DS_Head";

        public CompProperties_AcidSpit()
        {
            compClass = typeof(CompAcidSpit);
        }
    }

    // A ranged acid spit written as plain code, so it does not depend on projectiles
    // (which Combat Extended replaces). The creature stops, spits, then carries on with its normal AI.
    public class CompAcidSpit : ThingComp
    {
        private int nextSpitTick;
        private int fireAtTick;
        private Pawn pendingTarget;
        private int impactTick;
        private bool pendingHit;
        private Vector3 impactPos;

        // True while it stands still winding up a spit.
        public bool WindingUp
        {
            get { return fireAtTick != 0; }
        }

        public CompProperties_AcidSpit Props
        {
            get { return (CompProperties_AcidSpit)props; }
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

            // A spit in flight: the hit lands when the blob arrives.
            if (pendingTarget != null && now >= impactTick)
            {
                Pawn hit = pendingTarget;
                pendingTarget = null;
                Impact(pawn, hit);
            }

            // Winding up: when the timer runs out, the spit lands.
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
                    nextSpitTick = now + Props.cooldownTicks;
                    Fire(pawn);
                }
                return;
            }

            if (now < nextSpitTick || SpitPartGone(pawn) || MeleeBusy(pawn))
            {
                return;
            }

            Pawn aimAt = FindTarget(pawn);
            if (aimAt == null)
            {
                return;
            }

            // Stand still while it winds up.
            fireAtTick = now + Props.warmupTicks;
            pawn.pather.StopDead();
            pawn.rotationTracker.FaceTarget(aimAt);
            Job wait = JobMaker.MakeJob(JobDefOf.Wait, aimAt);
            wait.expiryInterval = Props.warmupTicks;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
        }

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

        private bool SpitPartGone(Pawn pawn)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_MissingPart gone = hediffs[i] as Hediff_MissingPart;
                if (gone != null && gone.Part != null && gone.Part.def.defName == Props.spitPartDef)
                {
                    return true;
                }
            }
            return false;
        }

        // The nearest living enemy in clear view, inside spitting range but not close enough to just fight.
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
            if (pawn.Dead || !pawn.Spawned || pawn.Map == null || SpitPartGone(pawn))
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

            // Combat Extended: fire a real CE projectile instead of the picture-only blob below.
            if (TryFireCE(pawn, target))
            {
                return;
            }

            // Launch the visible acid blob. The wound is applied when it arrives (see Impact).
            Map map = pawn.Map;
            Vector3 from = pawn.DrawPos;
            Vector3 to = target.DrawPos;

            // Roll to hit. Further away is harder. A miss lands off to one side.
            float span = Mathf.Max(0.01f, Props.range - Props.minRange);
            float t01 = Mathf.Clamp01(((to - from).magnitude - Props.minRange) / span);
            float chance = Mathf.Lerp(Props.hitChanceNear, Props.hitChanceFar, t01);
            float cover = CoverUtility.CalculateOverallBlockChance(target, pawn.Position, map);
            chance *= 1f - Mathf.Clamp01(cover * Props.coverFactor);
            bool hit = Rand.Chance(chance);
            if (!hit)
            {
                Vector3 offset = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
                if (offset.sqrMagnitude < 0.01f) { offset = new Vector3(1f, 0f, 0f); }
                to += offset.normalized * Rand.Range(1f, 3f);
            }

            Vector3 along = to - from;
            along.y = 0f;
            float distance = along.magnitude;
            float speed = Props.projectileSpeed > 0f ? Props.projectileSpeed : 18f;
            float flightSeconds = distance / speed;

            float angle = along.AngleFlat();
            string direction;
            if (angle >= 315f || angle < 45f) { direction = "up"; }
            else if (angle < 135f) { direction = "right"; }
            else if (angle < 225f) { direction = "down"; }
            else { direction = "left"; }

            FleckDef def = DefDatabase<FleckDef>.GetNamedSilentFail("DS_AcidSpit_" + direction);
            if (def != null)
            {
                FleckCreationData data = FleckMaker.GetDataStatic(from, map, def, Props.projectileScale);
                data.velocityAngle = angle;
                data.velocitySpeed = speed;
                data.solidTimeOverride = flightSeconds;
                map.flecks.CreateFleck(data);
            }

            pendingTarget = target;
            pendingHit = hit;
            impactPos = to;
            impactTick = Find.TickManager.TicksGame + Mathf.CeilToInt(flightSeconds * 60f);
        }

        // The blob arrives: splash, wound and corroded armour.
        private void Impact(Pawn spitter, Pawn target)
        {
            if (target == null || target.Dead || !target.Spawned || target.Map == null)
            {
                return;
            }

            Color acid = new Color(0.45f, 0.75f, 0.2f, 1f);
            if (!pendingHit)
            {
                // Missed: it splashes on the ground beside the target.
                FleckMaker.ThrowDustPuffThick(impactPos, target.Map, 1.2f, acid);
                LeavePuddle(impactPos.ToIntVec3(), target.Map);
                return;
            }
            FleckMaker.ThrowDustPuffThick(target.DrawPos, target.Map, 1.2f, acid);
            FleckMaker.ThrowMicroSparks(target.DrawPos, target.Map);
            LeavePuddle(target.Position, target.Map);

            target.TakeDamage(new DamageInfo(DamageDefOf.Burn, Props.damage, Props.armorPenetration, -1f, spitter));
            EatArmour(target);
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
                    Log.Warning("Dead Ship: Combat Extended is running but the puker spit projectile def '" + Props.ceProjectileDef + "' was not found, so the picture-only version is used.");
                }
                return false;
            }
            bool fired = CELauncher.TryLaunch(pawn, target, def, Props.ceSpreadDegrees);
            if (fired && !loggedCE)
            {
                loggedCE = true;
                Log.Message("Dead Ship: the puker fired a Combat Extended projectile (" + def.defName + ").");
            }
            return fired;
#else
            return false;
#endif
        }

        private void LeavePuddle(IntVec3 cell, Map map)
        {
            AcidEffects.LeavePuddle(cell, map);
        }

        private void EatArmour(Pawn target)
        {
            AcidEffects.EatArmour(target, Props.armorWear);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref nextSpitTick, "nextSpitTick", 0);
            Scribe_Values.Look(ref fireAtTick, "fireAtTick", 0);
            Scribe_References.Look(ref pendingTarget, "pendingTarget");
            Scribe_Values.Look(ref impactTick, "impactTick", 0);
            Scribe_Values.Look(ref pendingHit, "pendingHit", true);
            Scribe_Values.Look(ref impactPos, "impactPos");
        }
    }
}
