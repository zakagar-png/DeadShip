using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace DeadShip
{
    // The ventilation system. Buried ducts (and vents and intake grilles, which join the same network) lead from an air handler to
    // the rooms it serves. Every couple of seconds this walks each handler's duct network and:
    //  - heats or cools every room that has a vent on the network toward the handler's target temperature (and uses power for it);
    //  - marks those rooms as vented (a work speed bonus, a small mood bonus and faster sanity recovery) and, if the network
    //    also has an open intake grille, as fresh-aired (a bigger mood and work bonus);
    //  - remembers which vents are on a network with an open intake, because during a raid some small necromorphs crawl in
    //    through the ducts and come out of the vents. Nothing stops that.
    [StaticConstructorOnStartup]
    public class MapComponent_Air : MapComponent
    {
        public const string HandlerDefName = "DS_AirHandler";
        public const string DuctDefName = "DS_Duct";
        public const string VentDefName = "DS_Vent";
        public const string IntakeDefName = "DS_Intake";

        private const int Interval = 120;
        private const float EnergyPerVent = 30f;
        private const float AdditivePower = 240f;   // extra watts while an additive is pumped
        private const float BasePower = 300f;
        private const float PowerPerEnergy = 6f;

        private static readonly string[] EmergeKinds = { "DS_Slasher", "DS_Pack", "DS_Lurker", "DS_Exploder", "DS_Leaper", "DS_Puker", "DS_Pregnant", "DS_Swarmer" };
        private static readonly float[] EmergeWeights = { 10f, 8f, 6f, 5f, 5f, 4f, 3f, 2f };

        private HashSet<int> vented = new HashSet<int>();
        private HashSet<int> fresh = new HashSet<int>();
        private List<Thing> emergeVents = new List<Thing>();
        private List<Thing> emergeIntakes = new List<Thing>();
        private List<Thing> curIntakes = new List<Thing>();
        private List<Pawn> crawlers = new List<Pawn>();
        private List<Thing> crawlerIntakes = new List<Thing>();
        private List<Thing> brokenVents = new List<Thing>();
        private string diag = "";
        private Dictionary<int, string> roomAdditive = new Dictionary<int, string>();
        private static readonly string[] AdditiveHediffs = { "DS_Air_Wakeful", "DS_Air_Calming", "DS_Air_Nootropic", "DS_Air_Adrenal", "DS_Air_Tonic", "DS_Air_Sociable", "DS_Air_Euphoric" };
        private bool anyIntakeOpen;
        private Dictionary<int, string> reports = new Dictionary<int, string>();

        public MapComponent_Air(Map map) : base(map)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref crawlers, "ds_crawlers", LookMode.Reference);
            Scribe_Collections.Look(ref crawlerIntakes, "ds_crawlerIntakes", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (crawlers == null) crawlers = new List<Pawn>();
                if (crawlerIntakes == null) crawlerIntakes = new List<Thing>();
                while (crawlerIntakes.Count < crawlers.Count) crawlerIntakes.Add(null);
            }
        }

        public static MapComponent_Air Of(Map map)
        {
            return map == null ? null : map.GetComponent<MapComponent_Air>();
        }

        // 0 = not vented, 1 = vented, 2 = vented with fresh air.
        public static int RoomState(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
            {
                return 0;
            }
            MapComponent_Air air = Of(pawn.Map);
            if (air == null)
            {
                return 0;
            }
            Room room = pawn.Position.GetRoom(pawn.Map);
            if (room == null)
            {
                return 0;
            }
            if (air.fresh.Contains(room.ID))
            {
                return 2;
            }
            return air.vented.Contains(room.ID) ? 1 : 0;
        }

        public string Report(Thing handler)
        {
            string text;
            return reports.TryGetValue(handler.thingIDNumber, out text) ? text : null;
        }

        public override void MapComponentTick()
        {
            if (Find.TickManager.TicksGame % Interval == 0)
            {
                Rebuild();
            }
            if (Find.TickManager.TicksGame % 30 == 0)
            {
                ApplyAdditives();
                TickCrawlers();
            }
        }

        public void Rebuild()
        {
            vented.Clear();
            fresh.Clear();
            emergeVents.Clear();
            emergeIntakes.Clear();
            reports.Clear();
            diag = "";
            roomAdditive.Clear();
            anyIntakeOpen = false;

            ThingDef handlerDef = DefDatabase<ThingDef>.GetNamedSilentFail(HandlerDefName);
            if (handlerDef == null)
            {
                return;
            }
            List<Thing> handlers = map.listerThings.ThingsOfDef(handlerDef);
            for (int i = 0; i < handlers.Count; i++)
            {
                RunHandler(handlers[i]);
            }
        }

        // Colonists in a room that is being pumped get the buff straight away; the moment they leave it, they lose it.
        private void ApplyAdditives()
        {
            List<Pawn> cols = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < cols.Count; i++)
            {
                Pawn p = cols[i];
                string want = null;
                Room r = p.GetRoom();
                if (r != null)
                {
                    roomAdditive.TryGetValue(r.ID, out want);
                }
                for (int k = 0; k < AdditiveHediffs.Length; k++)
                {
                    HediffDef hd = DefDatabase<HediffDef>.GetNamedSilentFail(AdditiveHediffs[k]);
                    if (hd == null)
                    {
                        continue;
                    }
                    Hediff h = p.health.hediffSet.GetFirstHediffOfDef(hd);
                    if (AdditiveHediffs[k] == want)
                    {
                        if (h == null)
                        {
                            p.health.AddHediff(HediffMaker.MakeHediff(hd, p));
                        }
                        else
                        {
                            HediffComp_Disappears d = h.TryGetComp<HediffComp_Disappears>();
                            if (d != null)
                            {
                                d.ResetElapsedTicks();
                            }
                        }
                    }
                    else if (h != null)
                    {
                        p.health.RemoveHediff(h);
                    }
                }
            }
        }

        private void RunHandler(Thing handler)
        {
            List<Thing> vents = new List<Thing>();
            brokenVents.Clear();
            curIntakes.Clear();
            int intakes = 0;
            bool intakeOpen = false;
            Walk(handler, vents, ref intakes, ref intakeOpen);

            CompPowerTrader power = handler.TryGetComp<CompPowerTrader>();
            CompAirHandler comp = handler.TryGetComp<CompAirHandler>();
            bool on = power == null || power.PowerOn;
            string additive = comp != null ? comp.ActiveHediff() : null;
            float target = comp != null ? comp.targetTemp : 21f;

            float used = 0f;
            int rooms = 0;
            if (on)
            {
                Dictionary<Room, List<Thing>> byRoom = new Dictionary<Room, List<Thing>>();
                for (int i = 0; i < vents.Count; i++)
                {
                    Room room = vents[i].Position.GetRoom(map);
                    if (room == null || room.PsychologicallyOutdoors || room.UsesOutdoorTemperature)
                    {
                        continue;
                    }
                    List<Thing> list;
                    if (!byRoom.TryGetValue(room, out list))
                    {
                        list = new List<Thing>();
                        byRoom[room] = list;
                    }
                    list.Add(vents[i]);
                }

                foreach (KeyValuePair<Room, List<Thing>> pair in byRoom)
                {
                    Room room = pair.Key;
                    rooms++;
                    vented.Add(room.ID);
                    if (additive != null)
                    {
                        roomAdditive[room.ID] = additive;
                    }
                    if (intakeOpen)
                    {
                        fresh.Add(room.ID);
                    }

                    float diff = target - room.Temperature;
                    if (Mathf.Abs(diff) > 0.5f)
                    {
                        float cap = EnergyPerVent * pair.Value.Count;
                        float energy = Mathf.Clamp(diff * room.CellCount, -cap, cap);
                        room.PushHeat(energy);
                        used += Mathf.Abs(energy);
                    }
                }

            }

            if (on && additive != null)
            {
                used += AdditivePower / PowerPerEnergy;
            }

            diag += "handler at " + handler.Position + ": vents=" + vents.Count + " broken=" + brokenVents.Count + " intakes=" + intakes + " intakeOpen=" + intakeOpen + "; ";
            if (intakeOpen) anyIntakeOpen = true;
            if (curIntakes.Count > 0)
            {
                emergeIntakes.AddRange(curIntakes);
                emergeVents.AddRange(vents);
                emergeVents.AddRange(brokenVents);
            }

            if (power != null)
            {
                power.PowerOutput = -(BasePower + used * PowerPerEnergy);
            }

            string intakeText;
            if (intakes == 0)
            {
                intakeText = "no intake (recycled air)";
            }
            else if (!intakeOpen)
            {
                intakeText = "intake blocked (under a roof)";
            }
            else
            {
                intakeText = "intake open";
            }
            reports[handler.thingIDNumber] = on
                ? vents.Count + " vent(s) in " + rooms + " room(s), " + intakeText + "."
                : "No power.";
        }

        private static bool IsDuctPart(Thing t)
        {
            string n = t.def.defName;
            return n == DuctDefName || n == VentDefName || n == IntakeDefName;
        }

        private bool IsDuctCell(IntVec3 c)
        {
            List<Thing> things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (IsDuctPart(things[i]))
                {
                    return true;
                }
            }
            return false;
        }

        // Flood fill from the handler along ducts, vents and intakes.
        private void Walk(Thing handler, List<Thing> vents, ref int intakes, ref bool intakeOpen)
        {
            HashSet<IntVec3> seen = new HashSet<IntVec3>();
            Queue<IntVec3> queue = new Queue<IntVec3>();
            foreach (IntVec3 c in GenAdj.CellsAdjacentCardinal(handler))
            {
                if (c.InBounds(map) && IsDuctCell(c) && seen.Add(c))
                {
                    queue.Enqueue(c);
                }
            }

            while (queue.Count > 0)
            {
                IntVec3 cell = queue.Dequeue();
                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    if (things[i].def.defName == VentDefName)
                    {
                        if (!IsBroken(things[i])) vents.Add(things[i]);
                        else brokenVents.Add(things[i]);
                    }
                    else if (things[i].def.defName == IntakeDefName)
                    {
                        intakes++;
                        curIntakes.Add(things[i]);
                        if (!cell.Roofed(map))
                        {
                            intakeOpen = true;
                        }
                    }
                }

                for (int d = 0; d < 4; d++)
                {
                    IntVec3 n = cell + GenAdj.CardinalDirections[d];
                    if (n.InBounds(map) && !seen.Contains(n) && IsDuctCell(n))
                    {
                        seen.Add(n);
                        queue.Enqueue(n);
                    }
                }
            }
        }

        private static string PickKind()
        {
            return NecroUnlock.Pick(EmergeKinds, EmergeWeights);
        }

        // Called when a necromorph raid has just fired: some small necromorphs come out of the vents instead of walking in.
        private static Material[] linkMats;

        private static bool IsAirPart(string n)
        {
            return n == DuctDefName || n == VentDefName || n == IntakeDefName || n == HandlerDefName;
        }

        private bool ShowPipes()
        {
            Designator d = Find.DesignatorManager.SelectedDesignator;
            Designator_Build b = d as Designator_Build;
            if (b != null && b.PlacingDef != null && IsAirPart(b.PlacingDef.defName))
            {
                return true;
            }
            for (int i = 0; i < Find.Selector.SelectedObjects.Count; i++)
            {
                Thing t = Find.Selector.SelectedObjects[i] as Thing;
                if (t != null && IsAirPart(t.def.defName))
                {
                    return true;
                }
            }
            return false;
        }

        private bool Links(IntVec3 c)
        {
            if (!c.InBounds(map))
            {
                return false;
            }
            List<Thing> things = c.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                if (IsAirPart(things[i].def.defName))
                {
                    return true;
                }
            }
            return false;
        }

        public override void MapComponentUpdate()
        {
            base.MapComponentUpdate();
            if (Find.CurrentMap != map || !ShowPipes())
            {
                return;
            }
            ThingDef duct = DefDatabase<ThingDef>.GetNamedSilentFail(DuctDefName);
            if (duct == null)
            {
                return;
            }
            if (linkMats == null)
            {
                linkMats = new Material[16];
                for (int m = 0; m < 16; m++)
                {
                    linkMats[m] = MaterialPool.MatFrom("Things/Building/DS_Duct_Link_" + m, ShaderDatabase.Transparent, new Color(1f, 1f, 1f, 0.5f));
                }
            }
            CellRect view = Find.CameraDriver.CurrentViewRect;
            System.Collections.Generic.List<Thing> list = map.listerThings.ThingsOfDef(duct);
            for (int i = 0; i < list.Count; i++)
            {
                IntVec3 c = list[i].Position;
                if (!view.Contains(c))
                {
                    continue;
                }
                int mask = 0;
                if (Links(c + IntVec3.North)) mask |= 1;
                if (Links(c + IntVec3.East)) mask |= 2;
                if (Links(c + IntVec3.South)) mask |= 4;
                if (Links(c + IntVec3.West)) mask |= 8;
                Vector3 pos = c.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one), linkMats[mask], 0);
            }
        }

        // A vent below half health is breached: it stops working until repaired.
        public static bool IsBroken(Thing vent)
        {
            return vent.HitPoints * 2 < vent.MaxHitPoints;
        }

        // Called just after a necromorph raid fires. Some of the raiders (never a brute) do not walk in with the rest:
        // they head for an air intake, crawl in, and come out of a vent inside the base.
        public static void SendCrawlers(Map map, Faction faction, List<Pawn> raiders, int fragments)
        {
            MapComponent_Air air = Of(map);
            if (air == null || faction == null || raiders == null || raiders.Count == 0)
            {
                return;
            }
            air.Rebuild();
            Log.Message("Dead Ship vents: " + (air.diag.Length == 0 ? "no air handler on the map" : air.diag)
                + " intakes=" + air.emergeIntakes.Count + " vents=" + air.emergeVents.Count);
            if (air.emergeIntakes.Count == 0 || air.emergeVents.Count == 0)
            {
                return;
            }

            List<Pawn> pool = new List<Pawn>();
            for (int i = 0; i < raiders.Count; i++)
            {
                Pawn p = raiders[i];
                if (p != null && p.Spawned && !p.Dead && p.kindDef.defName != "DS_Brute")
                {
                    pool.Add(p);
                }
            }
            int count = Mathf.Min(Mathf.Clamp(1 + fragments / 4 + (air.anyIntakeOpen ? 2 : 0), 1, 7), (raiders.Count + 1) / 2);
            count = Mathf.Min(count, pool.Count);
            for (int n = 0; n < count; n++)
            {
                Pawn p = pool[Rand.Range(0, pool.Count)];
                pool.Remove(p);
                air.TryAddCrawler(p);
            }
            if (air.crawlers.Count > 0)
            {
                air.TickCrawlers();
            }
        }

        private bool TryAddCrawler(Pawn p)
        {
            if (crawlers.Contains(p))
            {
                return true;
            }
            if (emergeIntakes.Count == 0 || emergeVents.Count == 0)
            {
                return false;
            }
            Thing nearest = null;
            float best = 999999f;
            for (int k = 0; k < emergeIntakes.Count; k++)
            {
                Thing it = emergeIntakes[k];
                float d = (it.Position - p.Position).LengthHorizontalSquared;
                if (it.Spawned && d < best && p.CanReach(it.Position, PathEndMode.OnCell, Danger.Deadly))
                {
                    best = d;
                    nearest = it;
                }
            }
            if (nearest == null)
            {
                return false;
            }
            Lord lord = p.GetLord();
            if (lord != null)
            {
                lord.Notify_PawnLost(p, PawnLostCondition.ForcedByPlayerAction);
            }
            crawlers.Add(p);
            crawlerIntakes.Add(nearest);
            return true;
        }

        // A necromorph that cannot reach any colonist (they are walled in) goes for an air intake instead.
        public static bool TryCrawl(Pawn p)
        {
            if (p.Map == null || p.kindDef.defName == "DS_Brute")
            {
                return false;
            }
            MapComponent_Air air = Of(p.Map);
            return air != null && air.TryAddCrawler(p);
        }

        private void FreeCrawler(Pawn p)
        {
            if (p != null && p.Spawned && !p.Dead && p.GetLord() == null)
            {
                LordMaker.MakeNewLord(p.Faction, new LordJob_AssaultColony(p.Faction, false, false, false, false, false, false, false), map, new Pawn[] { p });
            }
        }

        private void TickCrawlers()
        {
            List<Pawn> entered = new List<Pawn>();
            for (int i = crawlers.Count - 1; i >= 0; i--)
            {
                Pawn p = crawlers[i];
                Thing intake = crawlerIntakes[i];
                if (p == null || p.Destroyed || p.Dead || !p.Spawned || p.Map != map)
                {
                    crawlers.RemoveAt(i);
                    crawlerIntakes.RemoveAt(i);
                    continue;
                }
                if (intake == null || !intake.Spawned || emergeVents.Count == 0)
                {
                    FreeCrawler(p);
                    crawlers.RemoveAt(i);
                    crawlerIntakes.RemoveAt(i);
                    continue;
                }

                if ((p.Position - intake.Position).LengthHorizontalSquared <= 2.3f * 2.3f)
                {
                    Thing vent = emergeVents[Rand.Range(0, emergeVents.Count)];
                    IntVec3 spot = CellFinder.StandableCellNear(vent.Position, map, 2f);
                    if (!spot.IsValid)
                    {
                        spot = vent.Position;
                    }
                    FleckMaker.ThrowDustPuff(p.Position, map, 1.2f);
                    p.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                    p.pather.StopDead();
                    p.Position = spot;
                    p.Notify_Teleported(true, true);
                    FleckMaker.ThrowDustPuff(spot, map, 1.5f);
                    if (vent.Spawned && !IsBroken(vent))
                    {
                        vent.HitPoints = Mathf.Max(1, vent.MaxHitPoints * 2 / 5);
                        map.mapDrawer.MapMeshDirty(vent.Position, MapMeshFlagDefOf.Things);
                    }
                    entered.Add(p);
                    crawlers.RemoveAt(i);
                    crawlerIntakes.RemoveAt(i);
                    continue;
                }

                if (p.CurJob == null || p.CurJob.def != JobDefOf.Goto || p.CurJob.targetA.Cell != intake.Position)
                {
                    if (!p.CanReach(intake.Position, PathEndMode.OnCell, Danger.Deadly))
                    {
                        FreeCrawler(p);
                        crawlers.RemoveAt(i);
                        crawlerIntakes.RemoveAt(i);
                        continue;
                    }
                    Job go = JobMaker.MakeJob(JobDefOf.Goto, intake.Position);
                    go.locomotionUrgency = LocomotionUrgency.Sprint;
                    go.expiryInterval = 600;
                    p.jobs.StartJob(go, JobCondition.InterruptForced);
                }
            }

            if (entered.Count > 0)
            {
                LordMaker.MakeNewLord(entered[0].Faction, new LordJob_AssaultColony(entered[0].Faction, false, false, false, false, false, false, false), map, entered);
                Find.LetterStack.ReceiveLetter("Necromorphs in the vents",
                    "Necromorphs found the air intake, crawled through the ducts, and have come out of the vents inside the base. "
                    + entered.Count + " of them.",
                    LetterDefOf.ThreatBig, entered[0]);
            }
        }
    }
}
