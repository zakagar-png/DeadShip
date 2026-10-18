using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // Where the Marker's effects come from: a resonance altar with a fragment loaded into it.
    // A fragment lying on the ground, or an empty altar, does nothing.
    public static class MarkerUtility
    {
        public const string AltarDefName = "DS_ResonanceAltar";
        public const string FullMarkerDefName = "DS_FullMarker";

        public const string SiteDefName = "DS_MarkerSite";

        // The finished Marker counts as this many fragments.
        public const int FullMarkerFragments = 16;

        // The Marker grows with every fragment loaded: the more there are, the more power it gives, the further
        // its dread reaches, the heavier it weighs on the colonists and the bigger the raids it draws.
        // One fragment is the baseline for each.
        public const float RangeGrowthPerFragment = 0.10f;
        public const float RaidGrowthPerFragment = 0.05f;

        // How many fragments a Marker structure has loaded (the finished Marker counts as all of them).
        public static int FragmentsLoaded(Thing marker)
        {
            if (marker == null)
            {
                return 0;
            }
            if (marker.def.defName == FullMarkerDefName)
            {
                return FullMarkerFragments;
            }
            ThingWithComps thing = marker as ThingWithComps;
            CompRefuelable fuel = thing == null ? null : thing.GetComp<CompRefuelable>();
            return fuel == null ? 0 : Mathf.FloorToInt(fuel.Fuel + 0.001f);
        }

        // Fragments loaded across everything on the map.
        public static int TotalFragments(Map map)
        {
            int total = 0;
            List<Thing> sources = ActiveAltars(map);
            for (int i = 0; i < sources.Count; i++)
            {
                total += FragmentsLoaded(sources[i]);
            }
            return total;
        }

        public const string AlloyDefName = "DS_ContainmentAlloy";
        public const string SuppressedHediffName = "DS_Suppressed";

        // A Marker standing in a closed room whose every wall and door is made of containment alloy has its dread muffled:
        // it counts as this fraction of its fragments when working out how far and how hard the dread hits.
        public const float ContainedFearFactor = 0.35f;
        // How far a contained Marker's nearby fear reaches, as a fraction of normal.
        public const float ContainedReach = 0.25f;

        // Remembered for a second so many colonists asking at once do not each walk the walls.
        private static readonly Dictionary<int, int> containedAt = new Dictionary<int, int>();
        private static readonly Dictionary<int, bool> containedValue = new Dictionary<int, bool>();

        // True when the structure is in an enclosed room walled entirely in containment alloy.
        public static bool IsContained(Thing marker)
        {
            if (marker == null || !marker.Spawned)
            {
                return false;
            }

            int now = Find.TickManager.TicksGame;
            int tick;
            bool cached;
            if (containedAt.TryGetValue(marker.thingIDNumber, out tick) && now - tick < 60
                && containedValue.TryGetValue(marker.thingIDNumber, out cached))
            {
                return cached;
            }

            bool result = ComputeContained(marker);
            containedAt[marker.thingIDNumber] = now;
            containedValue[marker.thingIDNumber] = result;
            return result;
        }

        private static bool ComputeContained(Thing marker)
        {
            return Diagnose(marker) == null;
        }

        // Null when the Marker is contained; otherwise the reason it is not (shown on its selection panel).
        public static string Diagnose(Thing marker)
        {
            ThingDef alloy = DefDatabase<ThingDef>.GetNamedSilentFail(AlloyDefName);
            if (alloy == null)
            {
                return "alloy def missing";
            }
            Room room = marker.GetRoom();
            if (room == null)
            {
                return "not in a room";
            }
            if (room.TouchesMapEdge)
            {
                return "room is open to the map edge";
            }

            Map map = marker.Map;
            int walls = 0;
            foreach (IntVec3 cell in room.BorderCells)
            {
                Building edifice = cell.GetEdifice(map);
                if (edifice == null)
                {
                    continue;
                }
                walls++;
                if (edifice.Stuff != alloy)
                {
                    return edifice.def.label + " at " + cell.x + "," + cell.z + " is "
                        + (edifice.Stuff == null ? "not made of a material" : edifice.Stuff.label);
                }
            }
            return walls > 0 ? null : "no walls found";
        }

        // Fragments as the colonists feel them: contained Markers count for less. Fractions are rounded down.
        public static int FearFragments(Map map)
        {
            float total = 0f;
            List<Thing> sources = ActiveAltars(map);
            for (int i = 0; i < sources.Count; i++)
            {
                float f = FragmentsLoaded(sources[i]);
                total += IsContained(sources[i]) ? f * ContainedFearFactor : f;
            }
            return Mathf.FloorToInt(total * MarkerResearch.DreadMultiplier() + 0.001f);
        }

        // Distances (in cells, before range growth) of the four dread stages around a Marker: worst to mildest.
        public static readonly float[] DreadStageDistances = { 6f, 12f, 20f, 30f };

        // Which dread stage a pawn is in, from 0 (worst) to 3 (mildest), or -1 when the Marker's dread does not reach them.
        // Uses the same reach rules as the "near the Marker" mood: fragments, containment and the research chains.
        public static int DreadStage(Pawn p)
        {
            return DreadStage(p, true);
        }

        // With damped false the research that dampens the dread is left out, so the same place can be dreaded less but attuned just as much.
        public static int DreadStage(Pawn p, bool damped)
        {
            if (p == null || !p.Spawned || p.Map == null)
            {
                return -1;
            }
            List<Thing> markers = ActiveAltars(p.Map);
            float nearest = float.MaxValue;
            for (int i = 0; i < markers.Count; i++)
            {
                float reach = IsContained(markers[i]) ? ContainedReach : RangeFactor(FragmentsLoaded(markers[i]));
                if (damped)
                {
                    reach *= MarkerResearch.DreadMultiplier();
                }
                float distance = (p.Position - markers[i].Position).LengthHorizontal / reach;
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }
            for (int stage = 0; stage < DreadStageDistances.Length; stage++)
            {
                if (nearest <= DreadStageDistances[stage])
                {
                    return stage;
                }
            }
            return -1;
        }

        // How strongly the Marker's signal acts on this colonist, from the base game's psychic sensitivity trait:
        // psychically deaf = not at all (0), psychically hypersensitive = double (2), anyone else = 1.
        public static float Susceptibility(Pawn p)
        {
            if (p == null || p.story == null || p.story.traits == null)
            {
                return 1f;
            }
            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity");
            if (def == null)
            {
                return 1f;
            }
            int degree = p.story.traits.DegreeOfTrait(def);
            if (degree <= -2) return 0f;
            if (degree >= 2) return 2f;
            return 1f;
        }

        // Memory suppressants dull the dread.
        public static bool IsSuppressed(Pawn p)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(SuppressedHediffName);
            return def != null && p != null && p.health != null && p.health.hediffSet.HasHediff(def);
        }

        // How far the Marker's dread reaches, as a multiple of the one-fragment distances.
        public static float RangeFactor(int fragments)
        {
            return 1f + RangeGrowthPerFragment * Mathf.Max(0, fragments - 1);
        }

        // How much bigger raids are, as a multiple.
        public static float RaidFactor(int fragments)
        {
            return 1f + RaidGrowthPerFragment * MarkerResearch.DreadMultiplier() * Mathf.Max(0, fragments - 1);
        }


        // The necromorph faction, made if the world does not have one yet (worlds that predate the mod).
        public static Faction NecromorphFaction()
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail("DS_Necromorphs");
            if (def == null)
            {
                return null;
            }

            Faction faction = Find.FactionManager.FirstFactionOfDef(def);
            if (faction == null)
            {
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
            return faction;
        }

        // Everything on the map that gives off the Marker's effects: loaded altars and full Markers.
        public static List<Thing> ActiveAltars(Map map)
        {
            List<Thing> result = new List<Thing>();
            if (map == null)
            {
                return result;
            }

            ThingDef altarDef = DefDatabase<ThingDef>.GetNamedSilentFail(AltarDefName);
            if (altarDef == null)
            {
                return result;
            }

            List<Thing> altars = map.listerThings.ThingsOfDef(altarDef);
            for (int i = 0; i < altars.Count; i++)
            {
                ThingWithComps altar = altars[i] as ThingWithComps;
                if (altar == null)
                {
                    continue;
                }
                CompRefuelable fuel = altar.GetComp<CompRefuelable>();
                if (fuel != null && fuel.HasFuel)
                {
                    result.Add(altar);
                }
            }

            // The scaffold counts too, from the first fragment loaded into it.
            ThingDef siteDef = DefDatabase<ThingDef>.GetNamedSilentFail(SiteDefName);
            if (siteDef != null)
            {
                List<Thing> sites = map.listerThings.ThingsOfDef(siteDef);
                for (int i = 0; i < sites.Count; i++)
                {
                    ThingWithComps site = sites[i] as ThingWithComps;
                    CompRefuelable fuel = site == null ? null : site.GetComp<CompRefuelable>();
                    if (fuel != null && fuel.HasFuel)
                    {
                        result.Add(site);
                    }
                }
            }

            // The full Marker has no off switch and needs no loading.
            ThingDef fullDef = DefDatabase<ThingDef>.GetNamedSilentFail(FullMarkerDefName);
            if (fullDef != null)
            {
                result.AddRange(map.listerThings.ThingsOfDef(fullDef));
            }
            return result;
        }
    }
}
