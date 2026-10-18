using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // The map parent of a wreck site. A caravan can right-click it to go in, and once the player has left (no colonists on
    // the map and no pawn keeping it open) the site and its map are removed.
    public class WreckSite : MapParent
    {
        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            foreach (FloatMenuOption option in base.GetFloatMenuOptions(caravan))
            {
                yield return option;
            }

            // Before the map exists the base class offers nothing, so add the "enter" option here.
            if (!HasMap)
            {
                foreach (FloatMenuOption option in CaravanArrivalAction_Enter.GetFloatMenuOptions(caravan, this))
                {
                    yield return option;
                }
            }
        }

        // A caravan already standing on the site's tile gets a button to go in.
        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            foreach (Gizmo gizmo in base.GetCaravanGizmos(caravan))
            {
                yield return gizmo;
            }

            if (caravan.Tile == Tile)
            {
                Caravan who = caravan;
                Command_Action enter = new Command_Action();
                enter.defaultLabel = "Enter wreck";
                enter.defaultDesc = "Take the caravan into the wreckage.";
                enter.icon = ContentFinder<Texture2D>.Get("UI/DS_EnterWreck", false);
                enter.action = delegate
                {
                    EnterWith(who);
                };
                yield return enter;
            }
        }

        // Builds the wreck map if it does not exist yet and takes the caravan in at the edge.
        private void EnterWith(Caravan caravan)
        {
            try
            {
                Map map = GetOrGenerateMapUtility.GetOrGenerateMap(Tile, new IntVec3(100, 1, 100), null);
                if (map == null)
                {
                    Log.Error("Dead Ship: the wreck map could not be generated.");
                    return;
                }
                CaravanEnterMapUtility.Enter(caravan, map, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, true);
                Current.Game.CurrentMap = map;
                CameraJumper.TryJump(map.Center, map);
            }
            catch (System.Exception ex)
            {
                Log.Error("Dead Ship: entering the wreck failed: " + ex);
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            alsoRemoveWorldObject = true;
            return !Map.mapPawns.AnyColonistSpawned && !Map.mapPawns.AnyPawnBlockingMapRemoval;
        }
    }
}
