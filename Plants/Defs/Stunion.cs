using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class StunionDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public StunionDefinition(IntPtr pointer) : base(pointer)
    {
        // Behaviour entirely from the controller (trigger, stall), like Primal Potato Mine.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Stunion", false);
        SetDescription("Stunion sits quietly until a zombie gets close. Then he breathes in and lets out a cloud of fumes that leaves the zombies in front of him stuck in place for twelve seconds. They can still be hurt, they just cannot go anywhere. One cloud, then he is spent.", false);
        SetTooltip("Stalls the zombies in front of him for 12 seconds when one gets close, then is spent.", false);

        // PvZ2 StunionDefault: Cost 25, Hitpoints 300, PacketCooldown 20 (StartingCooldown 15), StunDuration 12 seconds,
        // trigger/stun rect x -30..+90 (his own lane), IsInstant/IsConsumable.
        // PacketCooldown 20 -> m_refreshTime 2000 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 25;
        m_versusCost = 25;
        m_health = 300;

        m_refreshTime = 2000;
        m_versusBaseRefreshTime = 2000;
        m_versusSuddenDeathRefreshTime = 500;

        // PvZ2 lists the Contain-mint family, but the plant is gone about three seconds after it goes off, so there
        // is nothing for a mint to buff (m_mintFamily stays None, like Stallia).

        SetSeedPacketImage(CompleteCollectionMod.StunionBundleId, "assets/plantrip/stunion/spr_seedpacket_stunion.png");
        SetPreviewImage(CompleteCollectionMod.StunionBundleId, "assets/plantrip/stunion/spr_seedpacket_stunion.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Stunion_Default",
            AssetBundleId = CompleteCollectionMod.StunionBundleId,
            SkinPrefabId = "assets/plantrip/stunion/models/default/plant/stunion.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the inhale, burp and poof sounds are PlaySound Animation Events
        // baked into the "attack" clip from the PAM (PvZRipTools plant_sounds.py), and the cloud is part of that clip's art.

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<StunionBehaviorController>();
    }
}

// Clips (30 fps): "idle" (loops, the Animator default) -> [a zombie gets close] "attack" (95 frames: inhales, burps the
// cloud on the PAM's use_special marker at +30 (the bundle build bakes an ExecuteAttack event there), poofs out near
// the end, where an OwnerDie event removes the spent plant). Everything is counted in game ticks, so it follows the
// game speed.
[RegisterTypeInIl2Cpp]
public class StunionBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    // PvZ2 StunionDefault StunDuration 12 s (Replanted runs ~100 ticks a second).
    private const int StallTicks = 12 * 100;

    // PvZ2 trigger and stun rect x -30..+90 around the plant, in his own lane: a zombie whose body overlaps this strip
    // starts the burp and is stunned by it.
    private const float ReachLeft = 10f;
    private const float ReachRight = 45f;

    private const int CheckInterval = 5;

    private int ticks;
    private bool triggered;
    private bool stalled;

    #endregion

    #region Constructors

    public StunionBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (triggered || !IsSkinApplied)
        {
            return;
        }

        ticks++;
        if (ticks % CheckInterval != 0)
        {
            return;
        }

        if (ZombiesInReach().Count > 0)
        {
            triggered = true;
            PlayAnimation("attack");
        }
    }

    // ExecuteAttack event on the burp frame: stall everything in the strip.
    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        if (stalled)
        {
            return;
        }

        stalled = true;

        // "Stall" = the game's own butter immobilisation (what Kernel-pult's butter does; same as Stallia): the zombie
        // neither walks nor chews while Zombie.mButteredCounter runs, and may still be damaged. ApplyButter() is the
        // native entry point (it skips what butter never works on); it sets 4 s, stretched here to the 12 s.
        foreach (var zombie in ZombiesInReach())
        {
            zombie.ApplyButter();
            if (zombie.mButteredCounter > 0)
            {
                zombie.mButteredCounter = Math.Max(zombie.mButteredCounter, StallTicks);
            }
        }
    }

    private List<Zombie> ZombiesInReach()
    {
        var result = new List<Zombie>();
        if (Board?.m_zombies?.m_list == null)
        {
            return result;
        }

        var plantRect = Plant.GetPlantRect();
        float left = plantRect.x - ReachLeft;
        float right = plantRect.x + plantRect.width + ReachRight;

        foreach (var entry in ZombieSnapshot.Items(Board))
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
                result.Add(zombie);
            }
        }

        return result;
    }

    public override void Reset()
    {
        base.Reset();

        ticks = 0;
        triggered = false;
        stalled = false;
    }

    #endregion
}
