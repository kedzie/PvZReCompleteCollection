using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppReloaded;
using Il2CppReloaded.Data;
using Il2CppReloaded.Gameplay;
using Il2CppReloaded.Services;
using MelonLoader;
using PvZReCoreLib.Content.Common.Skins.SkinDataTypes.Subtypes.Plant;
using PvZReCoreLib.Content.Plants;
using PvZReCoreLib.Content.Plants.Behavior;
using PvZReCoreLib.Content.Plants.Mint;
using Type = Il2CppSystem.Type;

namespace PvZCompleteCollection.Plants.Defs;

[RegisterTypeInIl2Cpp]
public class ExplodeONutDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public ExplodeONutDefinition(IntPtr pointer) : base(pointer)
    {
        // A wall that does nothing until it is destroyed: Plant.Update still runs for every plant, which is
        // all the behaviour controller needs (damage pose + the "I was destroyed" explosion hook below).
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Explode-o-nut", false);
        SetDescription("Explode-o-nut is a stout wall with a short temper. Zombies that chew it down get blown up for their trouble, along with everything standing around them.", false);
        SetTooltip("A tough wall that explodes when it is destroyed, hurting zombies in a 3x3 area.", false);

        // PvZ2 ExplodeONutDefault: Cost 50, Hitpoints 3000, PacketCooldown 10 (StartingCooldown 5 has no
        // equivalent here). Replanted's Wall-nut costs 50 as well. Hitpoints are taken as they are (PvZ2's
        // Wall-nut is 4000 there, Replanted's is 4000 too), so this nut is the tougher-than-average wall
        // it is meant to be but not a Wall-nut. PacketCooldown 10 -> m_refreshTime 1000 (~100 ticks/sec).
        // Nut: enemy Ladder Zombies place their ladder on it like on a Wall-nut (see CustomPlantDefinition.m_countsAsNut).
        m_countsAsNut = true;

        // Wall-nut first aid: plant it again on a damaged one of its own kind to heal that one to full (see CustomPlantDefinition.m_healsOnReplant).
        m_healsOnReplant = true;

        m_seedCost = 50;
        m_versusCost = 50;
        m_health = 3000;

        m_refreshTime = 1000;
        m_versusBaseRefreshTime = 1000;
        m_versusSuddenDeathRefreshTime = 250;

        // PvZ2 family: Bombardmint (the family of Replanted's own Cherry Bomb and Potato Mine).
        m_mintFamily = MintFamily.BombardMint;

        SetSeedPacketImage(CompleteCollectionMod.ExplodeONutBundleId, "assets/plantrip/explodeonut/spr_seedpacket_explodeonut.png");
        SetPreviewImage(CompleteCollectionMod.ExplodeONutBundleId, "assets/plantrip/explodeonut/spr_seedpacket_explodeonut.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "ExplodeONut_Default",
            AssetBundleId = CompleteCollectionMod.ExplodeONutBundleId,
            SkinPrefabId = "assets/plantrip/explodeonut/models/default/plant/explodeonut.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded from the bundle: the PAM has none for the normal nut (only Plant Food
        // ones). The explosion borrows the game's own Cherry Bomb sound and particle (see Explode()).

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<ExplodeONutBehaviorController>();
    }
}

// Same art layout as Primal Wall-nut: the PvZ2 rip's "idle"/"idle2"/"idle3" clips are the metal-armoured
// Plant Food look, NOT the normal pose; the normal nut is "damage" (fresh) -> "damage2" -> "damage3"
// (cracked). The bundle build (PvZBundleBuilder.ApplyPlantAnimatorFixes, "explodeonut") makes "damage"
// the default state and loops the three, so this controller only has to switch between them as health
// drops (the same thirds Replanted's native Wall-nut uses).
//
// The explosion: CoreLib has no "plant died" hook, so two Harmony patches below (in this mod, not in
// CoreLib) watch Plant.Die and Plant.Squish for this plant's controller and call Explode() first.
[RegisterTypeInIl2Cpp]
public class ExplodeONutBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private static readonly string[] TierClips = { "damage", "damage2", "damage3" };

    // PvZ2: "DamageRadius" power (ExplodeONutDamageLvl1, damage level 1), a 3x3 blast. Not in the
    // data we have; the numbers of Replanted's Cherry Bomb are used (radius 115 px, the lanes either
    // side, 1800 damage = kills every normal zombie, burning them to ash).
    private const int BlastRadius = 115;
    private const int RowRange = 1;

    private const DamageRangeFlags BlastTargets = DamageRangeFlags.Ground | DamageRangeFlags.Flying
                                                  | DamageRangeFlags.Submerged | DamageRangeFlags.OffGround;

    private int tickCounter;
    private int shownTier = -1;
    private bool exploded;

    #endregion

    #region Constructors

    public ExplodeONutBehaviorController(IntPtr pointer) : base(pointer)
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

    // Called by the patches below just before the game removes this nut because zombies destroyed it.
    public void Explode()
    {
        if (exploded)
        {
            return;
        }

        exploded = true;

        var plantRect = Plant.GetPlantRect();
        float centerX = plantRect.x + plantRect.width / 2f;
        float centerY = plantRect.y + plantRect.height / 2f;

        // The same native call Cherry Bomb uses for its blast.
        Board.KillAllZombiesInRadius(Plant.mRow, centerX, centerY, BlastRadius, RowRange, true, BlastTargets);

        // Native explosion effects: Cherry Bomb's particle, screen shake and sound.
        Board.mApp.AddTodParticle(centerX, centerY, Board.MakeRenderOrder(RenderLayer.Top, Plant.mRow, 0), ParticleEffect.Powie);
        Board.ShakeBoard(3, -4);
        AppCore.GetService<IAudioService>().PlayFoley(FoleyType.Cherrybomb);
    }

    public override void Reset()
    {
        base.Reset();

        tickCounter = 0;
        shownTier = -1;
        exploded = false;
    }

    #endregion
}

// Plant.Die is the one way a plant leaves the board, whoever removes it (zombies chewing it down, the
// shovel, the end of the level ...). Only "destroyed" counts for an Explode-o-nut: a nut that has run
// out of health. (A shovelled or end-of-level nut still has health and just goes quietly.)
[HarmonyPatch(typeof(Plant), nameof(Plant.Die))]
public class ExplodeONut_PlantDie_Patch
{
    public static void Prefix(ref Plant __instance)
    {
        if (__instance == null || __instance.mDead || __instance.mPlantHealth > 0)
        {
            return;
        }

        CustomPlantBehaviorController.GetFor(__instance)?.TryCast<ExplodeONutBehaviorController>()?.Explode();
    }
}

// Zomboni / Gargantuar and the like flatten a plant without taking its health down: that is a
// destruction too (the nut blows up under them).
[HarmonyPatch(typeof(Plant), nameof(Plant.Squish))]
public class ExplodeONut_PlantSquish_Patch
{
    public static void Prefix(ref Plant __instance)
    {
        if (__instance == null || __instance.mDead)
        {
            return;
        }

        CustomPlantBehaviorController.GetFor(__instance)?.TryCast<ExplodeONutBehaviorController>()?.Explode();
    }
}
