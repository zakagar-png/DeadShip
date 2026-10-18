using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace DeadShip
{
    // Builds the wreck: a broken ship section of 3 x 2 rooms in open ground. Walls are damaged, doors link the rooms, and the
    // necromorphs inside are asleep until something disturbs them. Marker fragments lie in the rooms.
    public class GenStep_Wreck : GenStep
    {
        // Overall size of the hull in cells (3 x 2 rooms).
        private const int RoomW = 13;
        private const int RoomH = 11;
        private const int Cols = 3;
        private const int Rows = 2;

        public override int SeedPart
        {
            get { return 774218231; }
        }

        public override void Generate(Map map, GenStepParams parms)
        {
            int width = RoomW * Cols + 1;
            int height = RoomH * Rows + 1;
            IntVec3 center = map.Center;
            int x0 = center.x - width / 2;
            int z0 = center.z - height / 2;
            CellRect hull = new CellRect(x0, z0, width, height);
            MapGenerator.UsedRects.Add(hull.ExpandedBy(3));

            // Clear whatever the base map put there (trees, rocks, plants) and lay deck plating.
            foreach (IntVec3 c in hull.ExpandedBy(1).Cells)
            {
                if (!c.InBounds(map)) { continue; }
                List<Thing> things = new List<Thing>(c.GetThingList(map));
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i].def.destroyable && things[i].def.category != ThingCategory.Pawn)
                    {
                        things[i].Destroy(DestroyMode.Vanish);
                    }
                }
            }
            foreach (IntVec3 c in hull.Cells)
            {
                map.terrainGrid.SetTerrain(c, TerrainDefOf.MetalTile);
            }

            ThingDef wallDef = ThingDefOf.Wall;
            ThingDef doorDef = ThingDefOf.Door;
            ThingDef steel = ThingDefOf.Steel;

            // Which wall cells are doors, and which are broken open.
            HashSet<IntVec3> doors = new HashSet<IntVec3>();
            HashSet<IntVec3> breaks = new HashSet<IntVec3>();

            // Doors between neighbouring rooms.
            for (int cx = 0; cx < Cols; cx++)
            {
                for (int cz = 0; cz < Rows; cz++)
                {
                    int rx = x0 + cx * RoomW;
                    int rz = z0 + cz * RoomH;
                    if (cx < Cols - 1)
                    {
                        doors.Add(new IntVec3(rx + RoomW, 0, rz + Rand.RangeInclusive(2, RoomH - 2)));
                    }
                    if (cz < Rows - 1)
                    {
                        doors.Add(new IntVec3(rx + Rand.RangeInclusive(2, RoomW - 2), 0, rz + RoomH));
                    }
                }
            }

            // One door in the outer hull, so there is a way in. Everything else is sealed.
            {
                int side = Rand.RangeInclusive(0, 3);
                if (side == 0) { doors.Add(new IntVec3(x0, 0, z0 + Rand.RangeInclusive(2, height - 3))); }
                else if (side == 1) { doors.Add(new IntVec3(x0 + width - 1, 0, z0 + Rand.RangeInclusive(2, height - 3))); }
                else if (side == 2) { doors.Add(new IntVec3(x0 + Rand.RangeInclusive(2, width - 3), 0, z0)); }
                else { doors.Add(new IntVec3(x0 + Rand.RangeInclusive(2, width - 3), 0, z0 + height - 1)); }
            }

            // Walls on every room boundary.
            for (int x = x0; x < x0 + width; x++)
            {
                for (int z = z0; z < z0 + height; z++)
                {
                    bool boundary = ((x - x0) % RoomW == 0) || ((z - z0) % RoomH == 0);
                    if (!boundary) { continue; }
                    IntVec3 c = new IntVec3(x, 0, z);
                    if (breaks.Contains(c)) { continue; }
                    if (doors.Contains(c))
                    {
                        Thing door = ThingMaker.MakeThing(doorDef, steel);
                        door.SetFaction(null);
                        GenSpawn.Spawn(door, c, map);
                        continue;
                    }
                    Thing wall = ThingMaker.MakeThing(wallDef, steel);
                    GenSpawn.Spawn(wall, c, map);
                }
            }

            // Roof every room so the hull is sealed and dark inside.
            for (int cx = 0; cx < Cols; cx++)
            {
                for (int cz = 0; cz < Rows; cz++)
                {
                    if (true)
                    {
                        CellRect room = RoomRect(x0, z0, cx, cz);
                        foreach (IntVec3 c in room.Cells)
                        {
                            map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                        }
                    }
                }
            }

            // Wreckage on the ground around the hull.
            for (int i = 0; i < 14; i++)
            {
                IntVec3 c = hull.ExpandedBy(6).RandomCell;
                if (c.InBounds(map) && !hull.Contains(c) && c.Standable(map))
                {
                    GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel), c, map);
                }
            }

            PopulateRooms(map, x0, z0);
        }

        private static CellRect RoomRect(int x0, int z0, int cx, int cz)
        {
            return new CellRect(x0 + cx * RoomW + 1, z0 + cz * RoomH + 1, RoomW - 1, RoomH - 1);
        }


        private void PopulateRooms(Map map, int x0, int z0)
        {
            Faction faction = MarkerUtility.NecromorphFaction();
            ThingDef fragment = DefDatabase<ThingDef>.GetNamedSilentFail("DS_MarkerFragment");

            // The room with the most fragments is also the most heavily guarded.
            int coreCol = Rand.Range(0, Cols);
            int coreRow = Rand.Range(0, Rows);

            string[] kindNames = new string[] { "DS_Slasher", "DS_Slasher", "DS_Slasher", "DS_Puker", "DS_Lurker", "DS_Exploder", "DS_Leaper", "DS_Leaper" };

            for (int cx = 0; cx < Cols; cx++)
            {
                for (int cz = 0; cz < Rows; cz++)
                {
                    CellRect room = RoomRect(x0, z0, cx, cz);
                    bool core = cx == coreCol && cz == coreRow;

                    // Necromorphs, asleep.
                    if (faction != null)
                    {
                        List<string> picks = new List<string>();
                        if (core)
                        {
                            picks.Add("DS_Pregnant");
                        }
                        int count = core ? Rand.RangeInclusive(2, 3) : Rand.RangeInclusive(0, 3);
                        for (int n = 0; n < count; n++)
                        {
                            { string pk = kindNames[Rand.Range(0, kindNames.Length)]; picks.Add(NecroUnlock.Allowed(pk) ? pk : "DS_Slasher"); }
                        }

                        List<Pawn> pawns = new List<Pawn>();
                        for (int n = 0; n < picks.Count; n++)
                        {
                            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(picks[n]);
                            if (kind == null) { continue; }
                            IntVec3 spot;
                            if (!CellFinder.TryFindRandomCellInsideWith(room, delegate(IntVec3 c) { return c.Standable(map) && c.GetFirstPawn(map) == null; }, out spot))
                            {
                                continue;
                            }
                            Pawn p = PawnGenerator.GeneratePawn(kind, faction);
                            GenSpawn.Spawn(p, spot, map, Rot4.Random);
                            pawns.Add(p);
                        }
                        if (pawns.Count > 0)
                        {
                            LordMaker.MakeNewLord(faction, new LordJob_SleepThenAssaultColony(faction, true), map, pawns);
                        }
                    }

                    // Fragments: several in the core room, one or none elsewhere.
                    if (fragment != null)
                    {
                        int frags = core ? Rand.RangeInclusive(3, 4) : (Rand.Chance(0.5f) ? 1 : 0);
                        for (int f = 0; f < frags; f++)
                        {
                            IntVec3 spot;
                            if (CellFinder.TryFindRandomCellInsideWith(room, delegate(IntVec3 c) { return c.Standable(map); }, out spot))
                            {
                                GenSpawn.Spawn(ThingMaker.MakeThing(fragment), spot, map);
                            }
                        }
                    }

                    // Salvage.
                    if (Rand.Chance(0.6f))
                    {
                        IntVec3 spot;
                        if (CellFinder.TryFindRandomCellInsideWith(room, delegate(IntVec3 c) { return c.Standable(map); }, out spot))
                        {
                            Thing s = ThingMaker.MakeThing(ThingDefOf.Steel);
                            s.stackCount = Mathf.Min(Rand.RangeInclusive(40, 120), s.def.stackLimit);
                            GenSpawn.Spawn(s, spot, map);
                        }
                    }
                    if (Rand.Chance(0.35f))
                    {
                        IntVec3 spot;
                        if (CellFinder.TryFindRandomCellInsideWith(room, delegate(IntVec3 c) { return c.Standable(map); }, out spot))
                        {
                            Thing s = ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial);
                            s.stackCount = Mathf.Min(Rand.RangeInclusive(2, 6), s.def.stackLimit);
                            GenSpawn.Spawn(s, spot, map);
                        }
                    }
                }
            }
        }
    }
}
