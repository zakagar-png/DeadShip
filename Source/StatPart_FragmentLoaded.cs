using RimWorld;
using Verse;

namespace DeadShip
{
    // Added to the research-speed factor stat (see Patches/ResearchBenchStat.xml). A bench that holds
    // a fuel slot (the Marker study bench) gets a speed of zero until a fragment is loaded.
    public class StatPart_FragmentLoaded : StatPart
    {
        private static CompRefuelable FragmentSlot(StatRequest req)
        {
            ThingWithComps thing = req.Thing as ThingWithComps;
            if (thing == null || thing.def.defName != "DS_MarkerBench")
            {
                return null;
            }
            return thing.GetComp<CompRefuelable>();
        }

        public override void TransformValue(StatRequest req, ref float val)
        {
            CompRefuelable slot = FragmentSlot(req);
            if (slot != null && !slot.HasFuel)
            {
                val = 0f;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            CompRefuelable slot = FragmentSlot(req);
            if (slot != null && !slot.HasFuel)
            {
                return "No Marker fragment loaded: x0";
            }
            return null;
        }
    }
}
