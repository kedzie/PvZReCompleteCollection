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
        // A wall (like Primal Wall-nut): no projectile, no action; the controller swaps the damage pose and lures.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Sweet Potato", false);
        SetDescription("Sweet Potato is a big, sturdy blocker. Zombies in the lanes next to him smell him and wander over into his lane. He sweats and bruises as they chew through him.", false);
        SetTooltip("A tough wall that lures zombies from the next lanes.", false);

        // PvZ2 SweetPotatoDefault: Cost 150, Hitpoints 4000, PacketCooldown 20 (StartingCooldown 15). He also lures
        // zombies from the lanes next to him into his own (SweetPotatoBehaviorController, ZombieLaneLure).
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

    // PvZ2: NormalAttractRect x 0..80 on his column, the lane above and below; TimeBetweenLaneChanges 5 s
    // (~100 ticks/sec).
    private const float AttractWidth = 80f;
    private const int CheckInterval = 10;
    private const int LureCooldownTicks = 500;

    private int tickCounter;
    private int shownTier = -1;
    private int lureCooldown;

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
        if (lureCooldown > 0)
        {
            lureCooldown--;
        }

        if (tickCounter % CheckInterval != 0)
        {
            return;
        }

        if (lureCooldown <= 0)
        {
            TryLure();
        }

        int tier = CurrentTier;
        if (tier == shownTier)
        {
            return;
        }

        shownTier = tier;
        PlayAnimation(TierClips[tier]);
    }

    // Pull one zombie from the lane above or below into this lane when its body is over his column.
    private void TryLure()
    {
        var board = Board;
        if (board?.m_zombies?.m_list == null)
        {
            return;
        }

        int rows = board.GetNumRows();
        float left = board.GridToPixelX(Plant.mPlantCol, Plant.mRow);
        float right = left + AttractWidth;

        foreach (var entry in ZombieSnapshot.Items(board))
        {
            var zombie = entry.mItem;
            if (zombie == null
                || Math.Abs(zombie.mRow - Plant.mRow) != 1
                || zombie.mRow < 0 || zombie.mRow >= rows
                || !ZombieLaneLure.CanBeMoved(zombie))
            {
                continue;
            }

            var rect = zombie.GetZombieRect();
            if (rect.x >= right || rect.x + rect.width <= left
                || ZombieLaneLure.CrossesWater(board, zombie, Plant.mRow))
            {
                continue;
            }

            ZombieLaneLure.Start(zombie, Plant.mRow);
            lureCooldown = LureCooldownTicks;
            return;
        }
    }

    public override void Reset()
    {
        base.Reset();

        tickCounter = 0;
        shownTier = -1;
        lureCooldown = 0;
    }

    #endregion
}
