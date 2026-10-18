using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace DeadShip
{
    // The necromorph roster unlocks with the days since the Marker's first fragment was loaded.
    public class GameComponent_NecroClock : GameComponent
    {
        private int startTick = -1;

        public GameComponent_NecroClock(Game game)
        {
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref startTick, "ds_markerStart", -1);
        }

        public override void GameComponentTick()
        {
            if (startTick >= 0 || Find.TickManager.TicksGame % 250 != 0)
            {
                return;
            }
            List<Map> maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                if (MarkerUtility.TotalFragments(maps[i]) > 0)
                {
                    startTick = Find.TickManager.TicksGame;
                    return;
                }
            }
        }

        // Debug: move the clock on (starts it now if the Marker hasn't started it yet).
        public void AddDays(float days)
        {
            if (startTick < 0) startTick = Find.TickManager.TicksGame;
            startTick -= (int)(days * 60000f);
        }

        public void ResetClock()
        {
            startTick = -1;
        }

        public float Days
        {
            get { return startTick < 0 ? 0f : (Find.TickManager.TicksGame - startTick) / 60000f; }
        }
    }

    public static class NecroClockDebug
    {
        private static GameComponent_NecroClock Clock()
        {
            return Current.Game != null ? Current.Game.GetComponent<GameComponent_NecroClock>() : null;
        }

        private static void Add(float d)
        {
            GameComponent_NecroClock c = Clock();
            if (c == null) return;
            c.AddDays(d);
            Messages.Message("Necromorph clock: day " + c.Days.ToString("F1"), MessageTypeDefOf.NeutralEvent, false);
        }

        [DebugAction("Dead Ship", "Clock +5 days")]
        public static void Plus5() { Add(5f); }

        [DebugAction("Dead Ship", "Clock +10 days")]
        public static void Plus10() { Add(10f); }

        [DebugAction("Dead Ship", "Clock +30 days")]
        public static void Plus30() { Add(30f); }

        [DebugAction("Dead Ship", "Clock reset")]
        public static void Reset()
        {
            GameComponent_NecroClock c = Clock();
            if (c == null) return;
            c.ResetClock();
            Messages.Message("Necromorph clock reset.", MessageTypeDefOf.NeutralEvent, false);
        }
    }

    public static class NecroUnlock
    {
        public static float Days()
        {
            GameComponent_NecroClock c = Current.Game != null ? Current.Game.GetComponent<GameComponent_NecroClock>() : null;
            return c != null ? c.Days : 0f;
        }

        public static int UnlockDay(string kind)
        {
            switch (kind)
            {
                case "DS_Pack":
                case "DS_Puker":
                case "DS_Lurker":
                    return 10;
                case "DS_Exploder":
                case "DS_Leaper":
                    return 15;
                case "DS_Brute":
                    return 30;
                default:
                    return 0;   // slasher, pregnant, swarmer
            }
        }

        public static bool Allowed(string kind)
        {
            return Days() >= UnlockDay(kind);
        }

        // Weighted pick among the kinds that are unlocked; falls back to the slasher.
        public static string Pick(string[] kinds, float[] weights)
        {
            float total = 0f;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (Allowed(kinds[i])) total += weights[i];
            }
            float roll = Rand.Value * total;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (!Allowed(kinds[i])) continue;
                roll -= weights[i];
                if (roll <= 0f) return kinds[i];
            }
            return "DS_Slasher";
        }

        // Raid pawns come from the faction's normal group maker. Swap any that are not unlocked yet.
        public static void ReplaceLocked(Map map, Faction faction)
        {
            string[] kinds = { "DS_Slasher", "DS_Pack", "DS_Lurker", "DS_Exploder", "DS_Leaper", "DS_Puker", "DS_Pregnant", "DS_Swarmer" };
            float[] weights = { 10f, 8f, 6f, 5f, 5f, 4f, 3f, 2f };
            List<Pawn> all = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < all.Count; i++)
            {
                Pawn p = all[i];
                if (p.Faction != faction || p.Dead || Allowed(p.kindDef.defName)) continue;
                if (!p.kindDef.defName.StartsWith("DS_")) continue;
                PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Pick(kinds, weights));
                if (kind == null) continue;
                Pawn np = PawnGenerator.GeneratePawn(kind, faction);
                IntVec3 pos = p.Position;
                Lord lord = p.GetLord();
                GenSpawn.Spawn(np, pos, map);
                if (lord != null) lord.AddPawn(np);
                p.Destroy();
            }
        }
    }
}
