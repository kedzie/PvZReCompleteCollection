using Il2CppInterop.Runtime;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class SweetPotatoDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public SweetPotatoDefinition(IntPtr pointer) : base(pointer)
    {
        // A pure wall (like Primal Wall-nut): no projectile, no action; the controller only swaps the damage pose.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Sweet Potato", false);
        SetDescription("Sweet Potato is a big, sturdy blocker. He sweats and bruises as zombies chew through him, through four looks from fresh to battered.", false);
        SetTooltip("A tough wall that shows his wear as he takes damage.", false);

        // PvZ2 SweetPotatoDefault: Cost 150, Hitpoints 4000, PacketCooldown 20 (StartingCooldown 15). (PvZ2's version
        // also lures zombies into other lanes; that part is not in this port, he is the wall only.)
        // PacketCooldown 20 -> m_refreshTime 2000 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 150;
        m_versusCost = 150;
        m_health = 4000;

        m_refreshTime = 2000;
        m_versusBaseRefreshTime = 2000;
        m_versusSuddenDeathRefreshTime = 500;

        // PvZ2 family: Reinforce-mint (the walls).
        m_mintFamily = MintFamily.ReinforceMint;

        SetSeedPacketImage(CompleteCollectionMod.SweetPotatoBundleId, "assets/plantrip/sweetpotato/spr_seedpacket_sweetpotato.png");
        SetPreviewImage(CompleteCollectionMod.SweetPotatoBundleId, "assets/plantrip/sweetpotato/spr_seedpacket_sweetpotato.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "SweetPotato_Default",
            AssetBundleId = CompleteCollectionMod.SweetPotatoBundleId,
            SkinPrefabId = "assets/plantrip/sweetpotato/models/default/plant/sweetpotato.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code (a wall; the PAM's damage grunts are left out of the bundle build,
        // PvZBundleBuilder.NoBakedSounds, because they would repeat on every loop of the idle clips).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<SweetPotatoBehaviorController>();
    }
}

// PvZ2's own damage art: "idle" (fresh), "idle_damage", "idle_damage2", "idle_damage3" (the "idle2*" clips are the
// same looks with another fidget; they are left out of the bundle). All loop (the name has "idle" in it). The
// controller switches between them as health drops, in quarters (PvZ2 has four looks; Replanted's own Wall-nut uses
// thirds for its three).
[RegisterTypeInIl2Cpp]
public class SweetPotatoBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private static readonly string[] TierClips = { "idle", "idle_damage", "idle_damage2", "idle_damage3" };

    private int tickCounter;
    private int shownTier = -1;

    #endregion

    #region Constructors

    public SweetPotatoBehaviorController(IntPtr pointer) : base(pointer)
    {
    }

    #endregion

    #region Methods

    private int CurrentTier
    {
        get
        {
            int health = Plant.mPlantHealth;
            int max = Plant.mPlantMaxHealth;
            return health * 4 > max * 3 ? 0 : health * 2 > max ? 1 : health * 4 > max ? 2 : 3;
        }
    }

    // A few checks a second is plenty for a visual-only change.
    public override void PostPlantUpdate()
    {
        base.PostPlantUpdate();

        tickCounter++;
        if (tickCounter % 10 != 0)
        {
            return;
        }

        int tier = CurrentTier;
        if (tier == shownTier)
        {
            return;
        }

        shownTier = tier;
        PlayAnimation(TierClips[tier]);
    }

    public override void Reset()
    {
        base.Reset();

        tickCounter = 0;
        shownTier = -1;
    }

    #endregion
}
