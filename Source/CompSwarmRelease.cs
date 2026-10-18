using RimWorld;
using Verse;
using Verse.AI.Group;

namespace DeadShip
{
    public class CompProperties_SwarmRelease : CompProperties
    {
        public string swarmKind = "DS_Swarmer";
        public int countMin = 5;
        public int countMax = 7;

        public CompProperties_SwarmRelease()
        {
            compClass = typeof(CompSwarmRelease);
        }
    }

    // When the creature dies, however it dies, a swarm bursts out and attacks.
    public class CompSwarmRelease : ThingComp
    {
        private bool released;

        public CompProperties_SwarmRelease Props
        {
            get { return (CompProperties_SwarmRelease)props; }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref released, "ds_swarmReleased", false);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            base.Notify_Killed(prevMap, dinfo);
            if (released || prevMap == null)
            {
                return;
            }
            released = true;

            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(Props.swarmKind);
            Faction faction = MarkerUtility.NecromorphFaction();
            if (kind == null || faction == null)
            {
                return;
            }

            int count = Rand.RangeInclusive(Props.countMin, Props.countMax);
            System.Collections.Generic.List<Pawn> swarm = new System.Collections.Generic.List<Pawn>();
            for (int i = 0; i < count; i++)
            {
                IntVec3 spot = CellFinder.StandableCellNear(parent.Position, prevMap, 2.5f);
                if (!spot.IsValid)
                {
                    spot = parent.Position;
                }
                Pawn swarmer = PawnGenerator.GeneratePawn(kind, faction);
                GenSpawn.Spawn(swarmer, spot, prevMap);
                swarm.Add(swarmer);
            }

            if (swarm.Count > 0)
            {
                LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, false, false, false, false, false, false, false), prevMap, swarm);
                FleckMaker.ThrowDustPuff(parent.Position, prevMap, 2f);
            }
        }
    }
}
