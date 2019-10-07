using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public static class ThingToJsonConverter
{
    // For the format description, see anyland.com/info/thing-format.html

    public static string GetJson(GameObject thisThingGameObject, ref int vertexCount)
    {
        string json = "";
        Dictionary<string, int> changedVerticesIndexReference = new Dictionary<string, int>();

        Thing thing = thisThingGameObject.GetComponent<Thing>();

        json += "{";
        if (!string.IsNullOrEmpty(thing.givenName) && thing.givenName != CreationHelper.thingDefaultName)
        {
            json += "\"n\":" + JsonHelper.GetJson(thing.givenName) + ",";
        }

        if (thing.version > ThingManager.currentThingVersion)
        {
            thing.version = ThingManager.currentThingVersion;
        }
        json += "\"v\":" + thing.version + ",";
        if (!string.IsNullOrEmpty(thing.description))
        {
            json += "\"d\":" + JsonHelper.GetJson(thing.description) + ",";
        }

        string attributes = GetAttributesAsIntList(thing);
        if (attributes != "")
        {
            json += "\"a\":[" + attributes + "],";
        }

        if (thing.includedNameIds.Count >= 1)
        {
            json += "\"inc\":[" + JsonHelper.GetStringDictionaryAsArray(thing.includedNameIds) + "],";
        }

        if (thing.addBodyWhenAttached || thing.addBodyWhenAttachedNonClearing)
        {
            json += "\"bod\":" + GetOurCurrentBodyAttachmentsAsJson() + ",";
        }

        json += GetThingPhysicsJson(thing);

        RemoveUnneededThingPartGuids(thing);

        json += "\"p\":[";
        int indexWithinThing = 0;
        foreach (Transform child in thisThingGameObject.transform)
        {
            if (child.CompareTag("ThingPart"))
            {
                ThingPart thingPart = child.gameObject.GetComponent<ThingPart>();
                if (indexWithinThing >= 1) { json += ","; }

                json += "{";
                if (thingPart.baseType != ThingPartBase.Cube)
                {
                    json += "\"b\":" + (int)thingPart.baseType + ",";
                }
                if (thingPart.materialType != MaterialTypes.None)
                {
                    json += "\"t\":" + (int)thingPart.materialType + ",";
                }
                string thingPartAttributes = GetThingPartAttributeAsIntList(thingPart);
                if (thingPartAttributes != "")
                {
                    json += "\"a\":[" + thingPartAttributes + "],";
                }
                if (!string.IsNullOrEmpty(thingPart.guid))
                {
                    json += "\"id\":" + JsonHelper.GetJson(thingPart.guid) + ",";
                }
                if (!string.IsNullOrEmpty(thingPart.givenName))
                {
                    json += "\"n\":" + JsonHelper.GetJson(thingPart.givenName) + ",";
                }

                if (thingPart.isText)
                {
                    string thingPartText = thingPart.GetComponent<TextMesh>().text;
                    json += "\"e\":" + JsonHelper.GetJson(thingPartText) + ",";
                    if (thingPart.textLineHeight != 1f)
                    {
                        json += "\"lh\":" + thingPart.textLineHeight + ",";
                    }
                }

                json += GetIncludedSubThingsJson(thing, thingPart);
                json += GetPlacedSubThingsJson(thingPart);
                json += GetControllableJson(thingPart);

                if (thingPart.imageUrl != "")
                {
                    json += "\"im\":" + JsonHelper.GetJson(thingPart.imageUrl) + ",";
                    if (thingPart.imageType == ImageType.Png)
                    {
                        json += "\"imt\":" + (int)thingPart.imageType + ",";
                    }
                }

                if (thingPart.particleSystemType != ParticleSystemType.None)
                {
                    json += "\"pr\":" + ((int)thingPart.particleSystemType) + ",";
                }

                if (thingPart.textureTypes[0] != TextureType.None)
                {
                    json += "\"t1\":" + ((int)thingPart.textureTypes[0]) + ",";
                }
                if (thingPart.textureTypes[1] != TextureType.None)
                {
                    json += "\"t2\":" + ((int)thingPart.textureTypes[1]) + ",";
                }

                if (thingPart.autoContinuation != null)
                {
                    string autoContinuationJson = thingPart.autoContinuation.GetJson();
                    if (autoContinuationJson != "")
                    {
                        json += autoContinuationJson + ",";
                    }
                }

                if (thingPart.changedVertices != null)
                {
                    string changedVerticesJson = GetChangedVerticesJson(
                            thingPart, changedVerticesIndexReference, indexWithinThing);
                    if (changedVerticesJson != "")
                    {
                        json += changedVerticesJson + ",";
                    }
                }

                if (thingPart.smoothingAngle != null)
                {
                    int defaultSmoothingAngle = 0;
                    Managers.thingManager.smoothingAngles.TryGetValue(thingPart.baseType, out defaultSmoothingAngle);

                    if ((int)thingPart.smoothingAngle != defaultSmoothingAngle)
                    {
                        json += "\"sa\":" + ((int)thingPart.smoothingAngle).ToString() + ",";
                    }
                }

                if (thingPart.convex != null)
                {
                    json += "\"cx\":" + ((bool)thingPart.convex ? "1" : "0") + ",";
                }

                json += "\"s\":[";

                json += GetStateJson(child, thingPart);

                json += "]";
                json += "}";

                MeshFilter meshFilter = thingPart.gameObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    vertexCount += meshFilter.sharedMesh.vertexCount;
                }
                else
                {
                    const int guessVerticesForText = 50;
                    vertexCount += guessVerticesForText;
                }
                indexWithinThing++;
            }
        }
        json += "]";

        json += "}";

        json = json.Replace("\r\n", "\n");
        json = json.Replace("\r", "\n");
        json = json.Replace("\n", "\\n");

        return json;
    }

    static void RemoveUnneededThingPartGuids(Thing thing)
    {
        Component[] thingParts = thing.gameObject.GetComponentsInChildren(typeof(ThingPart));
        foreach (ThingPart thingPart in thingParts)
        {
            if (!string.IsNullOrEmpty(thingPart.guid) &&
                    !ThingPartGuidIsReferenced(thing, thingPart.guid, thingPart))
            {
                thingPart.guid = null;
            }
        }
    }

    static bool ThingPartGuidIsReferenced(Thing thing, string guid, ThingPart thingPartToIgnore)
    {
        bool isIt = false;

        Component[] thingParts = thing.gameObject.GetComponentsInChildren(typeof(ThingPart));
        foreach (ThingPart thingPart in thingParts)
        {
            if (thingPart != thingPartToIgnore && thingPart.autoContinuation != null &&
                    thingPart.autoContinuation.fromPartGuid == guid)
            {
                isIt = true;
                break;
            }
        }

        return isIt;
    }

    static string GetIncludedSubThingsJson(Thing thing, ThingPart thingPart)
    {
        string json = "";

        if (thingPart.includedSubThings != null)
        {
            foreach (IncludedSubThing includedSubThing in thingPart.includedSubThings)
            {
                if (json != "") { json += ","; }
                json += "{";

                json += "\"t\":" + JsonHelper.GetJson(includedSubThing.thingId) + ",";
                json += "\"p\":" + JsonHelper.GetJson(includedSubThing.originalRelativePosition) + ",";
                json += "\"r\":" + JsonHelper.GetJson(includedSubThing.originalRelativeRotation);

                if (includedSubThing.nameOverride != null && includedSubThing.nameOverride != thing.givenName)
                {
                    json += ",\"n\":" + JsonHelper.GetJson(includedSubThing.nameOverride);
                }

                string attributesToInvert = GetSubThingAttributeInvertsAsIntList(includedSubThing);
                if (attributesToInvert != "")
                {
                    json += ",\"a\":[" + attributesToInvert + "]";
                }

                json += "}";
            }

            if (json != "")
            {
                json = "\"i\":[" + json + "],";
            }
        }

        return json;
    }

    static string GetPlacedSubThingsJson(ThingPart thingPart)
    {
        string json = "";

        foreach (KeyValuePair<string, ThingIdPositionRotation> entry in thingPart.placedSubThingIdsWithOriginalInfo)
        {
            string placementId = entry.Key;
            ThingIdPositionRotation thingIdPositionRotation = entry.Value;
            if (json != "") { json += ","; }
            json += "{";
            json += "\"i\":" + JsonHelper.GetJson(placementId) + ",";
            json += "\"t\":" + JsonHelper.GetJson(thingIdPositionRotation.thingId) + ",";
            json += "\"p\":" + JsonHelper.GetJson(thingIdPositionRotation.position) + ",";
            json += "\"r\":" + JsonHelper.GetJson(thingIdPositionRotation.rotation);
            json += "}";
        }

        if (json != "")
        {
            json = "\"su\":[" + json + "],";
        }

        return json;
    }

    static string GetControllableJson(ThingPart thingPart)
    {
        string json = "";

        if (thingPart.controllableBodySlidiness != 0f) { json += "\"m_bs\": " + thingPart.controllableBodySlidiness + ","; }
        if (thingPart.controllableBodyBounciness != 0f) { json += "\"m_bb\": " + thingPart.controllableBodyBounciness + ","; }
        if (thingPart.isControllableWheel) { json += "\"m_w\": true,"; }

        if (thingPart.joystickToControllablePart != null && !thingPart.joystickToControllablePart.IsAllDefault())
        {
            json += thingPart.joystickToControllablePart.GetJson() + ",";
        }

        return json;
    }

    static string GetThingPhysicsJson(Thing thing)
    {
        string json = "";

        if (thing.mass != null) { json += "\"tp_m\": " + (float)thing.mass + ","; }
        if (thing.drag != null) { json += "\"tp_d\": " + (float)thing.drag + ","; }
        if (thing.angularDrag != null) { json += "\"tp_ad\": " + (float)thing.angularDrag + ","; }

        if (!thing.lockPhysicsPosition.IsAllDefault())
        {
            json += json += "\"tp_lp\": " + JsonHelper.GetJson(thing.lockPhysicsPosition) + ",";
        }
        if (!thing.lockPhysicsRotation.IsAllDefault())
        {
            json += json += "\"tp_lr\": " + JsonHelper.GetJson(thing.lockPhysicsRotation) + ",";
        }

        return json;
    }

    static string GetOurCurrentBodyAttachmentsAsJson(bool headOnly = false)
    {
        string s = "";

        s += GetOurCurrentBodyAttachmentAsJsonForPart(s, "HeadCore/HeadAttachmentPoint", "h",
                onlyIfCloneOfCurrentHead: true);

        if (!headOnly)
        {
            s += GetOurCurrentBodyAttachmentAsJsonForPart(s, "HeadCore/HeadTopAttachmentPoint", "ht");

            s += GetOurCurrentBodyAttachmentAsJsonForPart(s, "HandCoreLeft/ArmLeftAttachmentPoint", "al");
            s += GetOurCurrentBodyAttachmentAsJsonForPart(s, "HandCoreRight/ArmRightAttachmentPoint", "ar");

            s += GetOurCurrentBodyAttachmentAsJsonForPart(s, "Torso/UpperTorsoAttachmentPoint", "ut");
            s += GetOurCurrentBodyAttachmentAsJsonForPart(s, "Torso/LowerTorsoAttachmentPoint", "lt");

            s += GetOurCurrentBodyAttachmentAsJsonForPart(s, "Torso/LegLeftAttachmentPoint", "ll",
                    includingAttachmentPointPositions: true);
            s += GetOurCurrentBodyAttachmentAsJsonForPart(s, "Torso/LegRightAttachmentPoint", "lr",
                    includingAttachmentPointPositions: true);
        }

        s = "{" + s + "}";

        return s;
    }

    static string GetOurCurrentBodyAttachmentAsJsonForPart(string currentJson, string treePath, string shortName, bool onlyIfCloneOfCurrentHead = false, bool includingAttachmentPointPositions = false)
    {
        string s = "";
        treePath = "/OurPersonRig/" + treePath;
        Transform attachmentPoint = Managers.treeManager.GetTransform(treePath);
        GameObject thingObject = Misc.GetChildWithTag(attachmentPoint, "Attachment");
        if (thingObject != null)
        {
            Thing thing = thingObject.GetComponent<Thing>();
            if (thing != null)
            {
                bool doUse = true;

                if (onlyIfCloneOfCurrentHead)
                {
                    doUse = false;
                    if (CreationHelper.thingThatWasClonedFrom != null)
                    {
                        Thing clonedFromThing = CreationHelper.thingThatWasClonedFrom.GetComponent<Thing>();
                        doUse = clonedFromThing != null && clonedFromThing.thingId != "" &&
                                clonedFromThing.thingId == thing.thingId;
                    }
                }

                if (doUse)
                {
                    if (currentJson != "") { s += ","; }

                    s += "\"" + shortName + "\": {";
                    if (!onlyIfCloneOfCurrentHead)
                    {
                        s += "\"i\":\"" + thing.thingId + "\",";
                    }
                    s += "\"p\":" + JsonHelper.GetJson(thing.transform.localPosition) + ",";
                    s += "\"r\":" + JsonHelper.GetJson(thing.transform.localEulerAngles);

                    if (includingAttachmentPointPositions)
                    {
                        s += ",";
                        s += "\"ap\":" + JsonHelper.GetJson(thing.transform.parent.localPosition) + ",";
                        s += "\"ar\":" + JsonHelper.GetJson(thing.transform.parent.localEulerAngles);
                    }

                    s += "}";
                }
            }
        }
        return s;
    }

    static string GetAttributesAsIntList(Thing thing)
    {
        List<int> list = new List<int>();
        if (thing.isClonable) { list.Add((int)ThingAttribute.isClonable); }
        if (thing.isHoldable) { list.Add((int)ThingAttribute.isHoldable); }
        if (thing.remainsHeld) { list.Add((int)ThingAttribute.remainsHeld); }
        if (thing.isClimbable) { list.Add((int)ThingAttribute.isClimbable); }
        if (thing.isPassable) { list.Add((int)ThingAttribute.isPassable); }
        if (thing.isUnwalkable) { list.Add((int)ThingAttribute.isUnwalkable); }
        if (thing.doSnapAngles) { list.Add((int)ThingAttribute.doSnapAngles); }
        if (thing.doSoftSnapAngles) { list.Add((int)ThingAttribute.doSoftSnapAngles); }
        if (thing.isBouncy) { list.Add((int)ThingAttribute.isBouncy); }
        if (thing.doShowDirection) { list.Add((int)ThingAttribute.doShowDirection); }
        if (thing.keepPreciseCollider) { list.Add((int)ThingAttribute.keepPreciseCollider); }
        if (thing.doesFloat) { list.Add((int)ThingAttribute.doesFloat); }
        if (thing.doesShatter) { list.Add((int)ThingAttribute.doesShatter); }
        if (thing.isSticky) { list.Add((int)ThingAttribute.isSticky); }
        if (thing.isSlidy) { list.Add((int)ThingAttribute.isSlidy); }
        if (thing.doSnapPosition) { list.Add((int)ThingAttribute.doSnapPosition); }
        if (thing.amplifySpeech) { list.Add((int)ThingAttribute.amplifySpeech); }
        if (thing.benefitsFromShowingAtDistance) { list.Add((int)ThingAttribute.benefitsFromShowingAtDistance); }
        if (thing.scaleAllParts) { list.Add((int)ThingAttribute.scaleAllParts); }
        if (thing.doAlwaysMergeParts) { list.Add((int)ThingAttribute.doAlwaysMergeParts); }
        if (thing.addBodyWhenAttached) { list.Add((int)ThingAttribute.addBodyWhenAttached); }
        if (thing.hasSurroundSound) { list.Add((int)ThingAttribute.hasSurroundSound); }
        if (thing.canGetEventsWhenStateChanging) { list.Add((int)ThingAttribute.canGetEventsWhenStateChanging); }
        if (thing.replacesHandsWhenAttached) { list.Add((int)ThingAttribute.replacesHandsWhenAttached); }
        if (thing.mergeParticleSystems) { list.Add((int)ThingAttribute.mergeParticleSystems); }
        if (thing.isSittable) { list.Add((int)ThingAttribute.isSittable); }
        if (thing.smallEditMovements) { list.Add((int)ThingAttribute.smallEditMovements); }
        if (thing.scaleEachPartUniformly) { list.Add((int)ThingAttribute.scaleEachPartUniformly); }
        if (thing.snapAllPartsToGrid) { list.Add((int)ThingAttribute.snapAllPartsToGrid); }
        if (thing.invisibleToUsWhenAttached) { list.Add((int)ThingAttribute.invisibleToUsWhenAttached); }
        if (thing.replaceInstancesInArea) { list.Add((int)ThingAttribute.replaceInstancesInArea); }
        if (thing.addBodyWhenAttachedNonClearing) { list.Add((int)ThingAttribute.addBodyWhenAttachedNonClearing); }
        if (thing.avoidCastShadow) { list.Add((int)ThingAttribute.avoidCastShadow); }
        if (thing.avoidReceiveShadow) { list.Add((int)ThingAttribute.avoidReceiveShadow); }
        if (thing.omitAutoSounds) { list.Add((int)ThingAttribute.omitAutoSounds); }
        if (thing.omitAutoHapticFeedback) { list.Add((int)ThingAttribute.omitAutoHapticFeedback); }
        if (thing.keepSizeInInventory) { list.Add((int)ThingAttribute.keepSizeInInventory); }
        if (thing.autoAddReflectionPartsSideways) { list.Add((int)ThingAttribute.autoAddReflectionPartsSideways); }
        if (thing.autoAddReflectionPartsVertical) { list.Add((int)ThingAttribute.autoAddReflectionPartsVertical); }
        if (thing.autoAddReflectionPartsDepth) { list.Add((int)ThingAttribute.autoAddReflectionPartsDepth); }
        if (thing.activeEvenInInventory) { list.Add((int)ThingAttribute.activeEvenInInventory); }
        if (thing.stricterPhysicsSyncing) { list.Add((int)ThingAttribute.stricterPhysicsSyncing); }
        if (thing.removeOriginalWhenGrabbed) { list.Add((int)ThingAttribute.removeOriginalWhenGrabbed); }
        if (thing.persistWhenThrownOrEmitted) { list.Add((int)ThingAttribute.persistWhenThrownOrEmitted); }
        if (thing.invisible) { list.Add((int)ThingAttribute.invisible); }
        if (thing.uncollidable) { list.Add((int)ThingAttribute.uncollidable); }
        if (thing.movableByEveryone) { list.Add((int)ThingAttribute.movableByEveryone); }
        if (thing.isNeverClonable) { list.Add((int)ThingAttribute.isNeverClonable); }
        if (thing.floatsOnLiquid) { list.Add((int)ThingAttribute.floatsOnLiquid); }
        if (thing.invisibleToDesktopCamera) { list.Add((int)ThingAttribute.invisibleToDesktopCamera); }
        if (thing.personalExperience) { list.Add((int)ThingAttribute.personalExperience); }
        return GetCommaSeparatedIntList(list);
    }

    static string GetSubThingAttributeInvertsAsIntList(IncludedSubThing subThing)
    {
        List<int> list = new List<int>();
        if (subThing.invert_isHoldable) { list.Add((int)ThingAttribute.isHoldable); }
        if (subThing.invert_invisible) { list.Add((int)ThingAttribute.invisible); }
        if (subThing.invert_uncollidable) { list.Add((int)ThingAttribute.uncollidable); }
        return GetCommaSeparatedIntList(list);
    }

    static string GetThingPartAttributeAsIntList(ThingPart thingPart)
    {
        List<int> list = new List<int>();
        if (thingPart.offersScreen) { list.Add((int)ThingPartAttribute.offersScreen); }
        if (thingPart.videoScreenHasSurroundSound) { list.Add((int)ThingPartAttribute.videoScreenHasSurroundSound); }
        if (thingPart.videoScreenLoops) { list.Add((int)ThingPartAttribute.videoScreenLoops); }
        if (thingPart.videoScreenIsDirectlyOnMesh) { list.Add((int)ThingPartAttribute.videoScreenIsDirectlyOnMesh); }
        if (thingPart.scalesUniformly) { list.Add((int)ThingPartAttribute.scalesUniformly); }
        if (thingPart.isLiquid) { list.Add((int)ThingPartAttribute.isLiquid); }
        if (thingPart.offersSlideshowScreen) { list.Add((int)ThingPartAttribute.offersSlideshowScreen); }
        if (thingPart.isCamera) { list.Add((int)ThingPartAttribute.isCamera); }
        if (thingPart.isFishEyeCamera) { list.Add((int)ThingPartAttribute.isFishEyeCamera); }
        if (thingPart.useUnsoftenedAnimations) { list.Add((int)ThingPartAttribute.useUnsoftenedAnimations); }
        if (thingPart.invisible) { list.Add((int)ThingPartAttribute.invisible); }
        if (thingPart.uncollidable) { list.Add((int)ThingPartAttribute.uncollidable); }
        if (thingPart.isUnremovableCenter) { list.Add((int)ThingPartAttribute.isUnremovableCenter); }
        if (thingPart.omitAutoSounds) { list.Add((int)ThingPartAttribute.omitAutoSounds); }
        if (thingPart.doSnapTextureAngles) { list.Add((int)ThingPartAttribute.doSnapTextureAngles); }
        if (thingPart.textureScalesUniformly) { list.Add((int)ThingPartAttribute.textureScalesUniformly); }
        if (thingPart.avoidCastShadow) { list.Add((int)ThingPartAttribute.avoidCastShadow); }
        if (thingPart.looselyCoupledParticles) { list.Add((int)ThingPartAttribute.looselyCoupledParticles); }
        if (thingPart.textAlignCenter) { list.Add((int)ThingPartAttribute.textAlignCenter); }
        if (thingPart.textAlignRight) { list.Add((int)ThingPartAttribute.textAlignRight); }
        if (thingPart.isAngleLocker) { list.Add((int)ThingPartAttribute.isAngleLocker); }
        if (thingPart.isPositionLocker) { list.Add((int)ThingPartAttribute.isPositionLocker); }
        if (thingPart.isLocked) { list.Add((int)ThingPartAttribute.isLocked); }
        if (thingPart.avoidReceiveShadow) { list.Add((int)ThingPartAttribute.avoidReceiveShadow); }
        if (thingPart.isImagePasteScreen) { list.Add((int)ThingPartAttribute.isImagePasteScreen); }
        if (thingPart.allowBlackImageBackgrounds) { list.Add((int)ThingPartAttribute.allowBlackImageBackgrounds); }
        if (thingPart.useTextureAsSky) { list.Add((int)ThingPartAttribute.useTextureAsSky); }
        if (thingPart.stretchSkydomeSeam) { list.Add((int)ThingPartAttribute.stretchSkydomeSeam); }
        if (thingPart.subThingsFollowDelayed) { list.Add((int)ThingPartAttribute.subThingsFollowDelayed); }
        if (thingPart.hasReflectionPartSideways) { list.Add((int)ThingPartAttribute.hasReflectionPartSideways); }
        if (thingPart.hasReflectionPartVertical) { list.Add((int)ThingPartAttribute.hasReflectionPartVertical); }
        if (thingPart.hasReflectionPartDepth) { list.Add((int)ThingPartAttribute.hasReflectionPartDepth); }
        if (thingPart.videoScreenFlipsX) { list.Add((int)ThingPartAttribute.videoScreenFlipsX); }
        if (thingPart.persistStates) { list.Add((int)ThingPartAttribute.persistStates); }
        if (thingPart.isDedicatedCollider) { list.Add((int)ThingPartAttribute.isDedicatedCollider); }
        if (thingPart.personalExperience) { list.Add((int)ThingPartAttribute.personalExperience); }
        if (thingPart.invisibleToUsWhenAttached) { list.Add((int)ThingPartAttribute.invisibleToUsWhenAttached); }
        if (thingPart.lightOmitsShadow) { list.Add((int)ThingPartAttribute.lightOmitsShadow); }
        if (thingPart.showDirectionArrowsWhenEditing) { list.Add((int)ThingPartAttribute.showDirectionArrowsWhenEditing); }
        return GetCommaSeparatedIntList(list);
    }

    public static string GetCommaSeparatedIntList(List<int> list)
    {
        string s = "";
        foreach (var v in list)
        {
            if (s != "") { s += ","; }
            s += v.ToString();
        }
        return s;
    }

    public static string GetCommaSeparatedStringList(List<string> list)
    {
        string s = "";
        foreach (string v in list)
        {
            if (s != "") { s += "\",\""; }
            s += v;
        }
        if (s != "") { s = "\"" + s + "\""; }
        return s;
    }

    static string GetStateJson(Transform transform, ThingPart thingPart)
    {
        string json = "";

        int i = 0;
        foreach (ThingPartState state in thingPart.states)
        {
            if (++i >= 2) { json += ","; }
            json += "{";

            json += "\"p\":" + JsonHelper.GetJson(state.position) + ",";
            json += "\"r\":" + JsonHelper.GetJson(state.rotation) + ",";
            json += "\"s\":" + JsonHelper.GetJson(state.scale) + ",";
            json += "\"c\":" + JsonHelper.GetJson(state.color);

            if (state.scriptLines.Count >= 1)
            {
                json += ",\"b\":[";
                for (int n = 0; n < state.scriptLines.Count; n++)
                {
                    if (n != 0) { json += ","; }
                    json += JsonHelper.GetJson(state.scriptLines[n]);
                }
                json += "]";
            }

            if (state.textureProperties != null && state.textureProperties[0] != null &&
                    thingPart.textureTypes[0] != TextureType.None)
            {
                json += ",\"t1\":{" + GetStateTextureJson(thingPart, state, 0) + "}";
            }

            if (state.textureProperties != null && state.textureProperties[1] != null &&
                    thingPart.textureTypes[1] != TextureType.None)
            {
                json += ",\"t2\":{" + GetStateTextureJson(thingPart, state, 1) + "}";
            }

            if (state.particleSystemProperty != null)
            {
                json += ",\"pr\":{" + GetStateParticleSystemJson(thingPart, state) + "}";
            }

            json += "}";
        }

        return json;
    }

    static string GetStateTextureJson(ThingPart thingPart, ThingPartState state, int index)
    {
        string s = "";
        s += "\"c\":" + JsonHelper.GetJson(state.textureColors[index]);
        bool withOnlyAlphaSetting = Managers.thingManager.IsTextureTypeWithOnlyAlphaSetting(thingPart.textureTypes[index]);
        foreach (KeyValuePair<TextureProperty, string> item in Managers.thingManager.texturePropertyAbbreviations)
        {
            if (!withOnlyAlphaSetting || item.Key == TextureProperty.Strength)
            {
                s += ",\"" + item.Value + "\":" + state.textureProperties[index][item.Key];
            }
        }
        return s;
    }

    static string GetStateParticleSystemJson(ThingPart thingPart, ThingPartState state)
    {
        string s = "";
        s += "\"c\":" + JsonHelper.GetJson(state.particleSystemColor);
        bool withOnlyAlphaSetting = Managers.thingManager.IsParticleSystemTypeWithOnlyAlphaSetting(thingPart.particleSystemType);
        foreach (KeyValuePair<ParticleSystemProperty, string> item in Managers.thingManager.particleSystemPropertyAbbreviations)
        {
            if (!withOnlyAlphaSetting || item.Key == ParticleSystemProperty.Alpha)
            {
                s += ",\"" + item.Value + "\":" + state.particleSystemProperty[item.Key];
            }
        }
        return s;
    }

    static string GetChangedVerticesJson(ThingPart thingPart, Dictionary<string, int> changedVerticesIndexReference, int indexWithinThing)
    {
        // Changed Vertices  c: [ [x, y, z, i1, relative i2, ...], ... ]

        string s = "";

        Dictionary<string, string> indicesByPosition = new Dictionary<string, string>();

        foreach (KeyValuePair<int, Vector3> item in thingPart.changedVertices)
        {
            int index = item.Key;
            Vector3 position = item.Value;
            string positionString = JsonHelper.GetJsonNoBrackets(position);
            if (!indicesByPosition.ContainsKey(positionString))
            {

                string indices = "";
                int previousInnerIndex = 0;
                foreach (KeyValuePair<int, Vector3> innerItem in thingPart.changedVertices)
                {
                    int innerIndex = innerItem.Key;
                    Vector3 innerPosition = innerItem.Value;
                    if (JsonHelper.GetJsonNoBrackets(innerPosition) == positionString)
                    {
                        if (indices != "") { indices += ","; }
                        int relativeIndex = innerIndex - previousInnerIndex;
                        indices += relativeIndex.ToString();
                        previousInnerIndex = innerIndex;
                    }
                }
                indicesByPosition[positionString] = indices;

            }
        }

        foreach (KeyValuePair<string, string> item in indicesByPosition)
        {
            if (s != "") { s += ","; }
            s += "[" + item.Key + "," + item.Value + "]";
        }

        if (s != "")
        {
            s = "\"c\":[" + s + "]";

            string key = s;
            if (thingPart.smoothingAngle != null) { key += ((int)thingPart.smoothingAngle).ToString(); }
            key += "_";
            if (thingPart.convex != null) { key += thingPart.convex == true ? "1" : "0"; }

            int existingIndex;
            if (changedVerticesIndexReference.TryGetValue(key, out existingIndex))
            {
                s = "\"v\":" + existingIndex;
                thingPart.smoothingAngle = null;
                thingPart.convex = null;
            }
            else
            {
                changedVerticesIndexReference[key] = indexWithinThing;
            }
        }

        return s;
    }

}