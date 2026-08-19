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
public class PrimalWallnutDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public PrimalWallnutDefinition(IntPtr pointer) : base(pointer)
    {
        // A pure wall: no projectile, no action. Plant.Update still runs for every plant, and
        // that is all the behaviour controller below needs (it only swaps the damage pose).
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Primal Wall-nut", false);
        SetDescription("Primal Wall-nuts are cheap, sturdy blockers. They crack and chip as zombies chew through them.", false);
        SetTooltip("Primal Wall-nut is a cheap wall that blocks zombies and shows its wear as it takes damage.", false);

        // PvZ2 PrimalWallnutDefault: Cost 75, Hitpoints 4000, PacketCooldown 5.
        // Replanted's own Wall-nut has the same 4000 HP, so the health carries over 1:1. Cost 75
        // is PvZ2's own number (1.5x the 50 of both games' Wall-nut, same ratio Iceweed uses on
        // Spikeweed). PacketCooldown 5 -> m_refreshTime 500 like every other port here
        // (~100 ticks/sec); note this is a much faster recharge than Replanted's native Wall-nut.
        // Nut: enemy Ladder Zombies place their ladder on it like on a Wall-nut (see CustomPlantDefinition.m_countsAsNut).
        m_countsAsNut = true;

        m_seedCost = 75;
        m_versusCost = 75;
        m_health = 4000;

        m_refreshTime = 500;
        m_versusBaseRefreshTime = 500;
        m_versusSuddenDeathRefreshTime = 125;

        // PvZ2 family: Reinforce-mint (same as Replanted's own Wall-nut/Tall-nut).
        m_mintFamily = MintFamily.ReinforceMint;

        SetSeedPacketImage(CompleteCollectionMod.PrimalWallnutBundleId, "assets/plantrip/primal_wallnut/spr_seedpacket_primal_wallnut.png");
        SetPreviewImage(CompleteCollectionMod.PrimalWallnutBundleId, "assets/plantrip/primal_wallnut/spr_seedpacket_primal_wallnut.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "PrimalWallnut_Default",
            AssetBundleId = CompleteCollectionMod.PrimalWallnutBundleId,
            SkinPrefabId = "assets/plantrip/primal_wallnut/models/default/plant/primal_wallnut.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code (a wall has none worth playing).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<PrimalWallnutBehaviorController>();
    }
}

// The PvZ2 rip's "idle"/"idle2" clips are the stone-armoured Plant Food look, NOT the normal
// pose; the normal wall-nut is "damage" (fresh) -> "damage2" -> "damage3" (cracked). The bundle
// build (PvZBundleBuilder.FixPrimalWallnutAnimatorController) makes "damage" the default state and
// loops the three, so this controller only has to switch between them as health drops - the same
// thirds Replanted's native Wall-nut uses for its cracked states.
[RegisterTypeInIl2Cpp]
public class PrimalWallnutBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private static readonly string[] TierClips = { "damage", "damage2", "damage3" };

    private int tickCounter;
    private int shownTier = -1;

    #endregion

    #region Constructors

    public PrimalWallnutBehaviorController(IntPtr pointer) : base(pointer)
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
            return health * 3 > max * 2 ? 0 : health * 3 > max ? 1 : 2;
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
