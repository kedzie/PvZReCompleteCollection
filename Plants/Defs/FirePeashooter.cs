using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class FirePeashooterDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public FirePeashooterDefinition(IntPtr pointer) : base(pointer)
    {
        m_subClass = PlantSubClass.Shooter;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Fire Peashooter", false);
        SetDescription("Fire Peashooters are immune to frost and shoot flaming peas down the lane.", false);
        SetTooltip("Fire Peashooters are immune to frost and shoot flaming peas down the lane.", false);

        // Cost/hitpoints/recharge tier pulled directly from the real
        // FirePeashooterDefault@PlantProperties entry (Cost 175, PacketCooldown 5 -
        // same tier as Bloomerang/Bonkchoy, which both use 500/500/125 below).
        m_seedCost = 175;
        m_versusCost = 175;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        // Real game's basic-attack cooldown is 1.35-1.5s; Bloomerang's real 2.85-3s
        // maps to m_launchRate 300, i.e. ~100 ticks/sec, so ~140 here.
        m_launchRate = 140;

        SetSeedPacketImage(CompleteCollectionMod.FirepeashooterBundleId, "assets/plantrip/firepeashooter/spr_seedpacket_firepeashooter.png");
        SetPreviewImage(CompleteCollectionMod.FirepeashooterBundleId, "assets/plantrip/firepeashooter/spr_seedpacket_firepeashooter.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "FirePeashooter_Default",
            AssetBundleId = CompleteCollectionMod.FirepeashooterBundleId,
            SkinPrefabId = "assets/plantrip/firepeashooter/models/default/plant/firepeashooter.prefab",
            // 1.35 was copied from Bloomerang's own tuned value without
            // justification - our compositor's FirePeashooter crop runs
            // noticeably bigger than Bloomerang's (partly the flame overlay
            // inflating the bbox), so it visibly oversized the plant. Testing 1.0.
            ScaleOverride = new Vector3(1.0f, 1.0f, 1)
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<FirePeashooterBehaviorController>();
    }
}

[RegisterTypeInIl2Cpp]
public class FirePeashooterBehaviorController : CustomPlantBehaviorController
{
    #region Constructors

    public FirePeashooterBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void OnLaunchCounterTriggered()
    {
        base.OnLaunchCounterTriggered();

        var zombies = Board.m_zombies.m_list.ToList();
        zombies.RemoveAll(z => z.mItem == null);
        zombies.RemoveAll(z => z.mItem.IsDeadOrDying());
        zombies.RemoveAll(z => z.mItem.mRow != Plant.mRow && z.mItem.mZombieType != ZombieType.Boss);
        zombies.RemoveAll(z => !z.mItem.EffectedByDamage(DamageRangeFlags.Ground));
        zombies.RemoveAll(z => z.mItem.mPosX <= Plant.mX);

        if (!zombies.Any())
        {
            return;
        }

        // Spawn a real vanilla PeashooterPea, then flip it to PeashooterFireball via
        // the game's own Torchwood-conversion method - gives the authentic classic
        // fireball behavior/damage/visuals without a custom projectile definition.
        // ConvertToFireball(aGridX) no-ops if aGridX == mHitTorchwoodGridX (the
        // "already converted by this exact torchwood cell" guard) - and
        // ProjectileInitialize sets a fresh projectile's mHitTorchwoodGridX to -1 as
        // its sentinel default, so passing -1 here matched immediately and silently
        // skipped conversion every time. Real Torchwood passes its own grid column,
        // which is never -1, so it always converts; use Plant.mPlantCol here too.
        var m_currentTarget = zombies.First().mItem;
        var projectile = SpawnProjectile(ProjectileType.PeashooterPea);
        projectile.mTargetZombieID = m_currentTarget.mRelatedZombieID;
        // The real Plant.Fire() always sets this right after spawning (via
        // Plant.GetDamageRangeFlags()) - native CheckForCollision() checks
        // Zombie.EffectedByDamage(mDamageRangeFlags) before ever calling DoImpact,
        // and PvZReCoreLib's generic SpawnProjectile() leaves it unset (defaults to
        // none), so the pea flies through every zombie without ever damaging one.
        // Bloomerang never hits this because its custom PreDoImpact bypasses native
        // collision entirely and checks EffectedByDamage(DamageRangeFlags.Ground)
        // itself before calling DamageZombie() directly.
        projectile.mDamageRangeFlags = DamageRangeFlags.Ground;
        projectile.ConvertToFireball(Plant.mPlantCol);

        PlayAnimation("attack");
    }

    #endregion
}
