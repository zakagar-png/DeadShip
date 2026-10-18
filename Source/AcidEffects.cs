using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeadShip
{
    // Settings read by the Combat Extended acid projectile (set in the projectile's def, see Patches/CE_Barb.xml).
    public class AcidProjectileExtension : DefModExtension
    {
        public float armorWear = 12f;
    }

    // The aftermath of acid landing, shared by the picture-only spit and the Combat Extended projectile.
    public static class AcidEffects
    {
        // Wherever the acid lands it leaves a harmless puddle that has to be cleaned, like vomit.
        public static void LeavePuddle(IntVec3 cell, Map map)
        {
            ThingDef puddle = DefDatabase<ThingDef>.GetNamedSilentFail("DS_Filth_Acid");
            if (puddle != null && map != null && cell.InBounds(map))
            {
                FilthMaker.TryMakeFilth(cell, map, puddle, 1);
            }
        }

        // The acid corrodes one worn piece of armour.
        public static void EatArmour(Pawn target, float wear)
        {
            if (target == null || target.apparel == null || wear <= 0f)
            {
                return;
            }

            List<Apparel> armoured = new List<Apparel>();
            List<Apparel> worn = target.apparel.WornApparel;
            for (int i = 0; i < worn.Count; i++)
            {
                Apparel piece = worn[i];
                float protection = piece.GetStatValue(StatDefOf.ArmorRating_Sharp, true)
                                   + piece.GetStatValue(StatDefOf.ArmorRating_Blunt, true)
                                   + piece.GetStatValue(StatDefOf.ArmorRating_Heat, true);
                if (protection > 0.05f)
                {
                    armoured.Add(piece);
                }
            }

            if (armoured.Count == 0)
            {
                return;
            }

            Apparel chosen = armoured[Rand.Range(0, armoured.Count)];
            chosen.TakeDamage(new DamageInfo(DamageDefOf.Deterioration, wear));
        }
    }
}
