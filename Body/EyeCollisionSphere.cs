using UnityEngine;

public class EyeCollisionSphere : MonoBehaviour
{
    // Secures obstacles like walls by bumping non-editors
    // back when trying to walk through.

    bool active = true;
    
    void OnTriggerEnter(Collider other)
    {
        if (active && IsAppropriateForBumpingOneAway(other) && Our.lastTeleportHitPoint != null)
        {
            active = false;
            Hand.TeleportToPosition((Vector3) Our.lastTeleportHitPoint);
        }
    }

    void OnTriggerExit(Collider other)
    {
        active = true;
    }

    bool IsAppropriateForBumpingOneAway(Collider other)
    {
        bool isAppropriate = false;

        if (Managers.personManager == null || Managers.areaManager == null || other.transform.parent == null)
        {
            return isAppropriate;
        }

        Thing     thing     = other.transform.parent.GetComponent<Thing>();
        ThingPart thingPart = other.GetComponent<ThingPart>();
        if (thing == null || thingPart == null) { return isAppropriate; }

        bool weAreInExplorerMode = !Managers.areaManager.weAreEditorOfCurrentArea || Our.mode == EditModes.None;
        bool movingThroughObstaclesIsAllowed = Managers.areaManager.rights.movingThroughObstacles == true;
        bool isInvisible = other.gameObject.layer == LayerMask.NameToLayer("InvisibleToOurPerson");
        bool isOfPerson = Managers.personManager.GetPersonThisObjectIsOf(other.gameObject) != null;
        bool mayContainMovement = thingPart.states.Count >= 2 || thing.subThingMasterPart != null;
        bool isSmallish = thing.biggestSize <= 1.5f;

        isAppropriate = 
            weAreInExplorerMode &&
            thing.IsPlacement() &&
            Managers.areaManager.didTeleportMoveInThisArea &&
            !(
                movingThroughObstaclesIsAllowed     ||
                isOfPerson                          ||
                CrossDevice.desktopMode             ||
                isInvisible                         ||
                isOfPerson                          ||
                thing.isSittable                    ||
                thing.isPassable                    ||
                thingPart.invisible                 ||
                thingPart.isLiquid                  ||
                thingPart.isInInventoryOrDialog     ||
                mayContainMovement                  ||
                isSmallish
            );

        return isAppropriate;
    }

}
