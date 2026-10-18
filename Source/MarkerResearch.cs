using UnityEngine;
using Verse;

namespace DeadShip
{
    // What the Marker Studies research does to the Marker. Two chains:
    //  Resonance (tuning, focusing, carrier wave): the Marker gives more power. It also broadcasts a stronger signal, so the dread it
    //  gives off grows a little with each step.
    //  Damping (attenuation, neural shielding, cognitive isolation): each step cuts the dread and the raids it draws.
    // The dread never reaches nothing: the signal is the Marker, and it only gets quieter, not silent.
    public static class MarkerResearch
    {
        private static readonly string[] Resonance = { "DS_ResonanceTuning", "DS_HarmonicFocusing", "DS_CarrierWave" };
        private static readonly float[] PowerSteps = { 1.25f, 1.2f, 1.33f }; // cumulative 1.25, 1.5, 2.0

        private static readonly string[] Damping = { "DS_SignalAttenuation", "DS_NeuralShielding", "DS_CognitiveIsolation" };
        private static readonly float[] DampSteps = { 0.85f, 0.8f, 0.75f }; // cumulative 0.85, 0.68, 0.51

        // The loudest the signal gets for each Resonance step, and the least it can ever be.
        private const float LoudnessPerResonance = 0.08f;
        public const float MinDread = 0.4f;

        public static bool Done(string defName)
        {
            ResearchProjectDef def = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            return def != null && def.IsFinished;
        }

        public static int ResonanceTier()
        {
            int tier = 0;
            for (int i = 0; i < Resonance.Length; i++)
            {
                if (Done(Resonance[i]))
                {
                    tier = i + 1;
                }
            }
            return tier;
        }

        public static int DampingTier()
        {
            int tier = 0;
            for (int i = 0; i < Damping.Length; i++)
            {
                if (Done(Damping[i]))
                {
                    tier = i + 1;
                }
            }
            return tier;
        }

        // Necromorph knowledge: how much more often a plasma cut goes for a limb. Anatomy +0.15, weak point mapping another +0.15.
        public static float LimbChanceBonus()
        {
            float b = 0f;
            if (Done("DS_NecromorphAnatomy")) { b += 0.15f; }
            if (Done("DS_WeakPointMapping")) { b += 0.15f; }
            return b;
        }

        // Detection: signal triangulation brings wreck sites closer; early warning gives longer to deal with a rising body.
        public static bool Triangulated() { return Done("DS_SignalTriangulation"); }
        public static float OutbreakFuseFactor() { return Done("DS_EarlyWarning") ? 1.6f : 1f; }

        // Multiplier on the power each fragment gives.
        public static float PowerMultiplier()
        {
            float m = 1f;
            int tier = ResonanceTier();
            for (int i = 0; i < tier; i++)
            {
                m *= PowerSteps[i];
            }
            return m;
        }

        // Multiplier on how far and how hard the dread reaches, and on how much it draws raids. 1 with no research; never below MinDread.
        public static float DreadMultiplier()
        {
            float m = 1f + LoudnessPerResonance * ResonanceTier();
            int tier = DampingTier();
            for (int i = 0; i < tier; i++)
            {
                m *= DampSteps[i];
            }
            return Mathf.Max(MinDread, m);
        }
    }
}
