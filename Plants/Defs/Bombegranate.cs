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
public class BombegranateDefinition : CustomPlantDefinition
{
    public static SeedType PlantSeedType;

    public BombegranateDefinition(IntPtr pointer) : base(pointer)
    {
        // Instant plant, same shape as Gold Bloom: the controller starts the clip, an Animation Event
        // on the clip does the action, another one removes the plant.
        m_subClass = PlantSubClass.Normal;

        m_entryStatus = ReloadedEntryStatus.Ready;

        SetName("Bombegranate", false);
        SetDescription("Bombegranate is a pomegranate with a very short fuse. It blows up zombies in a 3x3 area and leaves nothing but ash.", false);
        SetTooltip("Explodes at once, destroying every zombie in a 3x3 area.", false);

        // PvZ2 BombegranateDefault: Cost 150, Hitpoints 300, PacketCooldown 35, IsInstant/IsConsumable.
        // Replanted's Cherry Bomb costs 150 too (and recharges slower than this). PacketCooldown 35 ->
        // m_refreshTime 3500 (~100 ticks/sec, same mapping as every other port here).
        m_seedCost = 150;
        m_versusCost = 150;
        m_health = 300;

        m_refreshTime = 3500;
        m_versusBaseRefreshTime = 3500;
        m_versusSuddenDeathRefreshTime = 875;

        // PvZ2 lists the Bombardmint family, but the plant is gone within about two seconds, so
        // there is nothing for a mint to buff (m_mintFamily stays None, like Gold Bloom).

        SetSeedPacketImage(CompleteCollectionMod.BombegranateBundleId, "assets/plantrip/bombegranate/spr_seedpacket_bombegranate.png");
        SetPreviewImage(CompleteCollectionMod.BombegranateBundleId, "assets/plantrip/bombegranate/spr_seedpacket_bombegranate.png");

        RegisterSkin(new SpriteRendererPlantSkin()
        {
            skinId = "Bombegranate_Default",
            AssetBundleId = CompleteCollectionMod.BombegranateBundleId,
            SkinPrefabId = "assets/plantrip/bombegranate/models/default/plant/bombegranate.prefab",
        });

        SetAlmanacBackground(CompleteCollectionMod.EgyptianAssetBundleId, "assets/almanac/backgrounds/pvz2_bg_egypt.png");

        // No sounds are loaded or played from code: the fuse/blast sounds are PlaySound Animation
        // Events baked into the three attack clips from the PAM (see PvZRipTools plant_sounds.py),
        // and the blast itself (flash, crater, smoke) is part of those clips' art.

        PlantSeedType = m_seedType;
    }

    public override Type GetCustomBehaviorType()
    {
        return Il2CppType.Of<BombegranateBehaviorController>();
    }
}

// PvZ2 has three attack animations for it (AnimRigProps AttackAnimations: attack, attack2, attack3), one
// picked per planting; each has the PlaySound event on +1, a use_action marker on +12 (0.4 s: the
// fuse - PvZ2's CooldownTimeMin 1 "how long before we explode" is measured from the plant's start) and
// ends in an OwnerDie event. The bundle build bakes the ExecuteAttack and OwnerDie events on all three.
[RegisterTypeInIl2Cpp]
public class BombegranateBehaviorController : CustomPlantBehaviorController
{
    #region Variables

    private static readonly string[] AttackClips = { "attack", "attack2", "attack3" };

    // PvZ2 BombegranateDefault Action: "radius" explosion, ExplodeRadius 2.0736 (1.44 tiles, the
    // "3by3" of its stats), Damage 1800, Burns. Replanted's Cherry Bomb is the same thing (radius
    // 115 px, the lanes either side, 1800 damage, zombies burn to ash), so it is done the same way.
    private const int BlastRadius = 115;
    private const int RowRange = 1;

    private const DamageRangeFlags BlastTargets = DamageRangeFlags.Ground | DamageRangeFlags.Flying
                                                  | DamageRangeFlags.Submerged | DamageRangeFlags.OffGround;

    // Let the skin finish applying before asking it to play a clip.
    private const float StartDelaySeconds = 0.1f;

    // Safety net only: if the OwnerDie event never fires, remove the plant anyway.
    private const float GiveUpSeconds = 12f;

    private float elapsed;
    private bool started;
    private bool blasted;

    #endregion

    #region Constructors

    public BombegranateBehaviorController(IntPtr pointer) : base(pointer)
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
            PlayAnimation(AttackClips[UnityEngine.Random.Range(0, AttackClips.Length)]);
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

        var plantRect = Plant.GetPlantRect();
        float centerX = plantRect.x + plantRect.width / 2f;
        float centerY = plantRect.y + plantRect.height / 2f;

        // The same native call Cherry Bomb uses: kills every zombie in the circle, in this and the
        // neighbouring lanes (burning them to ash).
        Board.KillAllZombiesInRadius(Plant.mRow, centerX, centerY, BlastRadius, RowRange, true, BlastTargets);
        Board.ShakeBoard(3, -4);
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
