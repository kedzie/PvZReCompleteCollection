using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection2.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class NightshadeDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public NightshadeDefinition(IntPtr pointer) : base(pointer)
    {
        // Behaviour entirely from the controller (reach test, slap), like Spring Bean.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Nightshade", false);
        SetDescription($"Nightshade lashes out with a whirl of petals at the zombies right in front of it - a heavy blow ({NightshadeBehaviorController.SlapDamage} damage) that hurts everything in reach. It does it again and again for as long as it lives.", false);
        SetTooltip($"Slaps the zombies in front of it for {NightshadeBehaviorController.SlapDamage} damage, again and again.", false);

        // PvZ2 NightshadeDefault: Cost 75, Hitpoints 300, PacketCooldown 5. Its melee is the "special" action, Damage 300, rect
        // trigger {mX -25, mY -30, mWidth 120, mHeight 130} (the properties' MeleeDamage 200 is a plant stat, the action's own
        // Damage is what is used here). PvZ2's ranged petals only fire under a Moon Flower's power, which Replanted does not have.
        // PacketCooldown 5 -> m_refreshTime 500 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 75;
        m_versusCost = 75;
        m_health = 300;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        // PvZ2 family: Conceal-mint.
        m_mintFamily = MintFamily.ConcealMint;

        SetSeedPacketImage(CompleteCollectionMod.NightshadeBundleId, "assets/plantrip/nightshade/spr_seedpacket_nightshade.png");
        SetPreviewImage(CompleteCollectionMod.NightshadeBundleId, "assets/plantrip/nightshade/spr_seedpacket_nightshade.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Nightshade_Default",
            AssetBundleId = CompleteCollectionMod.NightshadeBundleId,
            SkinPrefabId = "assets/plantrip/nightshade/models/default/plant/nightshade.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the slap sounds are PlaySound Animation Events baked into the "attack"
        // clip from the PAM (PvZRipTools plant_sounds.py).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<NightshadeBehaviorController>();
    }
}

// The PAM's own clips, unchanged: "idle" (loops; the Animator default; "idle2" and "water" are in the bundle too, unused) and
// "attack" (95 frames, one use_action marker at +40, the petals landing; within the clip the petals spin off and grow back).
// The PAM has no lost-petal / damage art, so the plant looks the same after every slap.
//
// PvZ2 data: the melee "special" action, Damage 300, trigger rect {mX -25, mY -30, mWidth 120, mHeight 130} around the plant's
// origin (its centre): used here as [centre - 25, centre + 95] of the plant's own lane. The slap starts when a zombie is in
// that reach and hits everything in it when the marker's frame comes. PvZ2's LeafRespawnCooldown 5 s is the regrowing of the
// petals, which is what the rest of the 3.2 s clip shows; the next slap may start once the clip is over. Counted in game ticks,
// so it follows the game speed.
[RegisterTypeInIl2Cpp]
public class NightshadeBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    public const int SlapDamage = 300;

    private const float ReachLeft = 25f;
    private const float ReachRight = 95f;

    // "attack" is 95 frames at 30 fps = 317 ticks; the next slap may start after that.
    private const int AttackClipTicks = 317 + 10;
    private const int CheckInterval = 5;

    private int ticksSinceStart = AttackClipTicks;

    // A plant never moves: its reach is worked out once, and the skin check stops once it has passed.
    private bool skinApplied;
    private bool reachKnown;
    private float reachLeft;
    private float reachRight;

    #endregion

    #region Constructors

    public NightshadeBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    // Many Nightshades on a lawn must stay cheap: until the next slap is due there is nothing to look at, the
    // zombie scan is the board's shared per-frame ZombieSnapshot (read once however many plants ask), and the
    // plant's own native calls are made once, not every tick.
    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        if (ticksSinceStart < AttackClipTicks)
        {
            ticksSinceStart++;
            return;
        }

        if (!skinApplied)
        {
            if (!IsSkinApplied)
            {
                return;
            }

            skinApplied = true;
        }

        ticksSinceStart++;
        if (ticksSinceStart % CheckInterval != 0)
        {
            return;
        }

        if (HasZombieInReach())
        {
            ticksSinceStart = 0;
            PlayAnimation("attack");
        }
    }

    // ExecuteAttack event on the clip's use_action frame.
    public override void OnAnimationAttackEvent()
    {
        base.OnAnimationAttackEvent();

        if (ticksSinceStart >= AttackClipTicks)
        {
            return;
        }

        foreach (var zombie in ZombiesInReach())
        {
            if (!zombie.IsDeadOrDying())
            {
                zombie.TakeDamage(SlapDamage, DamageFlags.HitsShieldAndBody);
            }
        }
    }

    private void EnsureReach()
    {
        if (reachKnown)
        {
            return;
        }

        var plantRect = Plant.GetPlantRect();
        float centerX = plantRect.x + plantRect.width / 2f;
        reachLeft = centerX - ReachLeft;
        reachRight = centerX + ReachRight;
        reachKnown = true;
    }

    private bool HasZombieInReach()
    {
        if (Board == null)
        {
            return false;
        }

        EnsureReach();
        int row = Plant.mRow;
        foreach (var entry in ZombieSnapshot.Get(Board))
        {
            if (entry.Targetable && entry.Row == row && entry.Left < reachRight && entry.Right > reachLeft)
            {
                return true;
            }
        }

        return false;
    }

    private List<Zombie> ZombiesInReach()
    {
        var result = new List<Zombie>();
        if (Board == null)
        {
            return result;
        }

        EnsureReach();
        int row = Plant.mRow;
        foreach (var entry in ZombieSnapshot.Get(Board))
        {
            if (entry.Targetable && entry.Row == row && entry.Left < reachRight && entry.Right > reachLeft)
            {
                result.Add(entry.Zombie);
            }
        }

        return result;
    }

    public override void Reset()
    {
        base.Reset();

        ticksSinceStart = AttackClipTicks;
        skinApplied = false;
        reachKnown = false;
    }

    #endregion
}
