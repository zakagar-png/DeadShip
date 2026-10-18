using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DeadShip
{
    // Scenario part: every starting colonist begins the game already wearing an engineering rig and helmet.
    public class ScenPart_DressStartingPawns : ScenPart
    {
        public override void PostGameStart()
        {
            base.PostGameStart();
            Map map = Find.CurrentMap;
            if (map == null) return;

            ThingDef suit = DefDatabase<ThingDef>.GetNamedSilentFail("DS_EngineerSuit");
            ThingDef helmet = DefDatabase<ThingDef>.GetNamedSilentFail("DS_EngineerHelmet");

            List<Pawn> colonists = new List<Pawn>(map.mapPawns.FreeColonists);
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn p = colonists[i];
                if (p.apparel == null || p.RaceProps == null || !p.RaceProps.Humanlike) continue;
                Wear(p, suit);
                Wear(p, helmet);
            }
        }

        private static void Wear(Pawn p, ThingDef def)
        {
            if (def == null) return;
            if (!ApparelUtility.HasPartsToWear(p, def)) return;
            ThingDef stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            Apparel a = ThingMaker.MakeThing(def, stuff) as Apparel;
            if (a == null) return;
            // Anything already worn that clashes is dropped by Wear.
            p.apparel.Wear(a);
        }
    }
}
