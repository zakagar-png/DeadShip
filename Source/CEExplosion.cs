#if CE
using CombatExtended;
using RimWorld;
using Verse;

namespace DeadShip
{
    // The exploder's blast through Combat Extended's own explosion code (GenExplosionCE), so CE's
    // damage falloff, armour handling and cover rules apply. Kept in its own class so the mod
    // still loads when Combat Extended is not installed.
    internal static class CEExplosion
    {
        public static void Explode(IntVec3 center, Map map, float radius, int damage, float armorPenetration, Thing instigator)
        {
            GenExplosionCE.DoExplosion(center, map, radius, DamageDefOf.Bomb, instigator, damage, armorPenetration);
        }
    }
}
#endif
