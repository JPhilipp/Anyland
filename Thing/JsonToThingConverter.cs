using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using SimpleJSON;
using System.Linq;

public static class JsonToThingConverter
{
    // For the format description, see anyland.com/info/thing-format.html

    static ThingPartAttribute[] partAttributesWhichCanBeMerged = new ThingPartAttribute[] {
            ThingPartAttribute.scalesUniformly,
            ThingPartAttribute.useUnsoftenedAnimations,
            ThingPartAttribute.omitAutoSounds,
            ThingPartAttribute.doSnapTextureAngles,
            ThingPartAttribute.textureScalesUniformly,
            ThingPartAttribute.isAngleLocker,
            ThingPartAttribute.isPositionLocker,
            ThingPartAttribute.isLocked,
            ThingPartAttribute.hasReflectionPartSideways,
            ThingPartAttribute.hasReflectionPartVertical,
            ThingPartAttribute.hasReflectionPartDepth,
        };

    const string mergableName = "MergableThingPart";

    public static void SetThing(GameObject thisThingGameObject, string json, bool alwaysKeepThingPartsSeparate = false, bool isForPlacement = false, Vector3? initialPosition = null, Vector3? initialRotation = null)
    {
        if (String.IsNullOrEmpty(json)) { Log.Warning("SetThingFromJsonString got null json"); return; }

        JSONNode data = JSON.Parse(json);
        if (data["p"].Count == 0) { Log.Warning("SetThingFromJsonString got 0 parts json"); return; }

        string thingName = data["n"] != null && !string.IsNullOrEmpty(data["n"].Value) ?
                data["n"].Value : CreationHelper.thingDefaultName;
        thisThingGameObject.name = thingName;

        Thing thing = thisThingGameObject.GetComponent<Thing>();
        thing.givenName = thingName;
        thing.version = data["v"] != null ? data["v"].AsInt : 1;
        thing.description = data["d"] != null ? data["d"] : null;

        ExpandThingAttributeFromJson(thing, data["a"]);
        ExpandThingIncludedNameIdsFromJson(thing, data["inc"]);
        bool containsLiquid = false;
        bool isVideoOrCameraButtonRelated = false;
        bool containsCollisionListener = false;
        bool anyThingPartContainsBehaviorScript = false;
        thing.containsInvisibleOrUncollidable = thing.invisible || thing.uncollidable || thing.suppressCollisions;

        if (data["tp_m"] != null) { thing.mass = data["tp_m"]; }
        if (data["tp_d"] != null) { thing.drag = data["tp_d"]; }
        if (data["tp_ad"] != null) { thing.angularDrag = data["tp_ad"]; }
        if (data["tp_lp"] != null) { thing.lockPhysicsPosition = JsonHelper.GetBoolVector3(data["tp_lp"]); }
        if (data["tp_lr"] != null) { thing.lockPhysicsRotation = JsonHelper.GetBoolVector3(data["tp_lr"]); }

        thing.thingPartCount = data["p"].Count;
        for (int i = 0; i < thing.thingPartCount; i++)
        {
            JSONNode part = data["p"][i];
            int baseIndex = part["b"] != null ? part["b"].AsInt : 1;

            GameObject thingPartGameObject = (GameObject)UnityEngine.Object.Instantiate(
                    Managers.thingManager.thingPartBases[baseIndex], Vector3.zero, Quaternion.identity);

            thingPartGameObject.name = Misc.RemoveCloneFromName(thingPartGameObject.name);
            thingPartGameObject.tag = "ThingPart";
            thingPartGameObject.transform.parent = thisThingGameObject.transform;

            ThingPart thingPart = thingPartGameObject.GetComponent<ThingPart>();
            thingPart.thingVersion = thing.version;
            thingPart.indexWithinThing = i;
            thingPart.isInInventoryOrDialog = thing.isInInventoryOrDialog;
            thingPart.isInInventory = thing.isInInventory;
            thingPart.activeEvenInInventory = thing.activeEvenInInventory;
            thingPart.isGiftInDialog = thing.isGiftInDialog;
            if (part["id"]) { thingPart.guid = part["id"]; }
            ExpandThingPartAttributeFromJson(thingPart, part["a"]);
            if (thing.stricterPhysicsSyncing) { thingPart.parentPersistsPhysics = true; }

            if (part["n"] != null && part["n"].Value != "")
            {
                thingPart.givenName = part["n"].Value;
            }

            if (part["m_bs"] != null) { thingPart.controllableBodySlidiness = part["m_bs"].AsFloat; }
            if (part["m_bb"] != null) { thingPart.controllableBodyBounciness = part["m_bb"].AsFloat; }
            thingPart.isControllableWheel = part["m_w"] != null;
            if (part["j"] != null)
            {
                thingPart.joystickToControllablePart = new JoystickToControllablePart();
                thingPart.joystickToControllablePart.SetFromJson(part["j"]);
            }

            if (part["vid_a"] != null) { thingPart.videoIdToPlayAtAreaStart = part["vid_a"]; }
            if (part["vid_p"] != null) { thingPart.videoIdToPlayWhenPressed = part["vid_p"]; }
            if (part["vid_v"] != null) { thingPart.videoAutoPlayVolume = part["vid_v"].AsFloat; }

            if (part["ac"] != null)
            {
                thingPart.autoContinuation = new ThingPartAutoContinuation();
                thingPart.autoContinuation.SetByJson(part["ac"]);
            }

            if (thingPart.isText && part["e"] != null)
            {
                string text = part["e"].Value;
                if (part["lh"] != null) { thingPart.textLineHeight = part["lh"].AsFloat; }
                thingPart.SetOriginalText(text);
            }

            if (part["t"] != null)
            {
                thingPart.materialType = (MaterialTypes)part["t"].AsInt;
                if (thingPart.materialType == MaterialTypes.InvisibleWhenDone_Deprecated)
                {
                    thingPart.materialType = MaterialTypes.None;
                    thingPart.invisible = true;
                }

                switch (thingPart.materialType)
                {
                    case MaterialTypes.PointLight:
                    case MaterialTypes.SpotLight:
                        if (thing.suppressLights)
                        {
                            thingPart.materialType = MaterialTypes.Glow;
                        }
                        else
                        {
                            thing.containsLight = true;
                        }
                        break;

                    case MaterialTypes.Particles:
                    case MaterialTypes.ParticlesBig:
                        if (thing.suppressParticles)
                        {
                            thingPart.materialType = MaterialTypes.None;
                        }
                        else
                        {
                            thing.containsBaseLayerParticleSystem = true;
                        }
                        break;
                }
            }
            else if (thingPart.isText && thing.version <= 3)
            {
                thingPart.materialType = MaterialTypes.Glow;
            }

            ApplyVertexChangesAndSmoothingAngle(thingPart, part, alwaysKeepThingPartsSeparate, data["p"]);

            if (part["i"] != null && part["i"].Count >= 1)
            {
                thingPart.includedSubThings = new List<IncludedSubThing>();
                for (int n = 0; n < part["i"].Count; n++)
                {
                    JSONNode subThingNode = part["i"][n];

                    IncludedSubThing includedSubThing = new IncludedSubThing();
                    includedSubThing.thingId = subThingNode["t"];
                    includedSubThing.originalRelativePosition = JsonHelper.GetVector3(subThingNode["p"]);
                    includedSubThing.originalRelativeRotation = JsonHelper.GetVector3(subThingNode["r"]);
                    if (subThingNode["n"] != null)
                    {
                        includedSubThing.nameOverride = subThingNode["n"];
                    }

                    ExpandIncludedSubThingInvertAttributeFromJson(includedSubThing, subThingNode["a"]);

                    thingPart.includedSubThings.Add(includedSubThing);
                }
            }

            if (part["su"] != null && part["su"].Count >= 1)
            {
                thing.containsPlacedSubThings = true;
                thing.requiresWiderReach = true;
                for (int n = 0; n < part["su"].Count; n++)
                {
                    JSONNode placedSubThing = part["su"][n];
                    if (placedSubThing["i"] != null)
                    {
                        string thingId = placedSubThing["t"];
                        Vector3 position = JsonHelper.GetVector3(placedSubThing["p"]);
                        Vector3 rotation = JsonHelper.GetVector3(placedSubThing["r"]);
                        thingPart.AddConfirmedNonExistingPlacedSubThingId(placedSubThing["i"], thingId, position, rotation);
                    }
                }
            }

            if (part["im"] != null)
            {
                Managers.areaManager.containsThingPartWithImage = true;
                thingPart.imageUrl = part["im"];
                if (part["imt"] != null)
                {
                    thingPart.imageType = (ImageType)part["imt"].AsInt;
                }
                thing.allPartsImageCount++;
            }

            bool shiftTexture2Left = part["t1"] == null && part["t2"] != null;

            if (!Managers.optimizationManager.hideTextures)
            {
                if (part["t1"] != null)
                {
                    if (thingPart.textureTypes == null)
                    {
                        thingPart.textureTypes = new TextureType[] { TextureType.None, TextureType.None };
                    }
                    thingPart.textureTypes[0] = (TextureType)part["t1"].AsInt;
                }
                if (part["t2"] != null)
                {
                    if (thingPart.textureTypes == null)
                    {
                        thingPart.textureTypes = new TextureType[] { TextureType.None, TextureType.None };
                    }
                    int textureIndex = shiftTexture2Left ? 0 : 1;
                    thingPart.textureTypes[textureIndex] = (TextureType)part["t2"].AsInt;
                }
            }

            if (part["pr"] != null && !thing.suppressParticles)
            {
                thingPart.particleSystemType = (ParticleSystemType)part["pr"].AsInt;
                if (thingPart.particleSystemType != ParticleSystemType.None)
                {
                    thing.containsParticleSystem = true;
                }
            }

            bool thingPartContainsBehaviorScript = false;
            int maxStates = thing.suppressScriptsAndStates ? 1 : part["s"].Count;
            for (int statesI = 0; statesI < maxStates; statesI++)
            {
                if (statesI >= 1) { thingPart.states.Add(new ThingPartState()); }
                JSONNode state = part["s"][statesI];

                thingPart.states[statesI].position = new Vector3(
                    state["p"][0].AsFloat, state["p"][1].AsFloat, state["p"][2].AsFloat
                );
                thingPart.states[statesI].rotation = new Vector3(
                    state["r"][0].AsFloat, state["r"][1].AsFloat, state["r"][2].AsFloat
                );
                thingPart.states[statesI].scale = new Vector3(
                    state["s"][0].AsFloat, state["s"][1].AsFloat, state["s"][2].AsFloat
                );
                thingPart.states[statesI].color = JsonHelper.GetColor(state["c"]);

                thingPart.states[statesI].name = thingPart.givenName;

                if (state["b"] != null && !thing.suppressScriptsAndStates)
                {
                    thing.containsBehaviorScript = true;
                    thingPartContainsBehaviorScript = true;
                    anyThingPartContainsBehaviorScript = true;
                    for (int n = 0; n < state["b"].Count; n++)
                    {
                        thingPart.states[statesI].scriptLines.Add(state["b"][n].Value);
                    }
                    thingPart.states[statesI].ParseScriptLinesIntoListeners(thing, thingPart);
                    if (!thing.containsOnAnyListener)
                    {
                        thing.containsOnAnyListener = thingPart.states[statesI].ContainsOnAnyListener();
                    }

                    if (!thingPart.isGrabbable)
                    {
                        foreach (StateListener listener in thingPart.states[statesI].listeners)
                        {
                            if (listener.eventType == StateListener.EventType.OnGrabbed)
                            {
                                thingPart.isGrabbable = true;
                                break;
                            }
                            else if (listener.eventType == StateListener.EventType.OnTriggered &&
                                    thing.isHoldable)
                            {
                                thing.remainsHeld = true;
                            }
                            else if (listener.eventType == StateListener.EventType.OnHearsAnywhere)
                            {
                                thing.requiresWiderReach = true;
                            }
                        }
                    }

                    if (!containsCollisionListener)
                    {
                        containsCollisionListener =
                                GetContainsCollisionListener(thingPart.states[statesI].listeners);
                    }

                    if (!thingPart.containsBehaviorScriptVariables)
                    {
                        thingPart.containsBehaviorScriptVariables =
                                GetContainsBehaviorScriptVariables(thingPart.states[statesI].listeners);
                        if (!thing.containsBehaviorScriptVariables && thingPart.containsBehaviorScriptVariables)
                        {
                            thing.containsBehaviorScriptVariables = true;
                        }
                    }

                    if (!thingPart.containsTextCommands)
                    {
                        thingPart.containsTextCommands = GetContainsTextCommands(thingPart.states[statesI].listeners);
                    }

                    if (!thing.requiresWiderReach)
                    {
                        thing.requiresWiderReach =
                                GetContainsAttractRepelOrLoopSurroundCommands(thingPart.states[statesI].listeners);
                    }

                    if (!thingPart.containsTurnCommands)
                    {
                        thingPart.containsTurnCommands = GetContainsTurnCommands(thingPart.states[statesI].listeners);
                        if (thingPart.containsTurnCommands)
                        {
                            thing.containsInvisibleOrUncollidable = true;
                        }
                    }
                }

                if (!thing.requiresWiderReach)
                {
                    thing.requiresWiderReach =
                            thingPart.videoScreenHasSurroundSound || thingPart.useTextureAsSky ||
                            !string.IsNullOrEmpty(thingPart.videoIdToPlayAtAreaStart);
                }

                if (!Managers.optimizationManager.hideTextures)
                {
                    if (state["t1"] != null)
                    {
                        SetTextureFromStateProperties(thingPart, thingPart.states[statesI], state["t1"], 0);
                    }
                    if (state["t2"] != null)
                    {
                        int textureIndex = shiftTexture2Left ? 0 : 1;
                        SetTextureFromStateProperties(thingPart, thingPart.states[statesI], state["t2"], textureIndex);
                    }
                }

                if (state["pr"] != null)
                {
                    SetParticleSystemFromStateProperties(thingPart.states[statesI], state["pr"]);
                }
            }

            if (thingPart.isLiquid) { containsLiquid = true; }
            if (!isVideoOrCameraButtonRelated)
            {
                isVideoOrCameraButtonRelated =
                        thingPart.isVideoButton ||
                        thingPart.isSlideshowButton ||
                        thingPart.isCameraButton;
            }

            Renderer thingPartRenderer = thingPart.GetComponent<Renderer>();

            if (part["pr"] != null)
            {
                thingPart.UpdateParticleSystem();
            }
            if (part["t1"] != null || part["t2"] != null)
            {
                if (!Managers.optimizationManager.hideTextures)
                {
                    thingPart.UpdateTextures();
                }
            }

            if (thingPartContainsBehaviorScript ||
                    thingPart.HasControllableSettings() ||
                    (containsCollisionListener && !thing.doAlwaysMergeParts) ||
                    (!isForPlacement && (thing.isHoldable || thing.remainsHeld)) ||
                    alwaysKeepThingPartsSeparate ||
                    thingPart.materialType == MaterialTypes.SpotLight ||
                    thingPart.materialType == MaterialTypes.PointLight ||
                    thingPart.materialType == MaterialTypes.Particles ||
                    thingPart.materialType == MaterialTypes.ParticlesBig ||
                    ContainsPartAttributesWhichShouldPreventMerging(part["a"]) ||
                    thingPart.isLiquid ||
                    thingPart.isText ||
                    !string.IsNullOrEmpty(thingPart.guid) ||
                    thingPart.includedSubThings != null ||
                    thingPart.useTextureAsSky ||
                    thingPart.imageUrl != "" ||
                    thingPart.isImagePasteScreen ||
                    isVideoOrCameraButtonRelated ||
                    thingPart.HasVertexTexture() ||
                    (thingPart.avoidCastShadow && !thing.avoidCastShadow) ||
                    (thingPart.avoidReceiveShadow && !thing.avoidReceiveShadow) ||
                    (thingPart.textureTypes[0] != TextureType.None && thingPart.states.Count >= 2) ||
                    (thingPart.particleSystemType != ParticleSystemType.None &&
                        !(thing.mergeParticleSystems || thing.doAlwaysMergeParts))
                    )
            {
                thingPart.material = thingPartRenderer.material;
            }
            else
            {
                string materialId = GetAddSharedMaterialsId(thingPart, thingPartRenderer);
                thingPartRenderer.sharedMaterial = Managers.thingManager.sharedMaterials[materialId];
                thingPart.material = thingPartRenderer.sharedMaterial;
                thingPart.name = mergableName;
            }

            if (thingPart.avoidCastShadow || thing.avoidCastShadow)
            {
                thingPartRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (thingPart.avoidReceiveShadow || thing.avoidReceiveShadow)
            {
                thingPartRenderer.receiveShadows = false;
            }

            ConvertDeprecatedHideEffectShapes(thing, thingPart);

            if (thingPart.invisible || thingPart.uncollidable)
            {
                thing.containsInvisibleOrUncollidable = true;
            }

            thingPart.SetTransformPropertiesByState();
        }
        thing.UpdateIsBigIndicators();

        bool doNormallyMerge = !(containsCollisionListener || thing.isVeryBig ||
                isVideoOrCameraButtonRelated || (!isForPlacement && (thing.isHoldable || thing.remainsHeld)) ||
                containsLiquid || thing.thingPartCount <= 1) || thing.suppressScriptsAndStates;

        if (Managers.thingManager.mergeThings &&
                !alwaysKeepThingPartsSeparate && (doNormallyMerge || thing.doAlwaysMergeParts))
        {
            AddStaticReflectionAndContinuationParts(thing);
            MergeMeshesToOptimize(thisThingGameObject, thing, thing.keepPreciseCollider);
        }

        bool isEditMode = alwaysKeepThingPartsSeparate;

        if (thing.isPassable)
        {
            ThingManager.SetLayerForThingAndParts(thing, "PassableObjects");
        }
        else if (thing.invisibleToDesktopCamera)
        {
            ThingManager.SetLayerForThingAndParts(thing, "InvisibleToDesktopCamera");
        }

        if (thing.movableByEveryone)
        {
            thing.gameObject.AddComponent<ThingMovableByEveryone>();
        }

        thing.UpdateAllVisibilityAndCollision(forceVisible: isEditMode, forceCollidable: isEditMode);

        if (initialPosition != null) { thing.transform.localPosition = (Vector3)initialPosition; }
        if (initialRotation != null) { thing.transform.localEulerAngles = (Vector3)initialRotation; }
    }

    static void ApplyVertexChangesAndSmoothingAngle(ThingPart thingPart, JSONNode partNode, bool isForEditing, JSONNode thingNode)
    {
        if (partNode["c"] != null || partNode["v"] != null || partNode["sa"] != null)
        {
            MeshFilter meshFilter = thingPart.GetComponent<MeshFilter>();
            if (meshFilter == null) { return; }
            Mesh mesh = meshFilter.mesh;

            // Changed Vertices reference  v: 2
            if (partNode["v"] != null)
            {
                int indexReference = partNode["v"].AsInt;
                partNode["c"] = thingNode[indexReference]["c"];
                if (thingNode[indexReference]["sa"] != null)
                {
                    partNode["sa"] = thingNode[indexReference]["sa"];
                }
                if (thingNode[indexReference]["cx"] != null)
                {
                    partNode["cx"] = thingNode[indexReference]["cx"];
                }
            }

            // Changed Vertices  c: [ [x, y, z, i1, relative i2, ...], ... ]
            if (partNode["c"] != null)
            {
                Vector3[] vertices = mesh.vertices;
                int verticesMax = mesh.vertices.Length - 1;

                if (isForEditing)
                {
                    thingPart.changedVertices = new Dictionary<int, Vector3>();
                }

                int itemMax = partNode["c"].Count;
                for (int itemI = 0; itemI < itemMax; itemI++)
                {
                    JSONNode item = partNode["c"][itemI];
                    Vector3 vector = new Vector3(item[0].AsFloat, item[1].AsFloat, item[2].AsFloat);

                    int vertexMax = item.Count;
                    int previousVertexIndex = 0;
                    for (int vertexI = 3; vertexI < vertexMax; vertexI++)
                    {
                        int relativeVertexIndex = item[vertexI].AsInt;
                        int vertexIndex = previousVertexIndex + relativeVertexIndex;
                        if (vertexIndex <= verticesMax)
                        {
                            vertices[vertexIndex] = vector;
                            previousVertexIndex = vertexIndex;

                            if (isForEditing)
                            {
                                thingPart.changedVertices[vertexIndex] = vector;
                            }
                        }
                    }
                }

                mesh.vertices = vertices;
            }


            // Smoothing Angle
            int smoothingAngle = 0;
            if (partNode["sa"] != null)
            {
                thingPart.smoothingAngle = partNode["sa"].AsInt;
                smoothingAngle = (int)thingPart.smoothingAngle;
            }
            else
            {
                int defaultSmoothingAngle = 0;
                if (Managers.thingManager.smoothingAngles.TryGetValue(thingPart.baseType, out defaultSmoothingAngle))
                {
                    smoothingAngle = defaultSmoothingAngle;
                }
            }


            // Convex override
            if (partNode["cx"] != null)
            {
                thingPart.convex = partNode["cx"].AsInt == 1;
            }


            mesh.RecalculateNormals(smoothingAngle);

            bool usePreciseCollision = mesh.vertices.Length <= VertexMover.maxVertexCountForPreciseCollisions;
            if (usePreciseCollision)
            {
                RecreateColliderAndChangeTypeIfNeeded(thingPart, mesh);
            }
            mesh.RecalculateTangents();

        }
        else if (partNode["cx"] != null)
        {
            thingPart.convex = partNode["cx"].AsInt == 1;
            MeshCollider meshCollider = thingPart.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                meshCollider.convex = (bool)thingPart.convex;
            }

        }
    }

    public static void RecreateColliderAndChangeTypeIfNeeded(ThingPart thingPart, Mesh mesh)
    {
        bool convex = true;

        MeshCollider meshCollider = thingPart.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            convex = meshCollider.convex;
        }

        GameObject.Destroy(thingPart.GetComponent<Collider>());

        meshCollider = thingPart.gameObject.AddComponent<MeshCollider>();
        if (thingPart.convex != null) { convex = (bool)thingPart.convex; }

        if (convex)
        {
            MeshFilter colliderFilter = meshCollider.GetComponent<MeshFilter>();
            if (colliderFilter != null)
            {
                int polygonCount = (int)colliderFilter.mesh.triangles.Length / 3;
                if (polygonCount > ThingManager.maxAllowedPolygonCountForConvex)
                {
                    meshCollider.inflateMesh = true;
                }
                /* Did not prevent error yet, so needs more investigation
                else if (polygonCount < ThingManager.minAllowedPolygonCountForConvex) {
                    convex = false;
                }
                */
            }
        }

        meshCollider.convex = convex;

        mesh.RecalculateBounds();
    }

    static void ConvertDeprecatedHideEffectShapes(Thing thing, ThingPart thingPart)
    {
        if (thing.hideEffectShapes_deprecated)
        {
            bool isEffectShape =
                    thingPart.materialType == MaterialTypes.Particles ||
                    thingPart.materialType == MaterialTypes.ParticlesBig ||
                    thingPart.materialType == MaterialTypes.PointLight ||
                    thingPart.materialType == MaterialTypes.SpotLight ||
                    thingPart.particleSystemType != ParticleSystemType.None ||
                    thingPart.useTextureAsSky;
            if (isEffectShape)
            {
                thingPart.invisible = true;
                thingPart.uncollidable = true;
            }
        }
    }

    static void AddStaticReflectionAndContinuationParts(Thing thing)
    {
        Component[] thingParts = thing.gameObject.GetComponentsInChildren<ThingPart>();
        foreach (ThingPart thingPart in thingParts)
        {
            thingPart.CreateMyReflectionPartsIfNeeded(thingPart.name);
            thingPart.HandleMyReflectionParts();

            thingPart.CreateMyAutoContinuationPartsIfNeeded(thingPart.name);
            thingPart.HandleMyAutoContinuationParts();
        }
    }

    static TextureType GetLastTextureType()
    {
        TextureType textureType = TextureType.None;
        foreach (TextureType thisTextureType in Enum.GetValues(typeof(TextureType)))
        {
            textureType = thisTextureType;
        }
        return textureType;
    }

    static void SetTextureFromStateProperties(ThingPart thingPart, ThingPartState state, JSONNode propertyNode, int index)
    {
        state.textureColors[index] = JsonHelper.GetColor(propertyNode["c"]);
        state.textureProperties[index] = new Dictionary<TextureProperty, float>();

        Managers.thingManager.SetTexturePropertiesToDefault(state.textureProperties[index],
                thingPart.textureTypes[index]);

        foreach (KeyValuePair<TextureProperty, string> item in Managers.thingManager.texturePropertyAbbreviations)
        {
            if (propertyNode[item.Value] != null)
            {
                state.textureProperties[index][item.Key] = propertyNode[item.Value].AsFloat;
            }
        }
    }

    static void SetParticleSystemFromStateProperties(ThingPartState state, JSONNode propertyNode)
    {
        state.particleSystemColor = JsonHelper.GetColor(propertyNode["c"]);
        state.particleSystemProperty = new Dictionary<ParticleSystemProperty, float>();
        foreach (KeyValuePair<ParticleSystemProperty, string> item in Managers.thingManager.particleSystemPropertyAbbreviations)
        {
            if (propertyNode[item.Value] != null)
            {
                state.particleSystemProperty[item.Key] = propertyNode[item.Value].AsFloat;
            }
        }
    }

    static bool ContainsPartAttributesWhichShouldPreventMerging(JSONNode attributes)
    {
        bool contains = false;
        if (attributes != null)
        {
            for (int i = 0; i < attributes.Count; i++)
            {
                ThingPartAttribute attribute = (ThingPartAttribute)attributes[i].AsInt;
                if (Array.IndexOf(partAttributesWhichCanBeMerged, attribute) == -1)
                {
                    contains = true;
                    break;
                }
            }
        }
        return contains;
    }

    static bool GetContainsCollisionListener(List<StateListener> listeners)
    {
        bool doesContain = false;
        foreach (StateListener listener in listeners)
        {
            switch (listener.eventType)
            {
                case StateListener.EventType.OnTouches:
                case StateListener.EventType.OnTouchEnds:
                case StateListener.EventType.OnConsumed:
                case StateListener.EventType.OnBlownAt:
                case StateListener.EventType.OnHitting:
                    doesContain = true;
                    break;
            }
            if (doesContain) { break; }
        }
        return doesContain;
    }

    static bool GetContainsBehaviorScriptVariables(List<StateListener> listeners)
    {
        bool doesContain = false;
        foreach (StateListener listener in listeners)
        {
            doesContain =
                    listener.eventType == StateListener.EventType.OnVariableChange ||
                    listener.variableOperations != null ||
                    listener.whenIsData != null;
            if (doesContain) { break; }
        }
        return doesContain;
    }

    static bool GetContainsTextCommands(List<StateListener> listeners)
    {
        bool doesContain = false;
        foreach (StateListener listener in listeners)
        {
            doesContain = listener.setText != null;
            if (doesContain) { break; }
        }
        return doesContain;
    }

    static bool GetContainsAttractRepelOrLoopSurroundCommands(List<StateListener> listeners)
    {
        bool doesContain = false;
        foreach (StateListener listener in listeners)
        {
            doesContain = listener.attractThingsSettings != null ||
                    (listener.startLoopSoundName != null && listener.loopSpatialBlend < 1f);
            if (doesContain) { break; }
        }
        return doesContain;
    }

    static bool GetContainsTurnCommands(List<StateListener> listeners)
    {
        bool doesContain = false;
        foreach (StateListener listener in listeners)
        {
            doesContain =
                    listener.turn != null ||
                    listener.turnThing != null ||
                    listener.turnSubThing != null;
            if (doesContain) { break; }
        }
        return doesContain;
    }

    static string GetAddSharedMaterialsId(ThingPart thingPart, Renderer thingPartRenderer)
    {
        string id = "";
        if (thingPart.materialType != MaterialTypes.None)
        {
            id += thingPart.materialType + "_";
        }
        if (thingPart.particleSystemType != ParticleSystemType.None)
        {
            id += thingPart.particleSystemType + "_p";
        }
        id += GetNormalizedColorString(thingPart.states[0].color) + "_";

        if (thingPart.textureTypes[0] != TextureType.None)
        {
            ThingPartState state = thingPart.states[0];
            for (int i = 0; i < state.textureProperties.Length; i++)
            {
                if (state.textureProperties[i] != null)
                {
                    id += GetNormalizedColorString(state.textureColors[i]) + "_";
                    foreach (KeyValuePair<TextureProperty, float> item in state.textureProperties[i])
                    {
                        string thisPart = Managers.thingManager.texturePropertyAbbreviations[item.Key] + item.Value.ToString();
                        thisPart = thisPart.Replace("0.", ".");
                        id += thisPart + "_";
                    }
                }
            }
        }

        if (!Managers.thingManager.sharedMaterials.ContainsKey(id))
        {
            Material newMaterial = new Material(thingPartRenderer.material);
            newMaterial.color = thingPart.states[0].color;
            newMaterial.name = id;
            Managers.thingManager.sharedMaterials.Add(id, newMaterial);
        }

        return id;
    }

    public static string GetNormalizedColorString(Color thisColor)
    {
        return GetNormalizedColorStringPart(thisColor.r) + "_" +
               GetNormalizedColorStringPart(thisColor.g) + "_" +
               GetNormalizedColorStringPart(thisColor.b);
    }

    static string GetNormalizedColorStringPart(float number)
    {
        const float max = 255f;
        int numberInt = (int)Mathf.Round(Mathf.Clamp(number * max, 0f, max));
        return numberInt.ToString();
    }

    static string GetAddSharedMeshesId(Thing thing, string materialId, CombineInstance[] combine, int combineVertexCount)
    {
        const int extraBuffer = 1000;
        const int maxVerticesFor16bit = 65535 - extraBuffer;

        string id = thing.thingId + "_" +
                (thing.suppressScriptsAndStates ? "s_" : "") +
                materialId;

        if (!Managers.thingManager.sharedMeshes.ContainsKey(id))
        {
            Mesh newMesh = new Mesh();
            const bool mergeSubMeshes = true;
            const bool useMatrices = true;
            if (combineVertexCount > maxVerticesFor16bit)
            {
                newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            newMesh.CombineMeshes(combine, mergeSubMeshes, useMatrices);
            Managers.thingManager.sharedMeshes.Add(id, newMesh);
        }

        return id;
    }

    static void MergeMeshesToOptimize(GameObject thing, Thing thingScript, bool keepPreciseCollider)
    {
        // Goes through all parts of a thing, then combines same-material
        // parts into single meshes for less Unity gameObjects, as those create
        // an apparent CPU overhead.

        Vector3 oldPosition = thing.transform.position;
        Vector3 oldScale = thing.transform.localScale;
        Quaternion oldRotation = thing.transform.rotation;

        thing.transform.position = Vector3.zero;
        thing.transform.localScale = Vector3.one;
        thing.transform.rotation = Quaternion.identity;

        bool didFindNewMaterial = true;
        while (didFindNewMaterial)
        {
            didFindNewMaterial = false;
            string materialIdFound = "";

            TextureType[] textureTypesFound = new TextureType[] { TextureType.None, TextureType.None };
            Color[] textureColorsFound = new Color[] { Color.white, Color.white };
            Dictionary<TextureProperty, float>[] texturePropertiesFound =
                    new Dictionary<TextureProperty, float>[] { null, null };

            int thisCount = 0;
            foreach (Transform child in thing.transform)
            {
                if (child.name == mergableName && child.CompareTag("ThingPart"))
                {
                    Renderer checkRenderer = child.GetComponent<Renderer>();
                    if (Managers.thingManager.sharedMaterials.ContainsKey(checkRenderer.sharedMaterial.name) &&
                            (!didFindNewMaterial || checkRenderer.sharedMaterial.name == materialIdFound))
                    {

                        if (!didFindNewMaterial)
                        {
                            didFindNewMaterial = true;
                            materialIdFound = checkRenderer.sharedMaterial.name;

                            ThingPart thingPart = child.GetComponent<ThingPart>();
                            if (thingPart != null && thingPart.textureTypes[0] != TextureType.None)
                            {
                                ThingPartState state = thingPart.states[0];
                                for (int index = 0; index < state.textureProperties.Length; index++)
                                {
                                    textureTypesFound[index] = thingPart.textureTypes[index];
                                    textureColorsFound[index] = new Color(
                                            state.textureColors[index].r,
                                            state.textureColors[index].g,
                                            state.textureColors[index].b);
                                    texturePropertiesFound[index] =
                                            Managers.thingManager.CloneTextureProperty(state.textureProperties[index]);
                                }
                            }
                        }
                        thisCount++;
                    }
                }
            }

            if (didFindNewMaterial)
            {
                MaterialTypes materialTypeFound = MaterialTypes.None;
                Color colorFound = Color.white;

                string thisId = thingScript.thingId + "_" +
                        (thingScript.suppressScriptsAndStates ? "s_" : "") +
                        materialIdFound;
                bool meshCombineAlreadyExists = materialIdFound != "" &&
                        Managers.thingManager.sharedMeshes.ContainsKey(thisId);

                CombineInstance[] combine = new CombineInstance[thisCount];
                int combineVertexCount = 0;
                int i = 0;
                foreach (Transform child in thing.transform)
                {
                    if (child.name == mergableName && child.CompareTag("ThingPart") &&
                            child.name != Universe.objectNameIfAlreadyDestroyed)
                    {
                        Renderer thisRenderer = child.GetComponent<Renderer>();
                        if (thisRenderer.sharedMaterial.name == materialIdFound)
                        {
                            ThingPart oldThingPart = child.GetComponent<ThingPart>();
                            if (oldThingPart != null)
                            {
                                materialTypeFound = oldThingPart.materialType;
                                colorFound = oldThingPart.states[0].color;
                            }

                            MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                            if (!meshCombineAlreadyExists)
                            {
                                combine[i].mesh = meshFilter.sharedMesh;
                                combineVertexCount += meshFilter.sharedMesh.vertexCount;
                                combine[i].transform = meshFilter.transform.localToWorldMatrix;
                            }
                            GameObject.Destroy(meshFilter);
                            Misc.Destroy(child.gameObject);
                            i++;
                        }
                    }
                }

                GameObject thingPart = (GameObject)UnityEngine.Object.Instantiate(Managers.thingManager.thingPartBases[(int)ThingPartBase.Pyramid]);

                thingPart.transform.parent = thing.transform;
                thingPart.transform.localScale = Vector3.one;
                thingPart.transform.position = Vector3.zero;
                thingPart.transform.rotation = Quaternion.identity;
                thingPart.tag = "ThingPart";

                MeshFilter filter = thingPart.GetComponent<MeshFilter>();

                string meshId = GetAddSharedMeshesId(thingScript, materialIdFound, combine, combineVertexCount);
                filter.sharedMesh = Managers.thingManager.sharedMeshes[meshId];

                thingPart.name = "Merge_" + meshId + "_" + materialIdFound;

                MeshCollider collider = thingPart.GetComponent<MeshCollider>();
                collider.convex = false;
                collider.sharedMesh = Managers.thingManager.sharedMeshes[meshId];

                MeshFilter colliderFilter = collider.GetComponent<MeshFilter>();
                int polygonCount = 0;
                if (colliderFilter != null)
                {
                    polygonCount = (int)colliderFilter.mesh.triangles.Length / 3;
                }

                if (polygonCount <= ThingManager.maxAllowedPolygonCountForConvex - 1 && !keepPreciseCollider)
                {
                    collider.convex = true;
                }

                Renderer thingPartRenderer = thingPart.GetComponent<Renderer>();
                ThingPart thingPartScript = thingPart.GetComponent<ThingPart>();
                thingPartScript.isInInventoryOrDialog = thingScript.isInInventoryOrDialog;
                thingPartRenderer.sharedMaterial = Managers.thingManager.sharedMaterials[materialIdFound];
                if (thingScript.avoidCastShadow)
                {
                    thingPartRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                if (thingScript.avoidReceiveShadow)
                {
                    thingPartRenderer.receiveShadows = false;
                }

                thingPartScript.material = thingPartRenderer.sharedMaterial;
                thingPartScript.materialType = materialTypeFound;
                thingPartScript.states[0].color = colorFound;

                if (textureTypesFound[0] != TextureType.None)
                {
                    thingPartScript.textureTypes = textureTypesFound;
                    ThingPartState state = thingPartScript.states[0];
                    for (int index = 0; index < state.textureProperties.Length; index++)
                    {
                        state.textureColors[index] = textureColorsFound[index];
                        state.textureProperties[index] = texturePropertiesFound[index];
                    }
                    thingPartScript.UpdateTextures(true);
                }

                thingPartScript.UpdateParticleSystem();
            }
        }

        thing.transform.position = oldPosition;
        thing.transform.localScale = oldScale;
        thing.transform.rotation = oldRotation;
    }

    static void ExpandThingIncludedNameIdsFromJson(Thing thing, JSONNode jsonNameIds)
    {
        if (jsonNameIds != null)
        {
            for (int nameIdsI = 0; nameIdsI < jsonNameIds.Count; nameIdsI++)
            {
                thing.includedNameIds.Add(jsonNameIds[nameIdsI][0], jsonNameIds[nameIdsI][1]);
            }
        }
    }

    static void ExpandThingAttributeFromJson(Thing thing, JSONNode jsonAttributes)
    {
        if (jsonAttributes != null)
        {
            for (int i = 0; i < jsonAttributes.Count; i++)
            {
                switch ((ThingAttribute)jsonAttributes[i].AsInt)
                {
                    case ThingAttribute.isClonable: thing.isClonable = true; break;
                    case ThingAttribute.isHoldable: thing.isHoldable = true; break;
                    case ThingAttribute.remainsHeld: thing.remainsHeld = true; break;
                    case ThingAttribute.isClimbable: thing.isClimbable = true; break;
                    case ThingAttribute.isPassable: thing.isPassable = true; break;
                    case ThingAttribute.isUnwalkable: thing.isUnwalkable = true; break;
                    case ThingAttribute.doSnapAngles: thing.doSnapAngles = true; break;
                    case ThingAttribute.doSoftSnapAngles: thing.doSoftSnapAngles = true; break;
                    case ThingAttribute.hideEffectShapes_deprecated: thing.hideEffectShapes_deprecated = true; break;
                    case ThingAttribute.isBouncy: thing.isBouncy = true; break;
                    case ThingAttribute.doShowDirection: thing.doShowDirection = true; break;
                    case ThingAttribute.keepPreciseCollider: thing.keepPreciseCollider = true; break;
                    case ThingAttribute.doesFloat: thing.doesFloat = true; break;
                    case ThingAttribute.doesShatter: thing.doesShatter = true; break;
                    case ThingAttribute.isSticky: thing.isSticky = true; break;
                    case ThingAttribute.isSlidy: thing.isSlidy = true; break;
                    case ThingAttribute.doSnapPosition: thing.doSnapPosition = true; break;
                    case ThingAttribute.amplifySpeech: thing.amplifySpeech = true; break;
                    case ThingAttribute.benefitsFromShowingAtDistance: thing.benefitsFromShowingAtDistance = true; break;
                    case ThingAttribute.scaleAllParts: thing.scaleAllParts = true; break;
                    case ThingAttribute.doAlwaysMergeParts: thing.doAlwaysMergeParts = true; break;
                    case ThingAttribute.addBodyWhenAttached: thing.addBodyWhenAttached = true; break;
                    case ThingAttribute.hasSurroundSound: thing.hasSurroundSound = true; break;
                    case ThingAttribute.canGetEventsWhenStateChanging: thing.canGetEventsWhenStateChanging = true; break;
                    case ThingAttribute.replacesHandsWhenAttached: thing.replacesHandsWhenAttached = true; break;
                    case ThingAttribute.mergeParticleSystems: thing.mergeParticleSystems = true; break;
                    case ThingAttribute.isSittable: thing.isSittable = true; break;
                    case ThingAttribute.smallEditMovements: thing.smallEditMovements = true; break;
                    case ThingAttribute.scaleEachPartUniformly: thing.scaleEachPartUniformly = true; break;
                    case ThingAttribute.snapAllPartsToGrid: thing.snapAllPartsToGrid = true; break;
                    case ThingAttribute.invisibleToUsWhenAttached: thing.invisibleToUsWhenAttached = true; break;
                    case ThingAttribute.replaceInstancesInArea: thing.replaceInstancesInArea = true; break;
                    case ThingAttribute.addBodyWhenAttachedNonClearing: thing.addBodyWhenAttachedNonClearing = true; break;
                    case ThingAttribute.avoidCastShadow: thing.avoidCastShadow = true; break;
                    case ThingAttribute.avoidReceiveShadow: thing.avoidReceiveShadow = true; break;
                    case ThingAttribute.omitAutoSounds: thing.omitAutoSounds = true; break;
                    case ThingAttribute.omitAutoHapticFeedback: thing.omitAutoHapticFeedback = true; break;
                    case ThingAttribute.keepSizeInInventory: thing.keepSizeInInventory = true; break;
                    case ThingAttribute.autoAddReflectionPartsSideways: thing.autoAddReflectionPartsSideways = true; break;
                    case ThingAttribute.autoAddReflectionPartsVertical: thing.autoAddReflectionPartsVertical = true; break;
                    case ThingAttribute.autoAddReflectionPartsDepth: thing.autoAddReflectionPartsDepth = true; break;
                    case ThingAttribute.activeEvenInInventory: thing.activeEvenInInventory = true; break;
                    case ThingAttribute.stricterPhysicsSyncing: thing.stricterPhysicsSyncing = true; break;
                    case ThingAttribute.removeOriginalWhenGrabbed: thing.removeOriginalWhenGrabbed = true; break;
                    case ThingAttribute.persistWhenThrownOrEmitted: thing.persistWhenThrownOrEmitted = true; break;
                    case ThingAttribute.invisible: thing.invisible = true; break;
                    case ThingAttribute.uncollidable: thing.uncollidable = true; break;
                    case ThingAttribute.movableByEveryone: thing.movableByEveryone = true; break;
                    case ThingAttribute.isNeverClonable: thing.isNeverClonable = true; break;
                    case ThingAttribute.floatsOnLiquid: thing.floatsOnLiquid = true; break;
                    case ThingAttribute.invisibleToDesktopCamera: thing.invisibleToDesktopCamera = true; break;
                    case ThingAttribute.personalExperience: thing.personalExperience = true; break;
                }
            }

            if (thing.suppressHoldable)
            {
                thing.isHoldable = false;
                thing.remainsHeld = false;
            }
        }
    }

    static void ExpandIncludedSubThingInvertAttributeFromJson(IncludedSubThing subThing, JSONNode jsonAttributes)
    {
        if (jsonAttributes != null)
        {
            for (int i = 0; i < jsonAttributes.Count; i++)
            {
                switch ((ThingAttribute)jsonAttributes[i].AsInt)
                {
                    case ThingAttribute.isHoldable: subThing.invert_isHoldable = true; break;
                    case ThingAttribute.invisible: subThing.invert_invisible = true; break;
                    case ThingAttribute.uncollidable: subThing.invert_uncollidable = true; break;
                }
            }
        }
    }

    static void ExpandThingPartAttributeFromJson(ThingPart thingPart, JSONNode jsonAttributes)
    {
        if (jsonAttributes != null)
        {
            for (int i = 0; i < jsonAttributes.Count; i++)
            {
                switch ((ThingPartAttribute)jsonAttributes[i].AsInt)
                {
                    case ThingPartAttribute.offersScreen: thingPart.offersScreen = true; break;
                    case ThingPartAttribute.videoScreenHasSurroundSound: thingPart.videoScreenHasSurroundSound = true; break;
                    case ThingPartAttribute.videoScreenLoops: thingPart.videoScreenLoops = true; break;
                    case ThingPartAttribute.videoScreenIsDirectlyOnMesh: thingPart.videoScreenIsDirectlyOnMesh = true; break;
                    case ThingPartAttribute.isVideoButton: thingPart.isVideoButton = true; break;
                    case ThingPartAttribute.scalesUniformly: thingPart.scalesUniformly = true; break;
                    case ThingPartAttribute.isLiquid: thingPart.isLiquid = true; break;
                    case ThingPartAttribute.offersSlideshowScreen: thingPart.offersSlideshowScreen = true; break;
                    case ThingPartAttribute.isSlideshowButton: thingPart.isSlideshowButton = true; break;
                    case ThingPartAttribute.isCamera: thingPart.isCamera = true; break;
                    case ThingPartAttribute.isCameraButton: thingPart.isCameraButton = true; break;
                    case ThingPartAttribute.isFishEyeCamera: thingPart.isFishEyeCamera = true; break;
                    case ThingPartAttribute.useUnsoftenedAnimations: thingPart.useUnsoftenedAnimations = true; break;
                    case ThingPartAttribute.invisible: thingPart.invisible = true; break;
                    case ThingPartAttribute.uncollidable: thingPart.uncollidable = true; break;
                    case ThingPartAttribute.isUnremovableCenter: thingPart.isUnremovableCenter = true; break;
                    case ThingPartAttribute.omitAutoSounds: thingPart.omitAutoSounds = true; break;
                    case ThingPartAttribute.doSnapTextureAngles: thingPart.doSnapTextureAngles = true; break;
                    case ThingPartAttribute.textureScalesUniformly: thingPart.textureScalesUniformly = true; break;
                    case ThingPartAttribute.avoidCastShadow: thingPart.avoidCastShadow = true; break;
                    case ThingPartAttribute.looselyCoupledParticles: thingPart.looselyCoupledParticles = true; break;
                    case ThingPartAttribute.textAlignCenter: thingPart.textAlignCenter = true; break;
                    case ThingPartAttribute.textAlignRight: thingPart.textAlignRight = true; break;
                    case ThingPartAttribute.isAngleLocker: thingPart.isAngleLocker = true; break;
                    case ThingPartAttribute.isPositionLocker: thingPart.isPositionLocker = true; break;
                    case ThingPartAttribute.isLocked: thingPart.isLocked = true; break;
                    case ThingPartAttribute.avoidReceiveShadow: thingPart.avoidReceiveShadow = true; break;
                    case ThingPartAttribute.isImagePasteScreen: thingPart.isImagePasteScreen = true; break;
                    case ThingPartAttribute.allowBlackImageBackgrounds: thingPart.allowBlackImageBackgrounds = true; break;
                    case ThingPartAttribute.useTextureAsSky: thingPart.useTextureAsSky = true; break;
                    case ThingPartAttribute.stretchSkydomeSeam: thingPart.stretchSkydomeSeam = true; break;
                    case ThingPartAttribute.subThingsFollowDelayed: thingPart.subThingsFollowDelayed = true; break;
                    case ThingPartAttribute.hasReflectionPartSideways: thingPart.hasReflectionPartSideways = true; break;
                    case ThingPartAttribute.hasReflectionPartVertical: thingPart.hasReflectionPartVertical = true; break;
                    case ThingPartAttribute.hasReflectionPartDepth: thingPart.hasReflectionPartDepth = true; break;
                    case ThingPartAttribute.videoScreenFlipsX: thingPart.videoScreenFlipsX = true; break;
                    case ThingPartAttribute.persistStates: thingPart.persistStates = true; break;
                    case ThingPartAttribute.isDedicatedCollider: thingPart.isDedicatedCollider = true; break;
                    case ThingPartAttribute.personalExperience: thingPart.personalExperience = true; break;
                    case ThingPartAttribute.invisibleToUsWhenAttached: thingPart.invisibleToUsWhenAttached = true; break;
                    case ThingPartAttribute.lightOmitsShadow: thingPart.lightOmitsShadow = true; break;
                    case ThingPartAttribute.showDirectionArrowsWhenEditing: thingPart.showDirectionArrowsWhenEditing = true; break;
                }
            }
        }
    }

}