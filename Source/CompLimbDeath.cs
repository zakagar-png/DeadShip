using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeadShip
{
    public class CompProperties_LimbDeath : CompProperties
    {
        // How many of the tracked limbs must be gone before the creature dies.
        public int limbsToDie = 3;

        // defNames of the body parts that count as limbs (arms, legs, head).
        public List<string> limbDefs;

        // defNames of body parts that kill the creature outright if any one of them is destroyed
        // (for example the puker's gut sac), whatever the limb count is.
        public List<string> killIfLostDefs;

        public CompProperties_LimbDeath()
        {
            compClass = typeof(CompLimbDeath);
        }
    }

    // Kills the creature the moment it has lost enough limbs, however healthy its torso is.
    public class CompLimbDeath : ThingComp
    {
        private string lastVariant = "";

        public CompProperties_LimbDeath Props
        {
            get { return (CompProperties_LimbDeath)props; }
        }

        // Necromorph Anatomy and Weak Point Mapping research: a hit from a colonist, with any weapon, goes for one of the
        // creature's limbs (or its other weak points) instead of wherever the normal hit roll lands, with the chance the research gives.
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || dinfo.HitPart != null || dinfo.Def == null || !dinfo.Def.harmsHealth || dinfo.Def.isExplosive)
            {
                return;
            }
            Pawn attacker = dinfo.Instigator as Pawn;
            if (attacker == null || attacker.Faction == null || !attacker.Faction.IsPlayer)
            {
                return;
            }
            float bonus = MarkerResearch.LimbChanceBonus();
            if (bonus <= 0f || !Rand.Chance(bonus) || Props.limbDefs == null)
            {
                return;
            }

            List<BodyPartRecord> targets = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (Props.limbDefs.Contains(part.def.defName))
                {
                    targets.Add(part);
                }
            }
            if (targets.Count > 0)
            {
                dinfo.SetHitPart(targets.RandomElement());
            }
        }

        public override void CompTick()
        {
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || pawn.health == null)
            {
                return;
            }

            // Checking twice a second is plenty.
            if (!pawn.IsHashIntervalTick(30))
            {
                return;
            }

            // Reanimated flesh feels nothing: make sure it carries the zero-pain effect.
            HediffDef noPain = DefDatabase<HediffDef>.GetNamedSilentFail("DS_NoPain");
            if (noPain != null && !pawn.health.hediffSet.HasHediff(noPain))
            {
                pawn.health.AddHediff(noPain);
            }

            // Legless, it drags itself along on its arms instead of going down: the arms are moving limbs too, so it still
            // moves, and this just labels it as crawling.
            TryStartCrawling(pawn);

            int missing = 0;
            bool vitalLost = false;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff_MissingPart gone = hediffs[i] as Hediff_MissingPart;
                if (gone == null || gone.Part == null)
                {
                    continue;
                }
                if (Props.limbDefs != null && Props.limbDefs.Contains(gone.Part.def.defName))
                {
                    missing++;
                }
                if (Props.killIfLostDefs != null && Props.killIfLostDefs.Contains(gone.Part.def.defName))
                {
                    vitalLost = true;
                }
            }

            // Redraw the sprite when the set of missing head/arms changes.
            string variant = SlasherParts.VariantKey(pawn) + "#" + missing;
            if (variant != lastVariant)
            {
                lastVariant = variant;
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }

            if (vitalLost || missing >= Props.limbsToDie)
            {
                pawn.Kill(null);
                // Redraw once more so the dead body shows its final picture (for example the destroyed gut sac).
                if (pawn.Drawer != null && pawn.Drawer.renderer != null)
                {
                    pawn.Drawer.renderer.SetAllGraphicsDirty();
                }
            }
        }

        private static void TryStartCrawling(Pawn pawn)
        {
            HediffDef crawling = DefDatabase<HediffDef>.GetNamedSilentFail("DS_Crawling");
            if (crawling == null || pawn.health.hediffSet.HasHediff(crawling))
            {
                return;
            }

            // The legs are the moving limbs whose part def is named leg.
            int legCount = 0;
            List<BodyPartRecord> moving = pawn.RaceProps.body.GetPartsWithTag(BodyPartTagDefOf.MovingLimbCore);
            for (int i = 0; i < moving.Count; i++)
            {
                if (moving[i].def.defName.IndexOf("Leg") < 0)
                {
                    continue;
                }
                legCount++;
                if (!pawn.health.hediffSet.PartIsMissing(moving[i]))
                {
                    return;
                }
            }
            if (legCount == 0)
            {
                return;
            }
            pawn.health.AddHediff(crawling);
        }
    }
}
