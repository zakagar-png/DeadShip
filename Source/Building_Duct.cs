using RimWorld;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // Buried air duct: not drawn in the normal map mesh. MapComponent_Air draws the connected pipe
    // network only while the player is placing or selecting air-system parts.
    public class Building_Duct : Building
    {
        public override void Print(SectionLayer layer)
        {
        }
    }
}
