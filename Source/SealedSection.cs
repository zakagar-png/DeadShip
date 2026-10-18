using RimWorld;
using Verse;
using Verse.AI.Group;

namespace DeadShip
{
    // Settings for the sealed ship section, set in Defs/ThingDefs/Marker.xml.
    public class SealedSectionExtension : DefModExtension
    {
        // Chance that breaking it open releases a necromorph, and which one.
        public float creatureChance = 0.35f;
        public string creatureKind = "DS_Lurker";
    }

    // The sealed ship section: a plain neutral building, like ancient ruins, which drafted
    // colonists can already melee with the normal attack order. Breaking it open drops a Marker fragment
    // (killedLeavings in the def) and sometimes releases something that was inside.
    public class SealedSection : Building
    {
        private bool released;

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            // Remember where we were: the base call removes the section from the map.
            Map map = Map;
            IntVec3 cell = Position;
            bool wasSpawned = Spawned;

            base.Destroy(mode);

            // Only breaking it open counts, not deconstruction or the dev tools.
            if (!wasSpawned || mode != DestroyMode.KillFinalize || released || map == null)
            {
                return;
            }
            released = true;

            SealedSectionExtension ext = def.GetModExtension<SealedSectionExtension>();
            if (ext == null)
            {
                return;
            }

            bool hit = Rand.Chance(ext.creatureChance);
            Log.Message("Dead Ship: sealed section broken open, creature roll: " + (hit ? "something was inside" : "empty"));
            if (hit)
            {
                ReleaseCreature(map, cell, ext);
            }
        }

        private static void ReleaseCreature(Map map, IntVec3 cell, SealedSectionExtension ext)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ext.creatureKind);
            Faction faction = MarkerUtility.NecromorphFaction();
            if (kind == null || faction == null)
            {
                return;
            }

            IntVec3 spot = CellFinder.StandableCellNear(cell, map, 4f);
            if (!spot.IsValid)
            {
                spot = cell;
            }

            Pawn creature = PawnGenerator.GeneratePawn(kind, faction);
            GenSpawn.Spawn(creature, spot, map);

            // It attacks at once; no creeping into ambush from inside a wall.
            CompNecroTactics tactics = creature.TryGetComp<CompNecroTactics>();
            if (tactics != null)
            {
                tactics.SkipAmbush();
            }
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, false, false, false, false, false, false, false), map, new Pawn[] { creature });

            Find.LetterStack.ReceiveLetter("Something was inside",
                "The hull split open and something that was sealed in there climbed out.",
                LetterDefOf.ThreatSmall, new TargetInfo(spot, map));
        }
    }
}
