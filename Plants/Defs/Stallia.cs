using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using UnityEngine;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class StalliaDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public StalliaDefinition(IntPtr pointer) : base(pointer)
    {
        // Instant plant, same shape as Gold Bloom: the controller starts the clip, an Animation Event
        // on the clip does the action, another one removes the plant.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Stallia", false);
        SetDescription("Stallia puffs out a cloud of purple spores that leaves every zombie in a 3x3 area stuck in place for twelve seconds. They can still be hurt, they just cannot go anywhere.", false);
        SetTooltip("Stalls every zombie in a 3x3 area for 12 seconds.", false);

        // PvZ2 StalliaDefault: Cost 0 (free), Hitpoints 300, PacketCooldown 20, IsInstant/IsConsumable.
        // PacketCooldown 20 -> m_refreshTime 2000 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 0;
        m_versusCost = 0;
        m_health = 300;

        m_refreshTime = 2000;
        m_versusBaseRefreshTime = 2000;
        m_versusSuddenDeathRefreshTime = 500;

        // PvZ2 lists the Containmint family, but the plant is gone within about two seconds, so
        // there is nothing for a mint to buff (m_mintFamily stays None, like Gold Bloom).

        SetSeedPacketImage(CompleteCollectionMod.StalliaBundleId, "assets/plantrip/stallia/spr_seedpacket_stallia.png");
        SetPreviewImage(CompleteCollectionMod.StalliaBundleId, "assets/plantrip/stallia/spr_seedpacket_stallia.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Stallia_Default",
            AssetBundleId = CompleteCollectionMod.StalliaBundleId,
            SkinPrefabId = "assets/plantrip/stallia/models/default/plant/stallia.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the puff sound is a PlaySound Animation Event
        // baked into the "attack" clip from the PAM (see PvZRipTools plant_sounds.py), and the
        // spore cloud is part of that clip's art.

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<StalliaBehaviorController>();
    }
}

// The "attack" clip (30 fps, 48 frames) has the PlaySound event on +1, a PAM "freeze" marker on +12 (the
// moment the stall takes hold; the bundle build bakes an ExecuteAttack event there, see
// PvZBundleBuilder.ExtraLaunchCommands) and an OwnerDie event near its end that removes the plant.
[RegisterTypeInIl2Cpp]
public class StalliaBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // PvZ2 StalliaDefault: StallDuration 12 (seconds). Replanted runs ~100 ticks a second.
    private const int StallTicks = 12 * 100;

    // "3by3" (PvZ2 stat): the plant's own lane and the one either side, one and a half tiles to each
    // side of the plant's centre (approximate lawn column width 80, like Tumbleweed and ZombiePushBack).
    private const float StallRadius = 1.5f * 80f;

    // PvZ2 StalliaDefault also has "Knockback": 0, so, unlike a bowling-style push, nothing is pushed back.

    private const int LastColumnIndex = 8;
    private const float RightEdgeSlack = 120f;

    // Let the skin finish applying before asking it to play a clip.
    private const float StartDelaySeconds = 0.1f;

    // Safety net only: if the OwnerDie event never fires, remove the plant anyway.
    private const float GiveUpSeconds = 12f;

    private float elapsed;
    private bool started;
    private bool stalled;

    #endregion

    #region Constructors

    public StalliaBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void Update()
    {
        base.Update();

        elapsed += Time.deltaTime;

        if (!started && elapsed >= StartDelaySeconds && IsSkinApplied)
        {
            started = true;
            PlayAnimation("attack");
        }

        if (elapsed >= GiveUpSeconds)
        {
            elapsed = float.MinValue;
            Plant.Die();
        }
    }

    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        if (stalled)
        {
            return;
        }

        stalled = true;
        StallZombies();
    }

    // "Stall" = the game's own butter immobilisation (what Kernel-pult's butter does): the zombie neither
    // walks nor chews while Zombie.mButteredCounter runs, and may still be damaged. ApplyButter() is the
    // native entry point (it also skips what butter never works on - Zomboni, boss, zombies in
    // states that cannot be held); it sets the counter to 4 s, so once it has taken hold the counter is
    // stretched to the 12 s of PvZ2's StallDuration.
    private void StallZombies()
    {
        var plantRect = Plant.GetPlantRect();
        float centerX = plantRect.x + plantRect.width / 2f;

        foreach (var entry in Board.m_zombies.m_list.ToList())
        {
            var zombie = entry.mItem;
            if (zombie == null
                || zombie.IsDeadOrDying()
                || zombie.mMindControlled
                || zombie.mZombieType == ZombieType.Gravestone)
            {
                continue;
            }

            if (Math.Abs(zombie.mRow - Plant.mRow) > 1
                || zombie.mPosX > Board.GridToPixelX(LastColumnIndex, zombie.mRow) + RightEdgeSlack)
            {
                continue;
            }

            var zombieRect = zombie.GetZombieRect();
            float zombieCenterX = zombieRect.x + zombieRect.width / 2f;
            if (Mathf.Abs(zombieCenterX - centerX) > StallRadius)
            {
                continue;
            }

            zombie.ApplyButter();
            if (zombie.mButteredCounter > 0)
            {
                zombie.mButteredCounter = Math.Max(zombie.mButteredCounter, StallTicks);
            }
        }
    }

    public override void Reset()
    {
        base.Reset();

        elapsed = 0f;
        started = false;
        stalled = false;
    }

    #endregion
}
