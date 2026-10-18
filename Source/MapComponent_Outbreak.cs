using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace DeadShip
{
    // The Marker's signal rewrites dead tissue. A corpse lying within reach of an active Marker stirs, and a few hours later
    // rises as a necromorph and attacks. What it becomes depends on what it was, as in the games: grown humans become slashers
    // and the like, infants and dogs and other small animals become lurkers and children become the pack. Burn it, bury it (a grave or sarcophagus) or carry
    // it out of range and it stays down. Containing the Marker shrinks the range to a quarter, but does not stop it: the signal
    // still gets through. The necromorphs are held together by that signal, so when no Marker is active any more they fall apart.
    public class MapComponent_Outbreak : MapComponent
    {
        // Base distance in cells before the Marker's fragment growth is applied.
        private const float BaseRange = 30f;
        private const int CheckInterval = 250;
        private const int MinDelay = 3000;
        private const int MaxDelay = 8000;

        // Corpse thing id -> tick it rises.
        private Dictionary<int, int> due = new Dictionary<int, int>();

        // What a grown human becomes, and how likely each is.
        private static readonly string[] AdultKinds = { "DS_Slasher", "DS_Puker", "DS_Exploder", "DS_Leaper" };
        private static readonly float[] AdultWeights = { 10f, 3f, 2f, 3f };

        // Lore: infants and dogs become lurkers.
        private const string SmallKind = "DS_Lurker";
        private const string ChildKind = "DS_Pack";
        private const int InfantAge = 3;
        private const int ChildAge = 13;

        // Set once a Marker has been active here, so that maps that never had one (wrecks) are left alone.
        private bool hadSource;

        public MapComponent_Outbreak(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hadSource, "ds_hadSource", false);
            Scribe_Collections.Look(ref due, "ds_outbreakDue", LookMode.Value, LookMode.Value);
            if (due == null)
            {
                due = new Dictionary<int, int>();
            }
        }

        public override void MapComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % CheckInterval != 0)
            {
                return;
            }

            List<Thing> sources = MarkerUtility.ActiveAltars(map);
            if (sources.Count == 0)
            {
                due.Clear();
                if (hadSource)
                {
                    Dissolve();
                }
                return;
            }
            hadSource = true;

            List<Thing> corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            HashSet<int> inRange = new HashSet<int>();
            List<Corpse> rising = new List<Corpse>();

            for (int i = 0; i < corpses.Count; i++)
            {
                Corpse corpse = corpses[i] as Corpse;
                if (corpse == null || !corpse.Spawned || !CanRise(corpse))
                {
                    continue;
                }
                if (!NearMarker(corpse.Position, sources))
                {
                    continue;
                }

                inRange.Add(corpse.thingIDNumber);
                int when;
                if (!due.TryGetValue(corpse.thingIDNumber, out when))
                {
                    due[corpse.thingIDNumber] = now + (int)(Rand.RangeInclusive(MinDelay, MaxDelay) * MarkerResearch.OutbreakFuseFactor());
                    Messages.Message(corpse.InnerPawn.LabelShort + "'s body is twitching. Burn it, bury it or carry it away before it rises.",
                        new LookTargets(corpse), MessageTypeDefOf.NegativeEvent, false);
                }
                else if (now >= when)
                {
                    rising.Add(corpse);
                }
            }

            // Anything no longer in range (moved, burned or buried) loses its timer.
            List<int> stale = new List<int>();
            foreach (KeyValuePair<int, int> pair in due)
            {
                if (!inRange.Contains(pair.Key))
                {
                    stale.Add(pair.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                due.Remove(stale[i]);
            }

            for (int i = 0; i < rising.Count; i++)
            {
                due.Remove(rising[i].thingIDNumber);
                Rise(rising[i]);
            }
        }

        private bool NearMarker(IntVec3 pos, List<Thing> sources)
        {
            for (int i = 0; i < sources.Count; i++)
            {
                float reach = BaseRange * MarkerUtility.RangeFactor(MarkerUtility.FragmentsLoaded(sources[i])) * MarkerResearch.DreadMultiplier();
                if (MarkerUtility.IsContained(sources[i]))
                {
                    reach = BaseRange * MarkerUtility.ContainedReach * MarkerResearch.DreadMultiplier();
                }
                if ((pos - sources[i].Position).LengthHorizontal <= reach)
                {
                    return true;
                }
            }
            return false;
        }

        // Which dead can rise: grown and young humans, and small animals (a third of them, so wildlife kills do not flood the map).
        private static bool CanRise(Corpse corpse)
        {
            Pawn pawn = corpse.InnerPawn;
            if (pawn == null || pawn.kindDef == null || pawn.kindDef.defName.StartsWith("DS_"))
            {
                return false;
            }
            bool human = pawn.RaceProps.Humanlike;
            bool smallAnimal = pawn.RaceProps.Animal && pawn.RaceProps.FleshType == FleshTypeDefOf.Normal
                && pawn.RaceProps.baseBodySize <= 1.5f && corpse.thingIDNumber % 3 == 0;
            if (!human && !smallAnimal)
            {
                return false;
            }
            CompRottable rot = corpse.GetComp<CompRottable>();
            return rot == null || rot.Stage != RotStage.Dessicated;
        }

        private static string ChooseKind(Pawn pawn)
        {
            string k = ChooseKindRaw(pawn);
            return NecroUnlock.Allowed(k) ? k : "DS_Slasher";
        }

        private static string ChooseKindRaw(Pawn pawn)
        {
            if (!pawn.RaceProps.Humanlike || pawn.ageTracker.AgeBiologicalYears < InfantAge)
            {
                return SmallKind;
            }
            if (pawn.ageTracker.AgeBiologicalYears < ChildAge)
            {
                return ChildKind;
            }
            float total = 0f;
            for (int i = 0; i < AdultWeights.Length; i++)
            {
                total += AdultWeights[i];
            }
            float roll = Rand.Value * total;
            for (int i = 0; i < AdultWeights.Length; i++)
            {
                roll -= AdultWeights[i];
                if (roll <= 0f)
                {
                    return AdultKinds[i];
                }
            }
            return AdultKinds[0];
        }

        // No active Marker: the signal that holds the necromorphs together is gone, and they collapse.
        private void Dissolve()
        {
            Faction faction = MarkerUtility.NecromorphFaction();
            if (faction == null)
            {
                return;
            }
            List<Pawn> pawns = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            int count = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (pawns[i].Faction == faction && !pawns[i].Dead)
                {
                    FleckMaker.ThrowDustPuff(pawns[i].Position, map, 1.5f);
                    pawns[i].Destroy();
                    count++;
                }
            }
            if (count > 0)
            {
                Messages.Message("The Marker's signal is gone. The necromorphs collapse into sludge.", MessageTypeDefOf.PositiveEvent, false);
            }
        }

        private void Rise(Corpse corpse)
        {
            Faction faction = MarkerUtility.NecromorphFaction();
            if (faction == null)
            {
                return;
            }

            string chosen = ChooseKind(corpse.InnerPawn);
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(chosen);
            if (kind == null)
            {
                return;
            }

            string name = corpse.InnerPawn.LabelShort;
            IntVec3 pos = corpse.Position;
            Map m = corpse.Map;

            Pawn necro = PawnGenerator.GeneratePawn(kind, faction);
            IntVec3 spot = CellFinder.StandableCellNear(pos, m, 2f);
            if (!spot.IsValid)
            {
                spot = pos;
            }
            corpse.Destroy();
            GenSpawn.Spawn(necro, spot, m);
            FleckMaker.ThrowDustPuff(spot, m, 2f);

            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, false, false, false, false, false, false, false), m,
                new List<Pawn> { necro });

            Find.LetterStack.ReceiveLetter("The dead rise",
                name + " did not stay dead. The Marker's signal has remade the body into something that is coming for the colony.",
                LetterDefOf.ThreatSmall, necro);
        }
    }
}
