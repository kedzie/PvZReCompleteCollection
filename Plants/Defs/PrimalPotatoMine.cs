using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using PvZReCoreLib.Util;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class PrimalPotatoMineDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    // PvZ2's PRIMAL_POTATOMINE_EXPLOSION (the "SPUD OW!" burst), ripped into the plant's own bundle as an
    // animated prefab (PvZRipTools plants.json "effects"). Stays null on an older bundle (no burst, no error).
    public static GameObject ExplosionPrefab;
    private const string ExplosionPrefabPath = "assets/plantrip/primal_potatomine/models/default/effects/explosion/explosion.prefab";

    public PrimalPotatoMineDefinition(IntPtr pointer) : base(pointer)
    {
        // Behaviour entirely from the controller (arming timer, trigger test, blast), like Explode-o-nut / Stallia.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Primal Potato Mine", false);
        SetDescription("Primal Potato Mine digs in and arms itself in five seconds - much quicker than a plain Potato Mine. When a zombie steps on it, it blows up with a far bigger bang, taking out everything around it.", false);
        SetTooltip("A fast-arming mine with a large blast radius.", false);

        // PvZ2 PrimalPotatomineDefault: Cost 50, Hitpoints 300, PacketCooldown 25, TimeUntilArmed 5, blast radius
        // 1.6 tiles for 2400 damage. PacketCooldown 25 -> m_refreshTime 2500 (~100 ticks/sec, same mapping as
        // every other port here).
        m_seedCost = 50;
        m_versusCost = 50;
        m_health = 300;

        m_refreshTime = 2500;
        m_versusBaseRefreshTime = 2500;
        m_versusSuddenDeathRefreshTime = 625;

        // PvZ2 family: Bombard-mint (the family of Replanted's own Cherry Bomb and Potato Mine).
        m_mintFamily = MintFamily.BombardMint;

        SetSeedPacketImage(CompleteCollectionMod.PrimalPotatoMineBundleId, "assets/plantrip/primal_potatomine/spr_seedpacket_primal_potatomine.png");
        SetPreviewImage(CompleteCollectionMod.PrimalPotatoMineBundleId, "assets/plantrip/primal_potatomine/spr_seedpacket_primal_potatomine.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "PrimalPotatoMine_Default",
            AssetBundleId = CompleteCollectionMod.PrimalPotatoMineBundleId,
            SkinPrefabId = "assets/plantrip/primal_potatomine/models/default/plant/primal_potatomine.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the emerge and the blast sounds are PlaySound Animation
        // Events baked into the "recover" and "attack" clips from the PAM (see PvZRipTools plant_sounds.py).

        LoadExplosionPrefab();

        PlantSeedType = m_seedType;
    }

    private static void LoadExplosionPrefab()
    {
        try
        {
            RegistryBridge.LoadAssetFromAssetBundle<GameObject>(
                CompleteCollectionMod.PrimalPotatoMineBundleId, ExplosionPrefabPath, prefab => ExplosionPrefab = prefab);
        }
        catch (Exception e)
        {
            MelonLogger.Warning($"[PrimalPotatoMine] explosion prefab not loaded, the blast has no burst art: {e.Message}");
        }
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PrimalPotatoMineBehaviorController>();
    }
}

// Clips (30 fps): "plant" (29 frames, digs in; the Animator's default state, so it starts by itself) -> "plant_idle"
// (2 frames, loops: only the spike shows) -> "recover" (25 frames, emerges; PlaySound on +1) -> "idle" (loops).
// "attack" (20 frames) is the blast: PlaySound events on +1 (silent in PvZ2's data) and +17 (the blast sound),
// and the bundle build bakes an ExecuteAttack event on that same +17 frame (PvZBundleBuilder.ExtraLaunchSamples)
// plus an OwnerDie event near the end that removes the spent mine. Ticks, not seconds, drive the arming and the
// trigger test, so both follow the game speed.
[RegisterTypeInIl2Cpp]
public class PrimalPotatoMineBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // PvZ2 TimeUntilArmed 5 seconds (Replanted runs ~100 ticks a second).
    private const int ArmTicks = 5 * 100;

    // Approximate width of one lawn column (the same guess Tumbleweed, Stallia and ZombiePushBack use).
    private const float TileWidth = 80f;

    // PvZ2 PrimalPotatoMineDamageLvl1 (POWERS): DamageRadius 1.6 grid squares, BaseDamage 2400
    // (hits_shield_and_body), zombies in other lanes are NOT ignored (the plain Potato Mine's list has
    // "not_in_row", this one does not), so the blast is a circle that reaches the lanes next to it.
    private const float BlastRadius = 1.6f * TileWidth;
    private const int BlastDamage = 2400;

    // PvZ2 trigger rect: -30..+35 around the plant's origin (65 wide), i.e. about the tile the mine sits on.
    private const float TriggerPadding = 10f;

    // The trigger test only needs a few checks a second.
    private const int TriggerCheckInterval = 5;

    // Explosion art placement: this effect's anchor sits (-6, +263) PAM pixels from the plant's anchor (worked out
    // from the two manifest.json files; see PlantEffectSpawner). Its clip is 35 frames = 1.17 s at 30 fps.
    private const float ExplosionDx = -6f;
    private const float ExplosionDyDown = 263f;
    private const float ExplosionLifetimeSeconds = 1.3f;

    private int ticks;
    private bool armed;
    private bool triggered;
    private bool blasted;

    #endregion

    #region Constructors

    public PrimalPotatoMineBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (triggered)
        {
            return;
        }

        ticks++;

        if (!armed)
        {
            if (ticks >= ArmTicks && IsSkinApplied)
            {
                armed = true;
                PlayAnimation("recover");
            }

            return;
        }

        if (ticks % TriggerCheckInterval != 0)
        {
            return;
        }

        if (FindTriggeringZombie() != null)
        {
            triggered = true;
            PlayAnimation("attack");
        }
    }

    // Any live, ground zombie of the mine's own lane standing on (or just about to chew) its tile.
    private Zombie FindTriggeringZombie()
    {
        var plantRect = Plant.GetPlantRect();
        float left = plantRect.x - TriggerPadding;
        float right = plantRect.x + plantRect.width + TriggerPadding;

        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null
                || zombie.IsDeadOrDying()
                || zombie.mMindControlled
                || zombie.mRow != Plant.mRow
                || zombie.mZombieType == ZombieType.Gravestone
                || !zombie.EffectedByDamage(DamageRangeFlags.Ground))
            {
                continue;
            }

            var zombieRect = zombie.GetZombieRect();
            if (zombieRect.x < right && zombieRect.x + zombieRect.width > left)
            {
                return zombie;
            }
        }

        return null;
    }

    // ExecuteAttack event on the frame of the blast sound.
    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        if (blasted)
        {
            return;
        }

        blasted = true;
        Explode();
    }

    private void Explode()
    {
        var plantRect = Plant.GetPlantRect();
        float centerX = plantRect.x + plantRect.width / 2f;
        float centerY = plantRect.y + plantRect.height / 2f;

        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null
                || zombie.IsDeadOrDying()
                || zombie.mMindControlled
                || zombie.mZombieType == ZombieType.Gravestone
                || Math.Abs(zombie.mRow - Plant.mRow) > 1
                || !zombie.EffectedByDamage(DamageRangeFlags.Ground))
            {
                continue;
            }

            // Circle against the zombie's rect (nearest point), like the native radius kills.
            var zombieRect = zombie.GetZombieRect();
            float nearestX = Mathf.Clamp(centerX, zombieRect.x, zombieRect.x + zombieRect.width);
            float nearestY = Mathf.Clamp(centerY, zombieRect.y, zombieRect.y + zombieRect.height);
            float dx = nearestX - centerX;
            float dy = nearestY - centerY;
            if (dx * dx + dy * dy > BlastRadius * BlastRadius)
            {
                continue;
            }

            zombie.TakeDamage(BlastDamage, DamageFlags.HitsShieldAndBody);
        }

        Board.ShakeBoard(3, -4);
        SpawnExplosionArt();
    }

    private void SpawnExplosionArt()
    {
        int sortingOrder = Il2CppReloaded.Utils.SortingUtils.RemapSortingLayers(
            Board.MakeRenderOrder(RenderLayer.Top, Plant.mRow, 0));
        PlantEffectSpawner.SpawnAtPlant(PrimalPotatoMineDefinition.ExplosionPrefab, Plant,
            ExplosionDx, ExplosionDyDown, sortingOrder, ExplosionLifetimeSeconds);
    }

    public override void Reset()
    {
        base.Reset();

        ticks = 0;
        armed = false;
        triggered = false;
        blasted = false;
    }

    #endregion
}
