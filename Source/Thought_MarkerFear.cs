using RimWorld;
using Verse;

namespace DeadShip
{
    // A situational thought caused by the Marker: its mood effect is scaled by how psychically sensitive the colonist is.
    // (Psychically deaf colonists never get it at all; see the thought workers.)
    public class Thought_MarkerFear : Thought_Situational
    {
        public override float MoodOffset()
        {
            return base.MoodOffset() * MarkerUtility.Susceptibility(pawn);
        }
    }
}
