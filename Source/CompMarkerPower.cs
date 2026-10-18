using RimWorld;
using UnityEngine;
using Verse;

namespace DeadShip
{
    public class CompProperties_MarkerPower : CompProperties_Power
    {
        // Power given for every Marker fragment loaded.
        public float wattsPerFragment = 4000f;

        public CompProperties_MarkerPower()
        {
            compClass = typeof(CompMarkerPower);
        }
    }

    // A power plant whose output grows with the fragments loaded: nothing with none, a steady amount for each one.
    public class CompMarkerPower : CompPowerPlant
    {
        private CompProperties_MarkerPower MarkerProps
        {
            get { return (CompProperties_MarkerPower)props; }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (PowerOn)
            {
                PowerOutput = MarkerUtility.FragmentsLoaded(parent) * MarkerProps.wattsPerFragment * MarkerResearch.PowerMultiplier();
            }
        }

        // Shows whether the walls are containing it, so you can see why the dread is or is not muffled.
        public override string CompInspectStringExtra()
        {
            string baseText = base.CompInspectStringExtra();
            string line = MarkerUtility.FragmentsLoaded(parent) + " fragment(s). "
                + "Power x" + MarkerResearch.PowerMultiplier().ToString("0.##") + ", dread x" + MarkerResearch.DreadMultiplier().ToString("0.##") + ". "
                + (MarkerUtility.IsContained(parent) ? "Contained: dread muffled." : "Not contained: " + MarkerUtility.Diagnose(parent) + ".");
            return string.IsNullOrEmpty(baseText) ? line : baseText + "\n" + line;
        }
    }
}
