#if CE
using CombatExtended;
using UnityEngine;
using Verse;

namespace DeadShip
{
    // Fires one Combat Extended projectile from a creature at a target. This is the only file that touches
    // CE's own types, and it is only compiled when build.bat finds CombatExtended.dll, and only called
    // while CE is running, so the mod still loads without CE.
    internal static class CELauncher
    {
        public static bool TryLaunch(Pawn shooter, Pawn target, ThingDef projectileDef, float spreadDegrees)
        {
            if (shooter == null || target == null || shooter.Map == null || projectileDef == null)
            {
                return false;
            }

            ProjectileCE projectile = ThingMaker.MakeThing(projectileDef, null) as ProjectileCE;
            if (projectile == null)
            {
                return false;
            }
            ProjectilePropertiesCE props = projectileDef.projectile as ProjectilePropertiesCE;
            if (props == null)
            {
                projectile.Destroy();
                return false;
            }

            // Muzzle and target positions with heights, the way CE's own shooting works out its aim.
            float shotHeight = new CollisionVertical(shooter).shotHeight;
            float targetHeight = new CollisionVertical(target).MiddleHeight;
            Vector3 source = shooter.DrawPos;
            source.y = shotHeight;
            Vector3 targetPos = target.DrawPos;
            targetPos.y = targetHeight;

            float speed = props.speed;
            float shotAngle = props.TrajectoryWorker.ShotAngle(props, source, targetPos, speed);
            float shotRotation = props.TrajectoryWorker.ShotRotation(props, source, targetPos);

            // Aim error: this is what makes it miss sometimes. CE's collision and cover do the rest.
            shotRotation += Rand.Gaussian(0f, spreadDegrees);
            shotAngle += Rand.Gaussian(0f, spreadDegrees * Mathf.Deg2Rad * 0.5f);

            GenSpawn.Spawn(projectile, shooter.Position, shooter.Map);
            projectile.canTargetSelf = false;
            projectile.minCollisionDistance = 1f;
            projectile.intendedTarget = target;

            Vector2 origin = new Vector2(shooter.DrawPos.x, shooter.DrawPos.z);
            float distance = (new Vector2(target.DrawPos.x, target.DrawPos.z) - origin).magnitude;
            projectile.Launch(shooter, origin, shotAngle, shotRotation, shotHeight, speed, null, distance);
            return true;
        }
    }
}
#endif
