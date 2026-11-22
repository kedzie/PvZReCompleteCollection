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
public class ChillyPepperDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public ChillyPepperDefinition(IntPtr pointer) : base(pointer)
    {
        // Instant plant: no launch counter, the controller drives everything from its own Update
        // and from the Animation Event baked into its "attack" clip (same shape as Gold Bloom).
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Chilly Pepper", false);
        SetDescription("Chilly Pepper lets out a freezing blast down its whole lane, chilling every zombie in it and hurting the ones close by. The lanes next to it get a chill too.", false);
        SetTooltip("Chills every zombie in its lane (and the next lanes), and damages the nearby ones.", false);

        // PvZ2 ChillyPepperDefault: Cost 125, Hitpoints 300, PacketCooldown 35, IsInstant/IsConsumable.
        // Replanted's Jalapeno costs 125 as well. PacketCooldown 35 -> m_refreshTime 3500
        // (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 125;
        m_versusCost = 125;
        m_health = 300;

        m_refreshTime = 3500;
        m_versusBaseRefreshTime = 3500;
        m_versusSuddenDeathRefreshTime = 875;

        // PvZ2 lists the Wintermint family, but the plant is gone within about two seconds, so
        // there is nothing for a mint to buff (m_mintFamily stays None, like Gold Bloom).

        SetSeedPacketImage(CompleteCollectionMod.ChillyPepperBundleId, "assets/plantrip/chillypepper/spr_seedpacket_chillypepper.png");
        SetPreviewImage(CompleteCollectionMod.ChillyPepperBundleId, "assets/plantrip/chillypepper/spr_seedpacket_chillypepper.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "ChillyPepper_Default",
            AssetBundleId = CompleteCollectionMod.ChillyPepperBundleId,
            SkinPrefabId = "assets/plantrip/chillypepper/models/default/plant/chillypepper.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the blast sound is a PlaySound Animation Event
        // baked into the "attack" clip from the PAM (see PvZRipTools plant_sounds.py). The frost
        // burst ("CHILOOSH!") is part of that clip's art, so no extra particle effect is needed.

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<ChillyPepperBehaviorController>();
    }
}

// The "attack" clip (30 fps, 59 frames) has the PlaySound event on its first frame, one PAM
// use_action marker on frame +25 (the blast; the bundle build bakes an ExecuteAttack event there) and
// an OwnerDie event near its end that removes the plant (AnimationScripts.OwnerDie -> Plant.Die).
// The controller only has to start the clip and do the chilling on the ExecuteAttack event.
[RegisterTypeInIl2Cpp]
public class ChillyPepperBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // Approximate width of one lawn column (the same guess Tumbleweed and ZombiePushBack use).
    private const float TileWidth = 80f;

    // PvZ2 ChillyPepperDefault, first Action ("radius_tiered_row"): ExplodeRadius 5.0736 (squared
    // squares, like every PvZ2 radius: 2.0736 = 1.44 tiles) = 2.25 tiles, Damage 200 in the plant's own
    // lane, SplashDamage 100 in the lanes next to it.
    private const float DamageRadius = 2.25f * TileWidth;
    private const int LaneDamage = 200;
    private const int AdjacentLaneDamage = 100;

    // The other two Actions ("row" and "row_adjacent": Damage 0, FreezeDuration/ChillDuration 1) apply
    // to the WHOLE lane and the two next to it, no radius. Replanted's own chill (Zombie.ApplyChill(false):
    // slow for 10 s) stands in for PvZ2's freeze/chill; DamageFlags.Freeze makes the damage chill too.
    private const DamageFlags ChillDamageFlags = DamageFlags.Freeze;

    // Zombies are only chilled once they are on the lawn: ignore anything further right than the last
    // column plus this slack (zombies spawn off-screen to the right).
    private const int LastColumnIndex = 8;
    private const float RightEdgeSlack = 120f;

    // Let the skin finish applying before asking it to play a clip.
    private const float StartDelaySeconds = 0.1f;

    // Safety net only: if the OwnerDie event never fires, remove the plant anyway. The real
    // clip is about 2 s at normal speed.
    private const float GiveUpSeconds = 12f;

    private float elapsed;
    private bool started;
    private bool blasted;

    #endregion

    #region Constructors

    public ChillyPepperBehaviorController(IntPtr pointer) : base(pointer)
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

        if (blasted)
        {
            return;
        }

        blasted = true;
        ChillLanes();
    }

    private void ChillLanes()
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

            int laneDistance = Math.Abs(zombie.mRow - Plant.mRow);
            if (laneDistance > 1 || zombie.mPosX > Board.GridToPixelX(LastColumnIndex, zombie.mRow) + RightEdgeSlack)
            {
                continue;
            }

            // The whole lane (and the next two) gets the chill ...
            zombie.ApplyChill(false);

            // ... and the ones within the blast radius also take the damage.
            var zombieRect = zombie.GetZombieRect();
            float zombieCenterX = zombieRect.x + zombieRect.width / 2f;
            if (Mathf.Abs(zombieCenterX - centerX) <= DamageRadius && zombie.EffectedByDamage(DamageRangeFlags.Ground))
            {
                zombie.TakeDamage(laneDistance == 0 ? LaneDamage : AdjacentLaneDamage, ChillDamageFlags);
            }
        }
    }

    public override void Reset()
    {
        base.Reset();

        elapsed = 0f;
        started = false;
        blasted = false;
    }

    #endregion
}
