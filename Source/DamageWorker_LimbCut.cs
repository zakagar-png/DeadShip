using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeadShip
{
    // Set on a damage def (Defs/DamageDefs/DeadShipDamage.xml): how often a hit goes for a limb instead of
    // wherever the normal hit roll lands.
    public class LimbBiasExtension : DefModExtension
    {
        public float limbChance = 0.6f;
    }

    // A cutting damage that aims for arms and legs: "strategic dismemberment". Used by the Dead Space weapons.
    public class DamageWorker_LimbCut : DamageWorker_Cut
    {
        protected override BodyPartRecord ChooseHitPart(DamageInfo dinfo, Pawn pawn)
        {
            LimbBiasExtension ext = dinfo.Def.GetModExtension<LimbBiasExtension>();
            if (ext != null && Rand.Chance(ext.limbChance))
            {
                BodyPartRecord limb = RandomLimb(pawn);
                if (limb != null)
                {
                    return limb;
                }
            }
            return base.ChooseHitPart(dinfo, pawn);
        }

        private static BodyPartRecord RandomLimb(Pawn pawn)
        {
            List<BodyPartRecord> limbs = new List<BodyPartRecord>();
            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.depth != BodyPartDepth.Outside) { continue; }
                List<BodyPartTagDef> tags = part.def.tags;
                if (tags.Contains(BodyPartTagDefOf.MovingLimbCore) || tags.Contains(BodyPartTagDefOf.ManipulationLimbCore))
                {
                    limbs.Add(part);
                }
            }
            return limbs.Count == 0 ? null : limbs.RandomElement();
        }
    }
}
