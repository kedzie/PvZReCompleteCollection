using Il2CppReloaded.Gameplay;
using MelonLoader;
using UnityEngine;

namespace PvZCompleteCollection.Plants;

// Places a ripped PvZ2 effect prefab (an effect PAM exported by PvZRipTools, e.g. Snapdragon's fire, Primal
// Potato Mine's explosion, Aloe's heal) relative to a plant, the way PvZ2 does it: the effect's PAM origin is the
// plant's PAM origin.
//
// Every frame the exporter writes is anchored at a fixed bottom-middle point of its animation set (PvZRipTools
// README, "Frame cropping and the anchor"): for a plant the lowest point of its standing clips, for an effect the
// lowest point of the whole effect. Both anchors are expressed in the same PAM canvas, so the effect has to sit
// at (effect anchor - plant anchor) from the plant's own anchor. The caller passes that difference, worked out
// from the two manifest.json files (dx right, dy DOWN, in art pixels), and this class turns it into world units.
//
// The plant's anchor in the world is the transform of its skin's "anim" child (the SpriteRenderer whose per-frame
// custom pivots are exactly that anchor), see CoreLib's SpriteRendererSkin; one art pixel is that transform's
// lossy scale. For a plant that has no such skin (a native one) the offset between this plant's own anim and its
// controller is reused, which is what 'AnchorOffset' is for.
public static class PlantEffectSpawner
{
    // World position of the art anchor of one of the mod's own plants, and the world size of one art pixel.
    //
    // The skin is NOT a direct child of the plant's controller object: CoreLib's SkinRegistry applies it to the
    // CharacterAnimationController's GameObject (a child of the controller object, see SkinRegistry /
    // PlantExtension), so the path is  <controller>/<CharacterAnimationController object>/SpriteRendererSkin/anim.
    // (A first version looked for "SpriteRendererSkin/anim" straight under the controller, found nothing, and the
    // effects were silently never spawned.)
    public static bool TryGetAnchor(Plant plant, out Vector3 anchor, out float unit)
    {
        anchor = default;
        unit = 1f;

        var controller = plant?.mController;
        if (controller == null)
        {
            return false;
        }

        Transform anim = null;

        var animationController = controller.AnimationController;
        if (animationController != null)
        {
            anim = animationController.gameObject.transform.Find("SpriteRendererSkin/anim");
        }

        if (anim == null)
        {
            // Fallback: any "anim" under a "SpriteRendererSkin" below the controller.
            foreach (var child in controller.gameObject.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "anim" && child.parent != null && child.parent.name == "SpriteRendererSkin")
                {
                    anim = child;
                    break;
                }
            }
        }

        if (anim == null)
        {
            return false;
        }

        anchor = anim.position;
        unit = anim.lossyScale.x;
        return true;
    }

    // World offset from a plant's controller to the anchor of its skin (valid for the mod's own plants); apply it
    // to any other plant's controller position to find where an equivalent anchor would be.
    public static bool TryGetAnchorOffset(Plant plant, out Vector3 offset, out float unit)
    {
        offset = default;
        if (!TryGetAnchor(plant, out var anchor, out unit))
        {
            return false;
        }

        offset = anchor - plant.mController.gameObject.transform.position;
        return true;
    }

    // Anchor + Spawn for one of the mod's own plants: the effect at (dx, dyDown) art pixels from the plant's art anchor.
    public static GameObject SpawnAtPlant(GameObject prefab, Plant plant, float dx, float dyDown, int sortingOrder, float lifetime)
    {
        if (prefab == null || !TryGetAnchor(plant, out var anchor, out float unit))
        {
            return null;
        }

        return Spawn(prefab, anchor, unit, dx, dyDown, sortingOrder, lifetime);
    }

    // Free-standing copy of an effect prefab, destroyed after 'lifetime' seconds (the clip's length: the game does
    // not destroy it, same as the hit effects). sortingOrder: the game's own render order, remapped, e.g.
    // SortingUtils.RemapSortingLayers(Board.MakeRenderOrder(RenderLayer.Particle, row, 1)).
    public static GameObject Spawn(GameObject prefab, Vector3 anchorWorld, float unit, float dx, float dyDown,
        int sortingOrder, float lifetime)
    {
        if (prefab == null)
        {
            return null;
        }

        var position = anchorWorld + new Vector3(dx * unit, -dyDown * unit, 0f);
        var instance = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        instance.transform.localScale = new Vector3(unit, unit, 1f);

        foreach (var renderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sortingOrder = sortingOrder;
        }

        UnityEngine.Object.Destroy(instance, lifetime);
        return instance;
    }
}
