using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // Settings for the Marker site, set in Defs/ThingDefs/MarkerBench.xml.
    public class MarkerSiteExtension : DefModExtension
    {
        // Name of the finished building that replaces the site once enough fragments are loaded.
        public string finishedDef = "DS_FullMarker";
        // Texture for each number of fragments loaded (index = fragments loaded).
        public List<string> stageTextures = new List<string>();
    }

    // The scaffold the colony builds for the Marker. Colonists load fragments into it one at a time (the
    // normal refuel job); the scaffold changes picture as it fills, and when the last fragment goes in it
    // becomes the finished Marker.
    public class Building_MarkerSite : Building
    {
        private static readonly Dictionary<string, Graphic> graphics = new Dictionary<string, Graphic>();

        private CompRefuelable fuel;

        private CompRefuelable Fuel
        {
            get
            {
                if (fuel == null) { fuel = GetComp<CompRefuelable>(); }
                return fuel;
            }
        }

        private int Loaded
        {
            get { return Fuel == null ? 0 : Mathf.FloorToInt(Fuel.Fuel + 0.001f); }
        }

        public override Graphic Graphic
        {
            get
            {
                MarkerSiteExtension ext = def.GetModExtension<MarkerSiteExtension>();
                if (ext == null || ext.stageTextures.Count == 0) { return base.Graphic; }
                // Spread the pictures evenly over however many fragments the scaffold takes.
                float capacity = Fuel == null ? 1f : Fuel.Props.fuelCapacity;
                int stage = Mathf.Clamp(Mathf.FloorToInt(Loaded * ext.stageTextures.Count / capacity), 0, ext.stageTextures.Count - 1);
                string path = ext.stageTextures[stage];
                Graphic cached;
                if (!graphics.TryGetValue(path, out cached))
                {
                    GraphicData data = new GraphicData();
                    data.CopyFrom(def.graphicData);
                    data.texPath = path;
                    cached = data.Graphic;
                    graphics[path] = cached;
                }
                return cached;
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || !this.IsHashIntervalTick(60)) { return; }

            CompRefuelable comp = Fuel;
            if (comp != null && comp.Fuel >= comp.Props.fuelCapacity - 0.001f)
            {
                Finish();
            }
        }

        private void Finish()
        {
            MarkerSiteExtension ext = def.GetModExtension<MarkerSiteExtension>();
            ThingDef finished = ext == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(ext.finishedDef);
            if (finished == null) { return; }

            Map map = Map;
            IntVec3 cell = Position;
            Faction faction = Faction;
            Rot4 rot = Rotation;

            // The fragments are consumed into the Marker.
            Fuel.ConsumeFuel(Fuel.Fuel);
            Destroy(DestroyMode.Vanish);

            Thing marker = ThingMaker.MakeThing(finished);
            marker.SetFaction(faction);
            GenSpawn.Spawn(marker, cell, map, rot);

            Messages.Message("The Marker is complete.", new TargetInfo(cell, map), MessageTypeDefOf.NeutralEvent);
        }
    }
}
