using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // A raid that only ever comes from the necromorph faction, and only while a Marker is
    // installed on the map. Remove the Marker (uninstall it and carry it off) and the raids stop.
    public class IncidentWorker_NecromorphRaid : IncidentWorker_RaidEnemy
    {
        private const string FactionDefName = "DS_Necromorphs";

        // True only while a resonance altar with a Marker fragment loaded stands on the target map.
        // Take the fragment out (or take it off the map) and the raids stop.
        private static bool MarkerStanding(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return map != null && MarkerUtility.ActiveAltars(map).Count > 0;
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!MarkerStanding(parms))
            {
                return false;
            }

            return base.CanFireNowSub(parms);
        }

        // The dev menu's "Execute incident" skips CanFireNow, so check again at the moment of firing.
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!MarkerStanding(parms))
            {
                Log.Message("Dead Ship: necromorph raid cancelled, no loaded resonance altar on the map.");
                return false;
            }

            // Every fragment loaded draws bigger raids.
            Map map = parms.target as Map;
            if (map != null)
            {
                parms.points *= MarkerUtility.RaidFactor(MarkerUtility.TotalFragments(map));
            }

            HashSet<int> before = new HashSet<int>();
            if (map != null && parms.faction != null)
            {
                foreach (Pawn ex in map.mapPawns.AllPawnsSpawned)
                {
                    if (ex.Faction == parms.faction) before.Add(ex.thingIDNumber);
                }
            }
            bool fired = base.TryExecuteWorker(parms);
            if (fired && map != null)
            {
                NecroUnlock.ReplaceLocked(map, parms.faction);
                // Some raiders go for an air intake and come out of a vent (see MapComponent_Air).
                List<Pawn> raiders = new List<Pawn>();
                foreach (Pawn np in map.mapPawns.AllPawnsSpawned)
                {
                    if (np.Faction == parms.faction && !before.Contains(np.thingIDNumber)) raiders.Add(np);
                }
                MapComponent_Air.SendCrawlers(map, parms.faction, raiders, MarkerUtility.TotalFragments(map));
            }
            return fired;
        }

        protected override bool TryResolveRaidFaction(IncidentParms parms)
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(FactionDefName);
            if (def == null)
            {
                return false;
            }

            Faction faction = Find.FactionManager.FirstFactionOfDef(def);
            if (faction == null)
            {
                // Worlds created before the mod was enabled have no necromorph faction. Make one.
                faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
                Find.FactionManager.Add(faction);

                List<Faction> all = Find.FactionManager.AllFactionsListForReading;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != faction)
                    {
                        faction.TryMakeInitialRelationsWith(all[i]);
                    }
                }
            }

            parms.faction = faction;
            return true;
        }
    }
}
