using RimWorld;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // Air vent that shows a breached grate below half health.
    [StaticConstructorOnStartup]
    public class Building_Vent : Building
    {
        private static Graphic broken;
        private bool wasBroken;

        public override Graphic Graphic
        {
            get
            {
                if (MapComponent_Air.IsBroken(this))
                {
                    if (broken == null)
                    {
                        broken = GraphicDatabase.Get<Graphic_Single>("Things/Building/DS_Vent_Broken", ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
                    }
                    return broken;
                }
                return base.Graphic;
            }
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            Refresh();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            wasBroken = MapComponent_Air.IsBroken(this);
        }

        public override void TickRare()
        {
            Refresh();
        }

        private void Refresh()
        {
            bool b = MapComponent_Air.IsBroken(this);
            if (b != wasBroken && Spawned)
            {
                wasBroken = b;
                Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);
            }
        }
    }
}
