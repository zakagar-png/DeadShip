#if CE
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // The puker's spit as a Combat Extended projectile. CE does the flight, collision and wound;
    // this adds what makes it acid: it splashes, eats the armour of whoever it hits, and leaves a puddle.
    public class AcidProjectileCE : ProjectileCE
    {
        public override void Impact(Thing hitThing)
        {
            // Read these first: the projectile is destroyed by the base call.
            Map map = Map;
            IntVec3 cell = Position;
            Vector3 where = ExactPosition;
            AcidProjectileExtension ext = def.GetModExtension<AcidProjectileExtension>();

            base.Impact(hitThing);

            if (map == null)
            {
                return;
            }

            FleckMaker.ThrowDustPuffThick(where, map, 1.2f, new Color(0.45f, 0.75f, 0.2f, 1f));
            Pawn pawn = hitThing as Pawn;
            if (pawn != null)
            {
                AcidEffects.EatArmour(pawn, ext != null ? ext.armorWear : 12f);
            }
            AcidEffects.LeavePuddle(cell, map);
        }
    }
}
#endif
