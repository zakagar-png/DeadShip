using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DeadShip
{
    // A signal from a crashed ship part. Only fires once the player has built a Marker research bench, and only while
    // no other wreck is waiting. It puts a wreck site on a nearby world tile; going there is how more Marker fragments are found.
    public class IncidentWorker_WreckSignal : IncidentWorker
    {
        public const string BenchDefName = "DS_MarkerBench";
        public const string WreckDefName = "DS_WreckSite";

        private static bool HasBench(Map map)
        {
            ThingDef bench = DefDatabase<ThingDef>.GetNamedSilentFail(BenchDefName);
            return map != null && bench != null && map.listerThings.ThingsOfDef(bench).Count > 0;
        }

        private static bool WreckWaiting()
        {
            WorldObjectDef def = DefDatabase<WorldObjectDef>.GetNamedSilentFail(WreckDefName);
            if (def == null)
            {
                return true;
            }
            List<WorldObject> all = Find.WorldObjects.AllWorldObjects;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].def == def)
                {
                    return true;
                }
            }
            return false;
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return HasBench(parms.target as Map) && !WreckWaiting();
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            WorldObjectDef def = DefDatabase<WorldObjectDef>.GetNamedSilentFail(WreckDefName);
            if (map == null || def == null || WreckWaiting())
            {
                return false;
            }

            PlanetTile tile;
            if (!TileFinder.TryFindNewSiteTile(out tile, minDist: MarkerResearch.Triangulated() ? 3 : 6, maxDist: MarkerResearch.Triangulated() ? 10 : 18))
            {
                Log.Message("Dead Ship: no world tile found for a wreck signal.");
                return false;
            }

            WorldObject site = WorldObjectMaker.MakeWorldObject(def);
            site.Tile = tile;
            Find.WorldObjects.Add(site);

            // The signal fades if nobody goes to look.
            TimeoutComp timeout = site.GetComponent<TimeoutComp>();
            if (timeout != null)
            {
                timeout.StartTimeout(Rand.RangeInclusive(14, 20) * 60000);
            }

            Find.LetterStack.ReceiveLetter("Signal detected",
                "The bench has picked up a faint resonance from nearby: part of the crashed ship, lying where it fell. "
                + "There may be Marker fragments in the wreckage, and whatever else came down with it.\n\n"
                + "Send a caravan to the new site to search it. The signal will fade in a couple of weeks.",
                LetterDefOf.NeutralEvent, new LookTargets(site));
            return true;
        }
    }
}
