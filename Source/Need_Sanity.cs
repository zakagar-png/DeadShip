using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // Sanity: how well a colonist is holding up against the Marker's signal. It drains while the dread reaches them (faster the
    // closer they are), when necromorphs are near, and recovers when they are out of reach, sleeping or enjoying themselves.
    // Low sanity gives a mood penalty (ThoughtWorker_Sanity) and, lower still, mental breaks. At the very bottom the signal
    // takes over and calls them to the Marker.
    // The Marker research chains and containment change how far the dread reaches, so they change the drain.
    public class Need_Sanity : Need
    {
        // Drain per need interval (150 ticks) for each dread stage, worst first. About 1.6, 1.1, 0.7 and 0.35 per day.
        private static readonly float[] StageDrain = { 0.0040f, 0.0028f, 0.0018f, 0.0009f };

        // The engineering rig and helmet shield the mind a little: each piece cuts the drain by 20%.
        private static float SuitFactor(Pawn pawn)
        {
            float f = 1f;
            if (pawn.apparel != null)
            {
                List<Apparel> worn = pawn.apparel.WornApparel;
                for (int i = 0; i < worn.Count; i++)
                {
                    if (worn[i].def.defName == "DS_EngineerSuit" || worn[i].def.defName == "DS_EngineerHelmet")
                    {
                        f -= 0.2f;
                    }
                }
            }
            return f;
        }

        private const float NecromorphDrain = 0.0020f;
        private const float VentedRecovery = 0.0006f;
        private const float NecromorphRange = 12f;
        private const float AmbientDrain = 0.00015f; // per four fragments loaded, anywhere on the map
        private const float FaintDrain = 0.0006f; // about 0.24 a day
        private const float BaseRecovery = 0.0010f;
        private const float SleepRecovery = 0.0015f;
        private const float JoyRecovery = 0.0005f;

        private const float MildBreak = 0.20f;
        private const float SevereBreak = 0.10f;
        private const float CalledBreak = 0.03f;

        // How long a colonist can stay below 10% sanity before the Marker claims them: two days of total time.
        // Time above 10% wears the count back down, but only at a third of the speed it built up, and mental-break relief does not reset it.
        private const float EmbraceLevel = 0.10f;
        private const int EmbraceTicks = 120000;
        private int lowTicks;
        private bool warned;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lowTicks, "ds_lowTicks", 0);
            Scribe_Values.Look(ref warned, "ds_lowWarned", false);
        }

        private void CheckEmbrace()
        {
            if (MarkerUtility.Susceptibility(pawn) <= 0f || MarkerUtility.TotalFragments(pawn.Map) <= 0)
            {
                lowTicks = 0;
                warned = false;
                return;
            }
            if (CurLevel < EmbraceLevel)
            {
                lowTicks += 150;
            }
            else
            {
                lowTicks = Mathf.Max(0, lowTicks - 50);
                if (lowTicks < EmbraceTicks / 4)
                {
                    warned = false;
                }
            }

            if (!warned && lowTicks >= EmbraceTicks / 2)
            {
                warned = true;
                Messages.Message(pawn.LabelShort + " is slipping away. If their sanity does not recover soon, the Marker will take them.",
                    pawn, MessageTypeDefOf.ThreatSmall, false);
            }

            if (lowTicks >= EmbraceTicks && !pawn.Downed)
            {
                MentalStateDef def = DefDatabase<MentalStateDef>.GetNamedSilentFail("DS_MarkerEmbrace");
                if (def == null) return;
                if (pawn.InMentalState && pawn.MentalStateDef == def) return;
                if (pawn.InMentalState) pawn.mindState.mentalStateHandler.Reset();
                if (pawn.mindState.mentalStateHandler.TryStartMentalState(def, "The Marker has worn their mind away.", false, true))
                {
                    lowTicks = 0;
                    warned = false;
                }
            }
        }

        public Need_Sanity(Pawn pawn) : base(pawn)
        {
            threshPercents = new List<float> { 0.4f, 0.2f, 0.1f };
        }

        public override void SetInitialLevel()
        {
            CurLevel = 1f;
        }

        public override void NeedInterval()
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Map == null)
            {
                return;
            }

            float drain = 0f;
            float susceptible = MarkerUtility.Susceptibility(pawn);
            int stage = MarkerUtility.DreadStage(pawn);
            if (stage >= 0)
            {
                drain += StageDrain[stage];
            }

            int fear = MarkerUtility.FearFragments(pawn.Map);
            if (fear >= 4)
            {
                drain += AmbientDrain * (fear / 4f);
            }

            // The Marker's own drain is scaled by psychic sensitivity (none if deaf, double if hypersensitive); necromorphs still frighten anyone.
            drain *= susceptible;

            bool necro = NecromorphNear();
            if (necro)
            {
                drain += NecromorphDrain;
            }

            // Out of the dread's reach but with a Marker active somewhere: a tiny constant strain, small enough for a bit of
            // recreation or a good sleep to cancel out.
            bool faintOnly = stage < 0 && !necro;
            if (faintOnly && MarkerUtility.TotalFragments(pawn.Map) > 0)
            {
                drain += FaintDrain * susceptible;
            }

            float rise = 0f;
            if (drain > 0f)
            {
                if (!pawn.Awake())
                {
                    drain *= 0.5f;
                }
                if (MarkerUtility.IsSuppressed(pawn))
                {
                    drain *= 0.5f;
                }
                drain *= SuitFactor(pawn);
                CurLevel = Mathf.Max(0f, CurLevel - drain);
            }
            else
            {
                rise = BaseRecovery;
            }

            // Rest and recreation always help, unless the dread is right on them.
            if (faintOnly)
            {
                if (!pawn.Awake())
                {
                    rise += SleepRecovery;
                }
                Need_Joy joy = pawn.needs.TryGetNeed<Need_Joy>();
                if (joy != null && joy.CurLevelPercentage > 0.6f)
                {
                    rise += JoyRecovery;
                }
            }
            // A ventilated room is a calmer place to be.
            if (MapComponent_Air.RoomState(pawn) > 0)
            {
                rise += VentedRecovery;
            }
            HediffDef calm = DefDatabase<HediffDef>.GetNamedSilentFail("DS_Air_Calming");
            if (calm != null && pawn.health.hediffSet.HasHediff(calm))
            {
                rise += 0.0012f;
            }
            if (rise > 0f)
            {
                CurLevel = Mathf.Min(1f, CurLevel + rise);
            }

            UpdateAttunement();
            CheckEmbrace();
            TryBreak();
        }

        // The Marker's gift: a tiny one for every colonist while a Marker is active, and the closer a colonist is the faster they work and research. It scales with the fragments loaded and the
        // Resonance research. The damping research does not reduce it, only the sanity drain, so it is pure gain for the same risk.
        private static readonly float[] StageStrength = { 1.0f, 0.7f, 0.45f, 0.2f };

        // Psychically deaf colonists are not attuned at all. Hypersensitive ones carry a second copy of the attunement, so its gifts are doubled.
        private void UpdateAttunement()
        {
            HediffDef main = DefDatabase<HediffDef>.GetNamedSilentFail("DS_Attuned");
            HediffDef echo = DefDatabase<HediffDef>.GetNamedSilentFail("DS_AttunedEcho");
            float susceptible = MarkerUtility.Susceptibility(pawn);
            if (susceptible <= 0f)
            {
                Hediff h = main == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(main);
                if (h != null) pawn.health.RemoveHediff(h);
                h = echo == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(echo);
                if (h != null) pawn.health.RemoveHediff(h);
                return;
            }

            UpdateAttunementMain();

            if (echo == null) return;
            Hediff m = main == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(main);
            Hediff e = pawn.health.hediffSet.GetFirstHediffOfDef(echo);
            if (susceptible >= 2f && m != null)
            {
                if (e == null) e = pawn.health.AddHediff(echo);
                if (e != null) e.Severity = m.Severity;
            }
            else if (e != null)
            {
                pawn.health.RemoveHediff(e);
            }
        }

        private void UpdateAttunementMain()
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail("DS_Attuned");
            if (def == null)
            {
                return;
            }
            int stage = MarkerUtility.DreadStage(pawn, false);
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (stage < 0)
            {
                // Out of reach of the dread, but not out of reach of the signal: while a Marker is active anywhere on the map
                // every colonist carries the faintest attunement, a tiny gift with no sanity cost.
                if (MarkerUtility.TotalFragments(pawn.Map) <= 0)
                {
                    if (existing != null)
                    {
                        pawn.health.RemoveHediff(existing);
                    }
                    return;
                }
                if (existing == null)
                {
                    existing = pawn.health.AddHediff(def);
                }
                if (existing != null)
                {
                    existing.Severity = 0.1f;
                }
                return;
            }

            float frags = Mathf.Min(MarkerUtility.TotalFragments(pawn.Map), MarkerUtility.FullMarkerFragments);
            float strength = StageStrength[stage] * (0.5f + 0.5f * frags / MarkerUtility.FullMarkerFragments)
                * (1f + 0.1f * MarkerResearch.ResonanceTier());
            strength = Mathf.Clamp(strength, 0.05f, 1f);
            if (existing == null)
            {
                existing = pawn.health.AddHediff(def);
            }
            if (existing != null)
            {
                existing.Severity = strength;
            }
        }

        private bool NecromorphNear()
        {
            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn other = pawns[i];
                if (other == pawn || other.Dead || other.kindDef == null || !other.kindDef.defName.StartsWith("DS_"))
                {
                    continue;
                }
                if ((other.Position - pawn.Position).LengthHorizontal <= NecromorphRange)
                {
                    return true;
                }
            }
            return false;
        }

        // Low sanity breaks the mind: first a short daze, then violence, and at the bottom the Marker's call.
        private void TryBreak()
        {
            if (pawn.InMentalState || pawn.Downed || !pawn.Spawned)
            {
                return;
            }
            float level = CurLevel;
            if (level > MildBreak)
            {
                return;
            }

            string defName = null;
            float chance;
            if (level <= CalledBreak && MarkerUtility.Susceptibility(pawn) > 0f)
            {
                chance = 0.012f;
                defName = "DS_MarkerCalled";
            }
            else if (level <= SevereBreak)
            {
                chance = 0.004f;
                defName = Rand.Value < 0.5f ? "Berserk" : "Wander_Psychotic_Short";
            }
            else
            {
                chance = 0.002f;
                defName = "Wander_Psychotic_Short";
            }

            if (Rand.Value > chance)
            {
                return;
            }
            MentalStateDef def = DefDatabase<MentalStateDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                return;
            }
            if (pawn.mindState.mentalStateHandler.TryStartMentalState(def, "The Marker's signal has broken their mind.", false, true))
            {
                // The break lets the pressure out for a while, so they do not break again at once.
                CurLevel = Mathf.Min(1f, CurLevel + 0.22f);
            }
        }
    }
}
