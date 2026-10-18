using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeadShip
{
    public class CompProperties_AirHandler : CompProperties
    {
        public CompProperties_AirHandler()
        {
            compClass = typeof(CompAirHandler);
        }
    }

    // The air handler's settings: the temperature it holds in every vented room. The work is done in MapComponent_Air.
    public class CompAirHandler : ThingComp
    {
        public float targetTemp = 21f;
        public int additive = 0;   // index into Additives, 0 = none

        // label, hediff, research needed (null = none), what it does
        public static readonly string[][] Additives = new string[][]
        {
            new string[] { "none", null, null, "" },
            new string[] { "wakeful air", "DS_Air_Wakeful", "DS_AirAdditives", "needs less sleep" },
            new string[] { "calming air", "DS_Air_Calming", "DS_AirAdditives", "faster sanity recovery" },
            new string[] { "nootropic air", "DS_Air_Nootropic", "DS_CognitiveAerosols", "higher Consciousness" },
            new string[] { "neuromotor air", "DS_Air_Tonic", "DS_CognitiveAerosols", "better Manipulation" },
            new string[] { "sociable air", "DS_Air_Sociable", "DS_CognitiveAerosols", "better social skill" },
            new string[] { "adrenal air", "DS_Air_Adrenal", "DS_PerformanceAerosols", "faster work" },
            new string[] { "euphoric air", "DS_Air_Euphoric", "DS_PerformanceAerosols", "better mood" }
        };

        public bool AdditiveUnlocked(int i)
        {
            return i == 0 || MarkerResearch.Done(Additives[i][2]);
        }

        // The additive in use, or null if none is chosen or its research is missing.
        public string ActiveHediff()
        {
            if (additive <= 0 || additive >= Additives.Length || !AdditiveUnlocked(additive))
            {
                return null;
            }
            return Additives[additive][1];
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref targetTemp, "ds_targetTemp", 21f);
            Scribe_Values.Look(ref additive, "ds_additive", 0);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            Command_Action lower = new Command_Action();
            lower.defaultLabel = "Cooler";
            lower.defaultDesc = "Lower the target temperature by 1 degree. Now " + targetTemp.ToStringTemperature("F0") + ".";
            lower.icon = ContentFinder<Texture2D>.Get("UI/Commands/TempLower", true);
            lower.action = delegate { targetTemp = Mathf.Max(5f, targetTemp - 1f); };
            yield return lower;

            Command_Action raise = new Command_Action();
            raise.defaultLabel = "Warmer";
            raise.defaultDesc = "Raise the target temperature by 1 degree. Now " + targetTemp.ToStringTemperature("F0") + ".";
            raise.icon = ContentFinder<Texture2D>.Get("UI/Commands/TempRaise", true);
            raise.action = delegate { targetTemp = Mathf.Min(35f, targetTemp + 1f); };
            yield return raise;

            if (MarkerResearch.Done("DS_AirAdditives"))
            {
                Command_Action chem = new Command_Action();
                chem.defaultLabel = "Additive: " + Additives[additive][0];
                chem.defaultDesc = "Choose a chemical to pump into every vented room (costs extra power). Click to choose from a list. Now: " + Additives[additive][0]
                    + (additive > 0 ? " (" + Additives[additive][3] + ")" : "") + ".";
                chem.icon = ContentFinder<Texture2D>.Get("UI/Commands/TempRaise", true);
                chem.action = delegate
                {
                    List<FloatMenuOption> opts = new List<FloatMenuOption>();
                    for (int n = 0; n < Additives.Length; n++)
                    {
                        if (!AdditiveUnlocked(n))
                        {
                            continue;
                        }
                        int pick = n;
                        string label = n == 0 ? "None" : Additives[n][0] + " (" + Additives[n][3] + ")";
                        opts.Add(new FloatMenuOption(label, delegate { additive = pick; }));
                    }
                    Find.WindowStack.Add(new FloatMenu(opts));
                };
                yield return chem;
            }
        }

        public override string CompInspectStringExtra()
        {
            MapComponent_Air air = MapComponent_Air.Of(parent.Map);
            string report = air == null ? null : air.Report(parent);
            string text = "Target temperature: " + targetTemp.ToStringTemperature("F0");
            if (ActiveHediff() != null)
            {
                text += "\nAdditive: " + Additives[additive][0] + " (" + Additives[additive][3] + ")";
            }
            if (report != null)
            {
                text += "\n" + report;
            }
            return text;
        }
    }
}
