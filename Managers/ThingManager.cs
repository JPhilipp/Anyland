using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using System.Linq;

public class ThingManager : MonoBehaviour, IGameManager
{
    // Managing all related to Things.

    public ManagerStatus status { get; private set; }
    public string failMessage { get; private set; }

    public const int currentThingVersion = 9;
    public const int maxThingsInArea = 5000;
    public const float subThingsSmoothDelay = 0.05f;

    public GameObject thingGameObject;

    public GameObject placements { get; private set; }
    public Transform controllables { get; private set; }

    public PhysicMaterial bouncyMaterial;
    public PhysicMaterial slidyMaterial;
    public PhysicMaterial bouncySlidyMaterial;
    public PhysicMaterial controllableMaterial;

    public GameObject[] thingPartBases;
    public Dictionary<string, Material> sharedMaterials = new Dictionary<string, Material>();
    public Dictionary<string, Material> sharedTextureMaterials = new Dictionary<string, Material>();
    public Dictionary<string, Mesh> sharedMeshes = new Dictionary<string, Mesh>();
    public Transform thrownOrEmittedThingsParent { get; private set; }
    ThingPartBase[] thingPartBasesSupportingReflectionParts;
    ThingPartBase[] thingPartBasesSupportingLimitedReflectionParts;

    public ThingDefinitionCache thingDefinitionCache;
    public HeldThingsRegistrar heldThingsRegistrar;

    public int statsThingsInArea = 0;
    public int statsThingsAroundPosition = 0;
    public int statsThingPartsAroundPosition = 0;

    public bool mergeThings = true;

    public const string steamScreenshotPrefix = "https://steamuserimages-a.akamaihd.net/ugc/";
    public const string steamScreenshotPrefixHttp = "http://steamuserimages-a.akamaihd.net/ugc/";
    public const string coverImagePrefix = "http://www.coverbrowser.com/image/";
    public const string coverCacheImagePrefix = "http://cache.coverbrowser.com/image/";
    public const int minAllowedPolygonCountForConvex = 3;
    public const int maxAllowedPolygonCountForConvex = 255;

    int vertexTextureCounter = 0;

    ThingPartAttribute[] partAttributesWhichCanBeMerged;

    public Dictionary<TextureProperty, string> texturePropertyAbbreviations;
    Dictionary<TextureProperty, float> texturePropertyDefault;
    Dictionary<TextureType, bool> textureTypeWithOnlyAlphaSetting;
    Dictionary<TextureType, bool> algorithmTextureTypes;
    Dictionary<TextureType, int> textureExtraParamsNumber;
    public Dictionary<TextureType, float> textureAlphaCaps;

    public Dictionary<ParticleSystemProperty, string> particleSystemPropertyAbbreviations;
    Dictionary<ParticleSystemProperty, float> particleSystemPropertyDefault;
    Dictionary<ParticleSystemType, bool> particleSystemTypeWithOnlyAlphaSetting;

    public Dictionary<ThingPartBase, int> smoothingAngles { get; private set; }

    Material outlineHighlightMaterial = null;
    Material innerGlowHighlightMaterial = null;

    public Shader shader_standard = null;
    public Shader shader_customGlow = null;
    public Shader shader_customUnshaded = null;
    public Shader shader_customInversion = null;
    public Shader shader_customBrightness = null;
    public Shader shader_customTransparentGlow = null;
    public Shader shader_textLit = null;
    public Shader shader_textEmissive = null;

    public void Startup()
    {
        status = ManagerStatus.Initializing;

        CacheShaders();
        InitSettings();

        thingDefinitionCache = gameObject.AddComponent<ThingDefinitionCache>();
        heldThingsRegistrar = gameObject.AddComponent<HeldThingsRegistrar>();

        bouncyMaterial = (PhysicMaterial)Resources.Load("Materials/BouncyMaterial");
        slidyMaterial = (PhysicMaterial)Resources.Load("Materials/SlidyMaterial");
        bouncySlidyMaterial = (PhysicMaterial)Resources.Load("Materials/BouncySlidyMaterial");
        controllableMaterial = (PhysicMaterial)Resources.Load("Materials/ControllableMaterial");

        placements = new GameObject("Placements");
        placements.tag = "Placements";

        controllables = new GameObject("Controllables").transform;

        thrownOrEmittedThingsParent =
                Managers.treeManager.GetTransform("/Universe/ThrownOrEmittedThings");

        thingGameObject = (GameObject)Resources.Load("Prefabs/Thing", typeof(GameObject));
        Thing thing = thingGameObject.GetComponent<Thing>();
        thing.version = ThingManager.currentThingVersion;

        LoadThingPartBases();
        #if UNITY_EDITOR
            // ShowThingPartBasesInfo();
        #endif

        Physics.IgnoreLayerCollision(
            LayerMask.NameToLayer("PassableObjects"),
            LayerMask.NameToLayer("IgnorePassableObjects")
        );

        Physics.IgnoreLayerCollision(
            LayerMask.NameToLayer("PassThroughEachOther"),
            LayerMask.NameToLayer("PassThroughEachOther")
        );

        status = ManagerStatus.Started;
    }

    #if UNITY_EDITOR
        void Update() {
            // if ( Input.GetKeyDown(KeyCode.E) ) { ExportAllBaseShapes(); }
        }
        
        void ExportAllBaseShapes() {
            OBJExporter exporter = new OBJExporter();
            exporter.applyPosition = false;
            string folderPath = Application.persistentDataPath + "/base-shapes-export";
            System.IO.Directory.CreateDirectory(folderPath);
            
            foreach ( ThingPartBase baseType in Enum.GetValues( typeof(ThingPartBase) ) ) {
                int number  = (int) baseType;
                string name = baseType.ToString();
                if ( name.IndexOf("Text") == -1 ) {
                    Log.Debug(number + " = " + name);
                    string path = folderPath + "/" + number + ".obj";

                    ExportBaseShape(exporter, name, thingPartBases[number], path);
                }
            }
        }
        
        void ExportBaseShape(OBJExporter exporter, string name, GameObject baseShape, string path) {
            GameObject shape = Instantiate(baseShape);
            shape.transform.position   = Vector3.zero;
            shape.transform.rotation   = Quaternion.identity;
            shape.transform.localScale = Vector3.one;
            shape.name = name;

            GameObject[] gameObjects = {shape};

            exporter.Export(path, gameObjects);
            
            Destroy(shape);
        }
    #endif

    void ShowThingPartBasesInfo()
    {
        // ShowThingPartBasesColliderVertexCount();
        // ShowThingPartBasesVertexCount();
        // ShowThingPartBasesWithOptimizedColliders();
    }

    void CacheShaders()
    {
        shader_standard = Shader.Find("Standard");
        shader_customGlow = Shader.Find("Custom/Glow");
        shader_customUnshaded = Shader.Find("Custom/Unshaded");
        shader_customInversion = Shader.Find("Custom/Inversion");
        shader_customBrightness = Shader.Find("Custom/Brightness");
        shader_customTransparentGlow = Shader.Find("Custom/AlphaSelfIllum");
        shader_textLit = Shader.Find("GUI/3D Text Shader - Lit");
        shader_textEmissive = Shader.Find("GUI/3D Text Shader - Cull Back");
    }

    void LoadThingPartBases()
    {
        thingPartBases = new GameObject[GetHighestThingPartBaseIndex() + 1];
        foreach (ThingPartBase thingPartBase in Enum.GetValues(typeof(ThingPartBase)))
        {
            string path = "ThingPartBases/" + thingPartBase.ToString();
            thingPartBases[(int)thingPartBase] = (GameObject)Resources.Load(path, typeof(GameObject));
            ThingPart thingPart = thingPartBases[(int)thingPartBase].GetComponent<ThingPart>();
            thingPart.baseType = thingPartBase;
        }
    }

    void ShowThingPartBasesVertexCount()
    {
        foreach (ThingPartBase thingPartBase in Enum.GetValues(typeof(ThingPartBase)))
        {
            MeshFilter meshFilter = thingPartBases[(int)thingPartBase].GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                int count = meshFilter.sharedMesh.vertexCount;
                if (count >= 250)
                {
                    Log.Debug(thingPartBase.ToString() + " meshFilter vertices: " + count);
                }
            }
        }
    }

    void ShowThingPartBasesColliderVertexCount()
    {
        foreach (ThingPartBase thingPartBase in Enum.GetValues(typeof(ThingPartBase)))
        {
            MeshCollider meshCollider = thingPartBases[(int)thingPartBase].GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                int count = meshCollider.sharedMesh.vertexCount;
                if (count >= 250)
                {
                    Log.Debug(thingPartBase.ToString() + " vertices: " + count);
                }
            }
        }
    }

    void ShowThingPartBasesWithOptimizedColliders()
    {
        foreach (ThingPartBase thingPartBase in Enum.GetValues(typeof(ThingPartBase)))
        {
            GameObject baseObject = thingPartBases[(int)thingPartBase];
            MeshFilter meshFilter = baseObject.GetComponent<MeshFilter>();
            MeshCollider meshCollider = baseObject.GetComponent<MeshCollider>();
            if (meshFilter != null && meshCollider != null &&
                    meshFilter.sharedMesh.vertexCount != meshCollider.sharedMesh.vertexCount)
            {

                string s = thingPartBase.ToString() + " has different collider count: " +
                        meshFilter.sharedMesh.vertexCount + " -> " + meshCollider.sharedMesh.vertexCount;
                if (meshCollider.sharedMesh.vertexCount > meshFilter.sharedMesh.vertexCount)
                {
                    s += "---> Collider has more vertices!";
                }
                Log.Debug(s);

            }
            else if (meshFilter != null && meshCollider == null)
            {
                string s = ">>> " + thingPartBase.ToString() + " has different collider type: ";
                BoxCollider boxCollider = baseObject.GetComponent<BoxCollider>();
                SphereCollider sphereCollider = baseObject.GetComponent<SphereCollider>();

                if (boxCollider != null) { s += "Box"; }
                else if (sphereCollider != null) { s += "Sphere"; }

                Log.Debug(s);
            }
        }
    }

    int GetHighestThingPartBaseIndex()
    {
        int highestIndex = 0;
        foreach (ThingPartBase thingPartBase in Enum.GetValues(typeof(ThingPartBase)))
        {
            if ((int)thingPartBase > highestIndex) { highestIndex = (int)thingPartBase; }
        }
        return highestIndex;
    }

    public Vector3 GetBaseShapeDropUpScale(ThingPartBase thingPartBase)
    {
        Vector3 vector3 = Vector3.zero;
        float? scale = null;

        switch (thingPartBase)
        {
            case ThingPartBase.JitterCube:
            case ThingPartBase.JitterSphere:
            case ThingPartBase.JitterCubeSoft:
            case ThingPartBase.JitterSphereSoft:
            case ThingPartBase.ChamferCube:
            case ThingPartBase.Spike:
            case ThingPartBase.SharpBent:
            case ThingPartBase.Pipe:
            case ThingPartBase.Pipe2:
            case ThingPartBase.Pipe3:
            case ThingPartBase.ShrinkDisk:
            case ThingPartBase.ShrinkDisk2:
                scale = 0.06f;
                break;

            case ThingPartBase.LowJitterCube:
            case ThingPartBase.LowJitterCubeSoft:
                scale = 0.04f;
                break;

            case ThingPartBase.JitterCone:
            case ThingPartBase.JitterConeSoft:
                scale = 0.07f;
                break;

            case ThingPartBase.JitterHalfCone:
            case ThingPartBase.JitterHalfConeSoft:
            case ThingPartBase.Tetrahedron:
            case ThingPartBase.HalfBowlSoft:
                scale = 0.08f;
                break;

            case ThingPartBase.Capsule:
            case ThingPartBase.QuarterBowlCube:
            case ThingPartBase.QuarterBowlCubeSoft:
            case ThingPartBase.CubeHole:
                scale = 0.075f;
                break;

            case ThingPartBase.JitterChamferCylinder:
            case ThingPartBase.JitterChamferCylinderSoft:
                scale = 0.09f;
                break;

            case ThingPartBase.HoleWall:
            case ThingPartBase.JaggyWall:
            case ThingPartBase.DirectionIndicator:
                scale = 0.045f;
                break;

            case ThingPartBase.Cylinder:
            case ThingPartBase.Ramp:
            case ThingPartBase.HalfSphere:
                scale = 0.1f;
                break;

            case ThingPartBase.Cube: scale = 0.175f; break;
            case ThingPartBase.RoundCube: scale = 0.1f; break;
            case ThingPartBase.HighPolySphere: scale = 0.225f; break;
            case ThingPartBase.Branch: scale = 0.03f; break;
            case ThingPartBase.Bubbles: scale = 0.045f; break;

            case ThingPartBase.BigDialog:
                vector3 = new Vector3(2f, 1f, 2f);
                break;

            case ThingPartBase.Quad:
                scale = 0.215f;
                break;

            case ThingPartBase.Cubeoctahedron: scale = 0.19f; break;
            case ThingPartBase.Icosahedron: scale = 0.23f; break;
            case ThingPartBase.Dodecahedron: scale = 0.22f; break;
            case ThingPartBase.Icosidodecahedron: scale = 0.24f; break;
            case ThingPartBase.Octahedron: scale = 0.24f; break;

            default:
                string baseName = thingPartBase.ToString();
                if (baseName.StartsWith("Text")) { scale = 0.003f; }
                else if (baseName.StartsWith("Gear")) { scale = 0.1f; }
                else if (baseName.StartsWith("Wheel")) { scale = 0.09f; }
                else if (baseName.StartsWith("Bowl")) { scale = 0.075f; }
                else if (baseName.StartsWith("Rocky")) { scale = 0.045f; }
                else if (baseName.StartsWith("Spikes")) { scale = 0.045f; }
                else if (baseName.StartsWith("WavyWall")) { scale = 0.045f; }
                else if (baseName.StartsWith("Drop")) { scale = 0.06f; }
                else { scale = 0.125f; }
                break;
        }

        if (scale != null)
        {
            vector3 = Misc.GetUniformVector3((float)scale);
        }

        return vector3;
    }

    //This just wraps the method in thingDefinitionCache, so we only need reference to instance here
    public IEnumerator PrimeCacheWithThingDefinitionBundleIfNeeded(List<PlacementData> placements, string areaId, string key, Action<bool> callback)
    {
        yield return thingDefinitionCache.PrimeCacheWithThingDefinitionBundleIfNeeded(placements, areaId, key, callback);
    }

    //Performance sensitive during area load!
    public void InstantiatePlacedThingViaCache(ThingRequestContext thingRequestContext, PlacementData placement)
    {
        if (String.IsNullOrEmpty(placement.Id))
        {
            throw new Exception("InstantiatePlacedThingViaCache called with placement missing placementId");
        }

        string areaIdAtStart = Managers.areaManager.currentAreaId;

        StartCoroutine(
            thingDefinitionCache.GetThingDefinition(thingRequestContext, placement.Tid, (errorString, thingDefinitionJSON) =>
            {
                bool areaChangedInMeantime = areaIdAtStart != Managers.areaManager.currentAreaId;
                if (areaChangedInMeantime) { return; }

                if (errorString != null)
                {
                    Log.Error(errorString);

                }
                else
                {
                    Assert.IsNotNull(thingDefinitionJSON, "InstantiatePlacedThingViaCache got null thingDefinitionJSON from cache manager! thingId:" + placement.Tid);

                    GameObject thingObject = (GameObject)Instantiate(thingGameObject);
                    thingObject.SetActive(false);

                    Thing thing = thingObject.GetComponent<Thing>();
                    thing.thingId = placement.thingId;
                    thing.placementId = placement.Id;
                    thing.isLocked = GetPlacementAttributeValue(placement, PlacementAttribute.Locked);
                    thing.isInvisibleToEditors = GetPlacementAttributeValue(placement, PlacementAttribute.InvisibleToEditors);

                    thing.suppressScriptsAndStates = GetPlacementAttributeValue(placement, PlacementAttribute.SuppressScriptsAndStates);
                    thing.suppressCollisions = GetPlacementAttributeValue(placement, PlacementAttribute.SuppressCollisions);
                    thing.suppressLights = GetPlacementAttributeValue(placement, PlacementAttribute.SuppressLights);
                    thing.suppressParticles = GetPlacementAttributeValue(placement, PlacementAttribute.SuppressParticles);
                    thing.suppressHoldable = GetPlacementAttributeValue(placement, PlacementAttribute.SuppressHoldable);
                    thing.suppressShowAtDistance = GetPlacementAttributeValue(placement, PlacementAttribute.SuppressShowAtDistance);

                    if (placement.distanceToShow != 0f)
                    {
                        thing.distanceToShow = placement.distanceToShow;
                    }

                    JsonToThingConverter.SetThing(thingObject, thingDefinitionJSON, isForPlacement: true,
                            initialPosition: placement.position, initialRotation: placement.rotation);

                    thing.MemorizeOriginalTransform(isOriginalPlacement: true);

                    thingObject.transform.parent = placements.transform;
                    thingObject.transform.localScale = placement.scale != 0f ?
                            Misc.GetUniformVector3(placement.scale) : Vector3.one;

                    if (thing.isInvisibleToEditors && Managers.areaManager.weAreEditorOfCurrentArea &&
                            !Our.seeInvisibleAsEditor)
                    {
                        Misc.SetAllObjectLayers(thing.gameObject, "InvisibleToOurPerson");
                    }
                }

                Managers.areaManager.DoneLoadingThisPlacement();
            })
        );
    }

    private bool GetPlacementAttributeValue(PlacementData placement, PlacementAttribute thisAttribute)
    {
        return placement.A != null && placement.A.Contains((int)thisAttribute);
    }

    public IEnumerator InstantiateThingViaCache(ThingRequestContext thingRequestContext, string thingId, Action<GameObject> callback, bool alwaysKeepThingPartsSeparate = false, bool isForPlacement = false, int layer = -1, Thing inheritSuppressAttributesThing = null)
    {
        string thingDefJSON = null;
        string getThingDefErrorString = null;
        GameObject thingObject = null;

        yield return StartCoroutine(thingDefinitionCache.GetThingDefinition(thingRequestContext, thingId,
            (errorString, definitionJSON) =>
            {
                thingDefJSON = definitionJSON;
                getThingDefErrorString = errorString;
            })
        );

        //Here we should have thingDefJSON and getThingDefErrorString populated...

        if (!String.IsNullOrEmpty(getThingDefErrorString))
        {
            Debug.LogError(getThingDefErrorString);

        }
        else
        {
            Assert.IsNotNull(thingDefJSON, "InstantiateThingViaCache got null thingDefinitionJSON from cache manager!");

            thingObject = (GameObject)Instantiate(thingGameObject);
            Thing thing = thingObject.GetComponent<Thing>();
            thing.thingId = thingId;

            if (inheritSuppressAttributesThing != null)
            {
                thing.suppressScriptsAndStates = inheritSuppressAttributesThing.suppressScriptsAndStates;
                thing.suppressCollisions = inheritSuppressAttributesThing.suppressCollisions;
                thing.suppressLights = inheritSuppressAttributesThing.suppressLights;
                thing.suppressParticles = inheritSuppressAttributesThing.suppressParticles;
                thing.suppressHoldable = inheritSuppressAttributesThing.suppressHoldable;
                thing.suppressShowAtDistance = inheritSuppressAttributesThing.suppressShowAtDistance;
            }

            JsonToThingConverter.SetThing(thingObject, thingDefJSON, alwaysKeepThingPartsSeparate,
                    isForPlacement: isForPlacement);
            if (layer > 0) { ThingManager.SetLayerForThingAndParts(thing, layer); }
        }

        if (thingObject == null)
        {
            Log.Warning("InstantiateThingViaCache returning null thing");
        }

        callback(thingObject);
    }

    public void InstantiateInventoryItemViaCache(ThingRequestContext thingRequestContext, InventoryItemData inventoryItem, Transform inventoryBoxTransform, bool isSearchResult = false, bool useFallingEffect = false)
    {
        StartCoroutine(
            thingDefinitionCache.GetThingDefinition(thingRequestContext, inventoryItem.thingId, (errorString, thingDefinitionJSON) =>
            {
                if (errorString != null)
                {
                    Debug.LogError(errorString);

                }
                else
                {
                    Assert.IsNotNull(thingDefinitionJSON, "InstantiateInventoryItemViaCache got null thingDefinitionJSON from cache manager!");

                    GameObject thingObject = (GameObject)Instantiate(thingGameObject);

                    Thing thing = thingObject.GetComponent<Thing>();
                    thing.thingId = inventoryItem.thingId;
                    thing.isInInventoryOrDialog = true;
                    thing.isInInventory = true;

                    JsonToThingConverter.SetThing(thingObject, thingDefinitionJSON);

                    float maxScale = isSearchResult ? 0.075f : 0.1f;
                    thingObject.transform.localScale = thing.keepSizeInInventory && !isSearchResult ?
                            Vector3.one : GetAppropriateDownScaleForThing(thingObject, maxScale: maxScale);

                    thingObject.transform.parent = inventoryBoxTransform;
                    thingObject.transform.localPosition = inventoryItem.position;
                    thingObject.transform.localEulerAngles = inventoryItem.rotation;

                    if (useFallingEffect)
                    {
                        thing.localTarget = inventoryItem.position;
                        float distance = UnityEngine.Random.Range(5f, 20f);
                        thingObject.transform.localPosition = new Vector3(
                            inventoryItem.position.x,
                            inventoryItem.position.y + distance,
                            inventoryItem.position.z + distance
                        );
                    }

                    thingObject.GetComponent<Thing>().MemorizeOriginalTransform();
                }
            })
        );
    }

    public void InstantiateThingOnDialogViaCache(ThingRequestContext thingRequestContext, string thingId, Transform fundament, Vector3 position, float scale = 0.035f, bool allowGrabbing = false, bool useDefaultRotation = false, float rotationX = 0f, float rotationY = 0f, float rotationZ = 0f, bool isGift = false, bool isNewGift = false)
    {
        StartCoroutine(
            thingDefinitionCache.GetThingDefinition(thingRequestContext, thingId, (errorString, thingDefinitionJSON) =>
            {
                if (errorString != null)
                {
                    Debug.LogError(errorString);
                }
                else
                {
                    Assert.IsNotNull(thingDefinitionJSON, "InstantiateThingOnDialogViaCache got null thingDefinitionJSON from cache manager!");

                    GameObject thingObject = (GameObject)Instantiate(thingGameObject);

                    Thing thing = thingObject.GetComponent<Thing>();
                    thing.thingId = thingId;
                    thing.isInInventoryOrDialog = true;
                    thing.isGiftInDialog = isGift;

                    JsonToThingConverter.SetThing(thingObject, thingDefinitionJSON);

                    thingObject.transform.parent = fundament;
                    thingObject.transform.localPosition = position;

                    if (scale == 1f)
                    {
                        thingObject.transform.localScale = Vector3.one;
                    }
                    else
                    {
                        thingObject.transform.localScale = GetAppropriateDownScaleForThing(
                                thingObject, scale, avoidUpscalingSmallThings: isGift);
                    }

                    thingObject.transform.localRotation = Quaternion.identity;
                    if (rotationX != 0f || rotationY != 0f || rotationZ != 0f)
                    {
                        thingObject.transform.localEulerAngles = new Vector3(rotationX, rotationY, rotationZ);
                    }
                    else if (!useDefaultRotation)
                    {
                        thingObject.transform.Rotate(new Vector3(90f, 0f, 0f));
                        thingObject.transform.Rotate(new Vector3(0f, -45f, 0f));
                    }
                    thingObject.GetComponent<Thing>().MemorizeOriginalTransform();

                    if (allowGrabbing)
                    {
                        thing.tag = "GrabbableDialogThingThumb";
                    }
                    else
                    {
                        thing.tag = "DialogThingThumb";
                        thing.isHoldable = false;
                        thing.remainsHeld = false;

                        if (!isGift)
                        {
                            Component[] components = thing.GetComponentsInChildren(typeof(Collider), true);
                            foreach (Collider collider in components)
                            {
                                collider.enabled = false;
                            }
                        }
                    }

                    if (isNewGift)
                    {
                        Effects.SpawnNewCreationSparkles(thingObject);
                        Managers.soundManager.Play("seeingNewGift", thingObject.transform, 0.4f);
                    }
                    else if (isGift)
                    {
                        Sound sound = new Sound();
                        sound.name = "goblet ding sparkle";
                        sound.volume = 0.03f;
                        Managers.soundLibraryManager.Play(thingObject.transform.position, sound);
                    }
                }
            })
        );
    }

    public void GetThingInfo(string thingId, Action<ThingInfo> callback)
    {
        StartCoroutine(Managers.serverManager.GetThingInfo(thingId, (response) =>
        {
            if (response.error == null)
            {
                if (response.thingInfo != null && string.IsNullOrEmpty(response.thingInfo.name))
                {
                    response.thingInfo.name = CreationHelper.thingDefaultName;
                }
                callback(response.thingInfo);
            }
            else
            {
                Log.Error(response.error);
            }
        }));
    }

    public void GetPlacementInfo(string areaId, string placementId, Action<PlacementInfo> callback)
    {
        //Catch null placementIs sometimes getting to server.
        if (string.IsNullOrEmpty(placementId))
        {
            Managers.errorManager.BeepError();
            Log.Error("GetPlacementInfo called with null placementId");
            callback(new PlacementInfo()); //Return empty object
            return;
        }

        StartCoroutine(Managers.serverManager.GetPlacementInfo(areaId, placementId, (response) =>
        {
            if (response.error == null)
            {
                callback(response.placementInfo);
            }
            else
            {
                Log.Error(response.error);
            }
        }));
    }

    public void GetThingFlagStatus(string thingId, Action<Boolean> callback)
    {
        StartCoroutine(Managers.serverManager.GetThingFlag(thingId, (response) =>
        {
            if (response.error == null)
            {
                callback(response.isFlagged);
            }
            else
            {
                Log.Error(response.error);
            }
        }));
    }


    public void ToggleThingFlag(string thingId, Action<Boolean> callback)
    {
        StartCoroutine(Managers.serverManager.ToggleThingFlag(thingId, (response) =>
        {
            if (response.error == null)
            {
                callback(response.isFlagged);
            }
            else
            {
                Log.Error(response.error);
            }
        }));
    }

    public void SetThingUnlisted(string thingId, bool isUnlisted, Action<bool> callback)
    {
        StartCoroutine(Managers.serverManager.SetThingUnlisted(thingId, isUnlisted, (response) =>
        {
            if (response.error != null)
            {
                Log.Error("response.error");
            }
            callback(response.error == null);
        }));
    }

    public void SetThingTags(string thingId, List<string> tagsToAdd, List<string> tagsToRemove, Action<bool> callback)
    {
        StartCoroutine(Managers.serverManager.SetThingTags(thingId, tagsToAdd, tagsToRemove, (response) =>
        {
            if (response.error == null)
            {
                if (Managers.personManager.ourPerson != null)
                {
                    Managers.personManager.ourPerson.thingTagCount += tagsToAdd.Count;
                    Managers.personManager.ourPerson.thingTagCount -= tagsToRemove.Count;
                }
            }
            else
            {
                Log.Error("response.error");
            }
            callback(response.error == null);
        }));
    }

    public void GetThingTags(string thingId, Action<List<ThingTagInfo>> callback)
    {
        StartCoroutine(Managers.serverManager.GetThingTags(thingId, (response) =>
        {
            if (response.error != null)
            {
                Log.Error("response.error");
            }
            callback(response.tags);
        }));
    }

    public void UnloadPlacements()
    {
        foreach (Transform placement in placements.transform)
        {
            Misc.Destroy(placement.gameObject);
        }
        sharedMaterials = new Dictionary<string, Material>();
        sharedTextureMaterials = new Dictionary<string, Material>();
        sharedMeshes = new Dictionary<string, Mesh>();
    }

    public float GetShapeToReferenceShapeScaleFactor(ThingPartBase baseType)
    {
        float factor = 1f;
        const ThingPartBase referenceBase = ThingPartBase.LowPolySphere;

        if (baseType != referenceBase)
        {
            Transform referenceTransform = thingPartBases[(int)referenceBase].transform;
            Transform thisTransform = thingPartBases[(int)baseType].transform;
            factor = referenceTransform.localScale.x / thisTransform.localScale.x;
        }

        return factor;
    }

    IEnumerator AttachHead(string thingId)
    {
        GameObject thingObject = null;

        yield return StartCoroutine(Managers.thingManager.InstantiateThingViaCache(ThingRequestContext.ApproveBodyDialogHead, thingId,
            (returnThingObject) =>
            {
                thingObject = returnThingObject;
            })
        );

        if (thingObject != null)
        {
            Thing thing = thingObject.GetComponent<Thing>();

            GameObject attachmentPoint = Managers.personManager.ourPerson.AttachmentPointHead;
            thing.transform.position = attachmentPoint.transform.position;
            thing.transform.rotation = attachmentPoint.transform.rotation;

            Managers.personManager.DoAttachThing(attachmentPoint, thing.gameObject);

            const bool clearSpotsLeftEmpty = false;
            StartCoroutine(Managers.thingManager.SetOurCurrentBodyAttachmentsByThing(
                    ThingRequestContext.LocalTest, thing.thingId, clearSpotsLeftEmpty));
        }
    }

    public IEnumerator SetOurCurrentBodyAttachmentsByThing(ThingRequestContext thingRequestContext, string thingId, bool clearSpotsLeftEmpty, Thing headToAttach = null)
    {
        string thingDefJSON = null;
        string getThingDefErrorString = null;

        yield return StartCoroutine(thingDefinitionCache.GetThingDefinition(thingRequestContext, thingId,
            (errorString, definitionJSON) =>
            {
                thingDefJSON = definitionJSON;
                getThingDefErrorString = errorString;
            })
        );

        if (!String.IsNullOrEmpty(getThingDefErrorString))
        {
            Debug.LogError(getThingDefErrorString);
        }
        else
        {
            Assert.IsNotNull(thingDefJSON, "SetOurCurrentBodyAttachmentsByThing got null thingDefJSON");
            var data = JSON.Parse(thingDefJSON);

            JSONNode body = data["bod"];
            if (body != null && (body["h"] != null || headToAttach == null))
            {
                Debug.Log("Body data found");

                if (body["h"] != null)
                {
                    if (headToAttach != null)
                    {
                        GameObject attachmentPoint = Managers.personManager.ourPerson.AttachmentPointHead;
                        GameObject headClone = (GameObject)Instantiate(headToAttach.gameObject,
                                headToAttach.transform.position, headToAttach.transform.rotation);
                        MakeDeepThingClone(headToAttach.gameObject, headClone, alsoCloneIfNotContainingScript: true);
                        Managers.personManager.DoAttachThing(attachmentPoint, headClone);
                        Managers.soundManager.Play("putDown", attachmentPoint.transform);
                    }

                    StartCoroutine(SetOurCurrentBodyAttachment(thingRequestContext, body, "HeadCore/HeadAttachmentPoint", "h",
                            merelyAdjustCurrent: true));
                }
                StartCoroutine(SetOurCurrentBodyAttachment(thingRequestContext, body, "HeadCore/HeadTopAttachmentPoint", "ht",
                        clearSpotsLeftEmpty: clearSpotsLeftEmpty));

                StartCoroutine(SetOurCurrentBodyAttachment(thingRequestContext, body, "HandCoreLeft/ArmLeftAttachmentPoint", "al",
                        clearSpotsLeftEmpty: clearSpotsLeftEmpty));
                StartCoroutine(SetOurCurrentBodyAttachment(thingRequestContext, body, "HandCoreRight/ArmRightAttachmentPoint", "ar",
                        clearSpotsLeftEmpty: clearSpotsLeftEmpty));

                StartCoroutine(SetOurCurrentBodyAttachment(thingRequestContext, body, "Torso/UpperTorsoAttachmentPoint", "ut",
                        clearSpotsLeftEmpty: clearSpotsLeftEmpty));
                StartCoroutine(SetOurCurrentBodyAttachment(thingRequestContext, body, "Torso/LowerTorsoAttachmentPoint", "lt",
                        clearSpotsLeftEmpty: clearSpotsLeftEmpty));

                StartCoroutine(SetOurCurrentBodyAttachment(thingRequestContext, body, "Torso/LegLeftAttachmentPoint", "ll",
                        clearSpotsLeftEmpty: clearSpotsLeftEmpty));
                StartCoroutine(SetOurCurrentBodyAttachment(thingRequestContext, body, "Torso/LegRightAttachmentPoint", "lr",
                        clearSpotsLeftEmpty: clearSpotsLeftEmpty));
            }
        }
    }

    private IEnumerator SetOurCurrentBodyAttachment(ThingRequestContext thingRequestContext, JSONNode body, string treePath, string shortName, bool merelyAdjustCurrent = false, bool clearSpotsLeftEmpty = false)
    {
        treePath = "/OurPersonRig/" + treePath;
        GameObject attachmentPoint = Managers.treeManager.GetObject(treePath);
        if (attachmentPoint != null)
        {

            JSONNode data = body[shortName];
            string oldId = "";
            if (merelyAdjustCurrent)
            {
                GameObject oldThingObject = Misc.GetChildWithTag(attachmentPoint.transform, "Attachment");
                if (oldThingObject != null)
                {
                    oldId = oldThingObject.GetComponent<Thing>().thingId;
                }
            }

            if (data != null || clearSpotsLeftEmpty)
            {
                Managers.personManager.DoRemoveAttachedThing(attachmentPoint);
            }

            if (data != null)
            {
                const float secondsToTryAvoidRacing = 0.35f;
                yield return new WaitForSeconds(secondsToTryAvoidRacing);

                GameObject attachmentThing = null;
                string thingId = merelyAdjustCurrent ? oldId : (string)data["i"];
                if (!string.IsNullOrEmpty(thingId))
                {
                    yield return StartCoroutine(Managers.thingManager.InstantiateThingViaCache(
                            thingRequestContext,
                            thingId,
                            (returnThing) => { attachmentThing = returnThing; })
                    );

                    if (attachmentThing != null)
                    {
                        attachmentThing.transform.parent = attachmentPoint.transform;
                        attachmentThing.transform.localPosition = JsonHelper.GetVector3(data["p"]);
                        attachmentThing.transform.localEulerAngles = JsonHelper.GetVector3(data["r"]);

                        bool containsLegAttachmentPositionRotation = data["ap"] != null && data["ar"] != null;
                        if (containsLegAttachmentPositionRotation)
                        {
                            attachmentPoint.transform.localPosition = JsonHelper.GetVector3(data["ap"]);
                            attachmentPoint.transform.localEulerAngles = JsonHelper.GetVector3(data["ar"]);
                        }

                        Managers.personManager.DoAttachThing(attachmentPoint, attachmentThing);
                    }
                }

                Managers.personManager.SaveOurLegAttachmentPointPositions();
            }

        }
    }

    public string GetThingJsonFromCache(Thing thing)
    {
        string json = null;
        if (thing != null)
        {
            thingDefinitionCache.level1Cache.TryGetValue(thing.thingId, out json);
        }
        return json;
    }

    public void MakeDeepThingClone(GameObject sourceThing, GameObject targetThing, bool alsoCloneIfNotContainingScript = false, bool isForPlacement = false, bool alwaysKeepThingPartsSeparate = false)
    {
        Thing sourceThingScript = sourceThing.GetComponent<Thing>();
        if (sourceThingScript.containsBehaviorScript || alsoCloneIfNotContainingScript || alwaysKeepThingPartsSeparate)
        {
            foreach (Transform child in targetThing.transform)
            {
                string tag = child.tag;
                if (tag == "ThingPart" ||
                        tag == "IncludedSubThings" || tag == "PlacedSubThings" ||
                        tag == "ReflectionPartDuringEditing" || tag == "ContinuationPartDuringEditing")
                {
                    Misc.Destroy(child.gameObject);
                }
            }

            string json = "";
            if (thingDefinitionCache.level1Cache.TryGetValue(sourceThingScript.thingId, out json))
            {
                JsonToThingConverter.SetThing(targetThing, json, isForPlacement: isForPlacement,
                        alwaysKeepThingPartsSeparate: alwaysKeepThingPartsSeparate);
            }
            else
            {
                int vertexCount = 0;
                json = ThingToJsonConverter.GetJson(sourceThing, ref vertexCount);
                JsonToThingConverter.SetThing(targetThing, json, isForPlacement: isForPlacement,
                        alwaysKeepThingPartsSeparate: alwaysKeepThingPartsSeparate);
            }

            Component[] components = targetThing.GetComponentsInChildren<ThingPart>();
            foreach (ThingPart thisThingPart in components)
            {
                if (thisThingPart.name != Universe.objectNameIfAlreadyDestroyed)
                {
                    thisThingPart.SetStatePropertiesByTransform();
                }
            }

            Thing thingScript = targetThing.GetComponent<Thing>();
            if (thingScript != null)
            {
                if (isForPlacement)
                {
                    thingScript.lastPositionForUndo = sourceThingScript.lastPositionForUndo;
                    thingScript.lastRotationForUndo = sourceThingScript.lastRotationForUndo;
                    thingScript.timeOfLastMemorizationForUndo = sourceThingScript.timeOfLastMemorizationForUndo;

                    thingScript.distanceToShow = sourceThingScript.distanceToShow;
                }

                thingScript.isHighlighted = false;
            }
        }

        if (targetThing != null)
        {
            targetThing.name = Misc.RemoveCloneFromName(targetThing.name);
        }
    }

    public void EmitThingFromOrigin(ThingRequestContext thingRequestContext, Transform origin, string thingId, float velocityPercent, bool isGravityFree, bool omitSound = false)
    {
        const int maxThrownOrEmittedThings = 2;
        if (thrownOrEmittedThingsParent.childCount < Managers.optimizationManager.maxThrownOrEmittedThingsForEmitting)
        {

            Thing originParentThing = origin.parent.GetComponent<Thing>();
            if (originParentThing != null && originParentThing.thingId != null)
            {
                string originParentThingId = originParentThing.thingId;

                Vector3 originPosition = origin.position;
                Vector3 originEulerAngles = origin.eulerAngles;
                Vector3 originForward = origin.forward;

                StartCoroutine(
                    thingDefinitionCache.GetThingDefinition(thingRequestContext, thingId, (errorString, thingDefinitionJSON) =>
                    {
                        if (errorString == null)
                        {
                            Assert.IsNotNull(thingDefinitionJSON, "InstantiateThingViaCache got null thingDefinitionJSON from cache manager!");

                            GameObject thingObject = (GameObject)Instantiate(thingGameObject);
                            Thing thing = thingObject.GetComponent<Thing>();
                            thingObject.transform.parent = thrownOrEmittedThingsParent;
                            thing.thingId = thingId;
                            JsonToThingConverter.SetThing(thingObject, thingDefinitionJSON);
                            thing.emittedByThingId = originParentThingId;

                            thing.EmitMeFromOrigin(origin, originPosition, originEulerAngles, originForward,
                                    velocityPercent, isGravityFree, omitSound);
                        }
                        else
                        {
                            Log.Error(errorString);
                        }
                    })
                );
            }

        }
    }

    public GameObject GetPlacementById(string placementId, bool ignorePlacedSubThings = false)
    {
        GameObject placement = null;
        if (!String.IsNullOrEmpty(placementId))
        {
            Component[] components = placements.GetComponentsInChildren(typeof(Thing), true);
            foreach (Thing thing in components)
            {
                if (thing.placementId == placementId && thing.CompareTag("Thing") &&
                        thing.name != Universe.objectNameIfAlreadyDestroyed)
                {

                    bool isPlacedSubThing = thing.transform.parent.CompareTag("PlacedSubThings");
                    if (!(isPlacedSubThing && ignorePlacedSubThings))
                    {
                        placement = thing.gameObject;
                        break;
                    }

                }
            }
        }
        return placement;
    }

    public GameObject GetThingByThrownId(string thrownId)
    {
        GameObject thingObject = null;

        {
            Component[] things = thrownOrEmittedThingsParent.GetComponentsInChildren<Thing>();
            foreach (Thing thing in things)
            {
                if (thing.isThrownOrEmitted && thing.thrownId == thrownId)
                {
                    thingObject = thing.gameObject;
                    break;
                }

            }
        }

        bool mightBeAStuckSticky = thingObject == null;
        if (mightBeAStuckSticky)
        {
            Component[] things = placements.GetComponentsInChildren<Thing>();
            foreach (Thing thing in things)
            {
                if (thing.isThrownOrEmitted && thing.thrownId == thrownId)
                {
                    thingObject = thing.gameObject;
                    Log.Debug("Found as sticky");
                    break;
                }

            }
        }

        return thingObject;
    }

    public void GetTopThingIdsCreatedByPerson(string personId, Action<List<string>> callback)
    {
        const int numberToGet = 4;
        StartCoroutine(Managers.serverManager.GetTopThingIdsCreatedByPerson(personId, numberToGet, (response) =>
        {
            if (response.error == null)
            {
                callback(response.Ids);
            }
            else
            {
                Log.Error(response.error);
            }
        }));
    }

    public Vector3 GetAppropriateDownScaleForThing(GameObject thing, float maxScale = 0.1f, bool avoidUpscalingSmallThings = false)
    {
        Transform oldParent = thing.transform.parent;
        Vector3 oldScale = thing.transform.localScale;

        thing.transform.parent = null;
        thing.transform.localScale = Vector3.one;

        Component[] particleSystems = thing.GetComponentsInChildren(typeof(ParticleSystem), true);
        foreach (ParticleSystem childParticleSystem in particleSystems)
        {
            childParticleSystem.gameObject.SetActive(false);
        }

        float scale = 1f;
        Vector3 size = GetCombinedBoundsSizeOfThingParts(thing);
        scale = maxScale / Misc.GetLargestValueOfVector(size);

        if (avoidUpscalingSmallThings && scale > 1f)
        {
            scale = 1f;
        }

        foreach (ParticleSystem childParticleSystem in particleSystems)
        {
            childParticleSystem.gameObject.SetActive(true);
        }

        thing.transform.parent = oldParent;
        thing.transform.localScale = oldScale;

        return Misc.GetUniformVector3(scale);
    }

    public static Vector3 GetCombinedBoundsSizeOfThingParts(GameObject parentObject)
    {
        Bounds bounds = new Bounds(parentObject.transform.position, Vector3.zero);
        foreach (Renderer renderer in parentObject.GetComponentsInChildren(typeof(Renderer), true))
        {
            if (renderer.gameObject.CompareTag("ThingPart"))
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return bounds.size;
    }

    public void UpdateStats()
    {
        Dictionary<string, int> partCountById = new Dictionary<string, int>();
        Dictionary<string, string> thingNamesById = new Dictionary<string, string>();

        statsThingsAroundPosition = 0;
        statsThingPartsAroundPosition = 0;
        statsThingsInArea = placements.transform.childCount;

        foreach (Transform placement in placements.transform)
        {
            if (placement.gameObject.activeSelf)
            {
                Thing thing = placement.GetComponent<Thing>();
                string thingId = thing.thingId;
                int thisPartCount = GetThingPartCount(placement.gameObject);

                if (!partCountById.ContainsKey(thingId))
                {
                    partCountById.Add(thingId, 0);
                    thingNamesById.Add(thingId, thing.givenName);
                }
                partCountById[thingId] += thisPartCount;
                statsThingsAroundPosition++;
                statsThingPartsAroundPosition += thisPartCount;
            }
        }
    }

    public bool GetPlacementsReachedLimit(out string info, GameObject dialogFundament = null)
    {
        bool reachedLimit = false;
        info = "";
        UpdateStats();

        reachedLimit = statsThingsInArea >= maxThingsInArea;
        if (reachedLimit)
        {
            info = "Sorry, your area capacity has reached the limit of " +
                    maxThingsInArea + " things. You may want to set up " +
                    "transports to other areas you make to combine into a " +
                    "bigger location.";
        }
        return reachedLimit;
    }

    public int GetThingPartCount(GameObject thing)
    {
        int partCount = 0;
        foreach (Transform child in thing.transform)
        {
            if (child.gameObject.CompareTag("ThingPart"))
            {
                partCount++;
            }
        }
        return partCount;
    }

    public int GetThingPartCountFullDepthWithStrictSyncCount(GameObject thing, out int strictSyncCount)
    {
        Component[] thingParts = thing.GetComponentsInChildren<ThingPart>();

        strictSyncCount = 0;
        foreach (ThingPart thingPart in thingParts)
        {
            if (thingPart.IsStrictSyncingToAreaNewcomersNeeded(true))
            {
                strictSyncCount++;
            }
        }

        return thingParts.Length;
    }

    public void LoadMaterial(GameObject gameObject, string assetPath)
    {
        StartCoroutine(LoadMaterialAsync(gameObject, assetPath));
    }

    private IEnumerator LoadMaterialAsync(GameObject gameObject, string assetPath)
    {
        ResourceRequest request = Resources.LoadAsync(assetPath);
        yield return request;
        if (!request.isDone) { Log.Debug("undone object after yield! " + assetPath); }
        if (gameObject != null)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            Material[] materials = renderer.materials;
            materials[0] = (Material)request.asset as Material;
            if (materials[0] != null)
            {
                renderer.materials = materials;
            }
            else
            {
                Log.Debug("Material was null on load: " + assetPath);
            }
        }
    }

    public string ExportThing(Thing thing)
    {
        string folderPath = Application.persistentDataPath + "/things";
        System.IO.Directory.CreateDirectory(folderPath);

        Vector3 originalPosition = thing.transform.position;
        thing.transform.position = Vector3.zero;

        OBJExporter exporter = new OBJExporter();
        exporter.applyPosition = true;

        Component[] thingParts = thing.gameObject.GetComponentsInChildren<ThingPart>();
        GameObject[] gameObjects = new GameObject[thingParts.Length];
        int i = 0;
        foreach (ThingPart thingPart in thingParts)
        {
            if (thingPart.material.name.IndexOf("(Instance)") >= 0)
            {
                thingPart.material.name = JsonToThingConverter.GetNormalizedColorString(thingPart.material.color);
            }

            gameObjects[i++] = thingPart.gameObject;
        }

        string fileNameBase = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss");
        string givenName = thing.givenName.Replace(" ", "-").ToLower();
        string allowedChars = Validator.lowerCaseLettersAndNumbers + "-";
        if (Validator.ContainsOnly(givenName, allowedChars))
        {
            fileNameBase = givenName + "-" + fileNameBase;
        }

        string filePath = folderPath + "/" + fileNameBase + ".obj";
        exporter.Export(filePath, gameObjects);


        string jsonFilePath = folderPath + "/" + fileNameBase + ".json";

        string json = "";
        if (thingDefinitionCache.level1Cache.TryGetValue(thing.thingId, out json))
        {
            System.IO.File.WriteAllText(jsonFilePath, json);
        }
        else
        {
            int vertexCount = 0;
            json = ThingToJsonConverter.GetJson(thing.gameObject, ref vertexCount);
            System.IO.File.WriteAllText(jsonFilePath, json);
        }

        thing.transform.position = originalPosition;

        return folderPath;
    }

    public string ExportAllThings(List<string> thingIdsByOtherCreators)
    {
        string folderPath = Application.persistentDataPath + "/areas";
        System.IO.Directory.CreateDirectory(folderPath);

        string fileNameBase = DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss");
        string name = Managers.areaManager.currentAreaName.Replace(" ", "-").ToLower();
        name = name.Replace("'", "");
        string allowedChars = Validator.lowerCaseLettersAndNumbers + "-";
        if (Validator.ContainsOnly(name, allowedChars))
        {
            fileNameBase = name + "-" + fileNameBase;
        }

        string thingsJsonPath = folderPath + "/" + fileNameBase + "-things";
        System.IO.Directory.CreateDirectory(thingsJsonPath);

        OBJExporter exporter = new OBJExporter();
        exporter.applyPosition = true;

        Component[] thingPartComponents = placements.GetComponentsInChildren(typeof(ThingPart), true);
        GameObject[] gameObjects = new GameObject[thingPartComponents.Length];
        List<string> alreadyExportedThingids = new List<string>();
        int i = 0;
        foreach (Component thingPartComponent in thingPartComponents)
        {
            if (thingPartComponent.transform.parent != null)
            {
                Thing thing = thingPartComponent.transform.parent.GetComponent<Thing>();
                if (thing != null)
                {
                    ThingPart thingPart = thingPartComponent.GetComponent<ThingPart>();
                    bool isCreatedByUs = !thingIdsByOtherCreators.Contains(thing.thingId);
                    if (isCreatedByUs || thing.isClonable)
                    {

                        if (!thingPart.invisible)
                        {
                            gameObjects[i++] = thingPartComponent.gameObject;
                        }

                        if (!alreadyExportedThingids.Contains(thing.thingId))
                        {
                            alreadyExportedThingids.Add(thing.thingId);
                            string thingJsonPath = thingsJsonPath + "/" + thing.thingId + ".json";
                            string thingJson;
                            if (!thingDefinitionCache.level1Cache.TryGetValue(thing.thingId, out thingJson))
                            {
                                int vertexCount = 0;
                                thingJson = ThingToJsonConverter.GetJson(thing.gameObject, ref vertexCount);
                            }
                            System.IO.File.WriteAllText(thingJsonPath, thingJson);
                        }

                    }
                }
            }
        }

        string filePath = folderPath + "/" + fileNameBase + ".obj";
        exporter.Export(filePath, gameObjects);


        string areaJsonPath = folderPath + "/" + fileNameBase + ".json";
        string areaJson = Managers.areaManager.GetAreaJsonForExport();
        System.IO.File.WriteAllText(areaJsonPath, areaJson);


        return folderPath;
    }

    public void ResetTriggeredOnSomeoneNewInVicinity()
    {
        Component[] components = placements.GetComponentsInChildren(typeof(Thing), true);
        foreach (Thing thing in components)
        {
            thing.triggeredOnSomeoneNewInVicinity = false;
        }
    }

    public string GetUnremovableCenterColorString(Thing thing)
    {
        string colorString = "";
        Component[] thingParts = thing.GetComponentsInChildren<ThingPart>();
        foreach (ThingPart thingPart in thingParts)
        {
            if (thingPart.isUnremovableCenter && thingPart.material != null)
            {
                Color32 color = thingPart.material.color;
                colorString = color.r + "," + color.g + "," + color.b;
            }
        }
        return colorString;
    }

    public void PlacePlacedSubThingsAsTheyWereOriginallyPositioned(GameObject thingObject)
    {
        Component[] thingParts = thingObject.GetComponentsInChildren<ThingPart>();
        int placedCount = 0;
        foreach (ThingPart thingPart in thingParts)
        {

            Dictionary<string, ThingIdPositionRotation> oldPlacedSubThingIdsWithOriginalInfo =
                    GetPlacedSubThingIdsWithOriginalInfoClone(thingPart.placedSubThingIdsWithOriginalInfo);

            foreach (KeyValuePair<string, ThingIdPositionRotation> entry in oldPlacedSubThingIdsWithOriginalInfo)
            {
                ThingIdPositionRotation thingIdPositionRotation = entry.Value;

                PlaceBasedOnOldThingIdPositionRotation(
                        ThingRequestContext.PlaceOriginalPlacedSubThings,
                        thingPart, thingIdPositionRotation);
                if (++placedCount >= CreationHelper.maxPlacedSubThingsToRecreate)
                {
                    return;
                }
            }

        }
    }

    private Dictionary<string, ThingIdPositionRotation> GetPlacedSubThingIdsWithOriginalInfoClone(Dictionary<string, ThingIdPositionRotation> original)
    {
        Dictionary<string, ThingIdPositionRotation> thisClone = new Dictionary<string, ThingIdPositionRotation>();
        foreach (KeyValuePair<string, ThingIdPositionRotation> entry in original)
        {
            thisClone.Add(entry.Key, entry.Value);
        }
        return thisClone;
    }

    private void PlaceBasedOnOldThingIdPositionRotation(ThingRequestContext thingRequestContext, ThingPart thingPart, ThingIdPositionRotation thingIdPositionRotation)
    {
        StartCoroutine(
            thingDefinitionCache.GetThingDefinition(thingRequestContext, thingIdPositionRotation.thingId, (errorString, thingDefinitionJSON) =>
            {
                if (errorString == null)
                {
                    GameObject newThingObject = (GameObject)Instantiate(thingGameObject);
                    Thing newThing = newThingObject.GetComponent<Thing>();
                    newThing.thingId = thingIdPositionRotation.thingId;

                    JsonToThingConverter.SetThing(newThingObject, thingDefinitionJSON, isForPlacement: true);

                    Vector3 oldThingPartScale = thingPart.transform.localScale;
                    thingPart.transform.localScale = Vector3.one;
                    newThingObject.transform.parent = thingPart.transform;
                    newThingObject.transform.localPosition = thingIdPositionRotation.position;
                    newThingObject.transform.localEulerAngles = thingIdPositionRotation.rotation;
                    newThingObject.transform.parent = placements.transform;
                    thingPart.transform.localScale = oldThingPartScale;

                    newThing.MemorizeOriginalTransform(isOriginalPlacement: true);

                    string newPlacementId = Managers.personManager.DoPlaceRecreatedPlacedSubThing(newThingObject);
                    thingPart.placedSubThingIdsWithOriginalInfo.Add(newPlacementId, thingIdPositionRotation);
                }
                else
                {
                    Log.Error(errorString);
                }
            })
        );
    }

    public void UpdatePlacedSubThingsInfo(Thing thing)
    {
        ThingPart[] thingParts = thing.GetComponentsInChildren<ThingPart>();
        foreach (ThingPart thingPart in thingParts)
        {

            Dictionary<string, ThingIdPositionRotation> oldPlacedSubThingIdsWithOriginalInfo =
                    GetPlacedSubThingIdsWithOriginalInfoClone(thingPart.placedSubThingIdsWithOriginalInfo);
            thingPart.placedSubThingIdsWithOriginalInfo = new Dictionary<string, ThingIdPositionRotation>();

            foreach (KeyValuePair<string, ThingIdPositionRotation> entry in oldPlacedSubThingIdsWithOriginalInfo)
            {
                string placementId = entry.Key;
                ThingIdPositionRotation thingIdPositionRotation = entry.Value;

                GameObject placement = GetPlacementById(placementId);
                if (placement != null)
                {
                    Thing placementThing = placement.GetComponent<Thing>();
                    if (placementThing != null)
                    {
                        thingPart.ResetStates();

                        Transform oldParent = placement.transform.parent;
                        Vector3 oldThingPartScale = thingPart.transform.localScale;
                        thingPart.transform.localScale = Vector3.one;
                        placement.transform.parent = thingPart.transform;

                        thingPart.AddConfirmedNonExistingPlacedSubThingId(
                            placementId, placementThing.thingId,
                            placement.transform.localPosition, placement.transform.localEulerAngles
                        );

                        placement.transform.parent = oldParent;
                        thingPart.transform.localScale = oldThingPartScale;
                    }
                }

            }

        }
    }

    public void StoreThingJsonInAllCaches(string thingId, string json)
    {
        thingDefinitionCache.StoreInAllCaches(thingId, json);
    }

    public Dictionary<TextureProperty, float>[] CloneTextureProperties(Dictionary<TextureProperty, float>[] originalTextureProperties)
    {
        Dictionary<TextureProperty, float>[] newTextureProperties = null;
        if (originalTextureProperties != null)
        {
            newTextureProperties = new Dictionary<TextureProperty, float>[] { null, null };
            for (int i = 0; i < originalTextureProperties.Length; i++)
            {
                newTextureProperties[i] = CloneTextureProperty(originalTextureProperties[i]);
            }
        }
        return newTextureProperties;
    }

    public Dictionary<TextureProperty, float> CloneTextureProperty(Dictionary<TextureProperty, float> originalTextureProperty)
    {
        Dictionary<TextureProperty, float> newTextureProperty = null;
        if (originalTextureProperty != null)
        {
            newTextureProperty = new Dictionary<TextureProperty, float>();
            foreach (KeyValuePair<TextureProperty, float> item in originalTextureProperty)
            {
                newTextureProperty.Add(item.Key, item.Value);
            }
        }
        return newTextureProperty;
    }

    public void SetTexturePropertiesToDefault(Dictionary<TextureProperty, float> textureProperty, TextureType textureType)
    {
        foreach (TextureProperty property in System.Enum.GetValues(typeof(TextureProperty)))
        {
            if (textureProperty.ContainsKey(property))
            {
                textureProperty[property] = texturePropertyDefault[property];
            }
            else
            {
                textureProperty.Add(property, texturePropertyDefault[property]);
            }
        }

        float alphaCap = 1f;
        float alphaCapOverride;
        if (textureAlphaCaps.TryGetValue(textureType, out alphaCapOverride))
        {
            alphaCap = alphaCapOverride;
        }

        textureProperty[TextureProperty.Strength] = Mathf.Lerp(0.5f, 0.5f + alphaCap * 0.5f, 0.785f);

        if (IsAlgorithmTextureType(textureType))
        {
            textureProperty[TextureProperty.ScaleX] = 0.17f;
            textureProperty[TextureProperty.ScaleY] = textureProperty[TextureProperty.ScaleX];
            textureProperty[TextureProperty.OffsetX] = 0.5f;
            textureProperty[TextureProperty.OffsetY] = 0.5f;
        }

        switch (textureType)
        {
            case TextureType.SideGlow:
                textureProperty[TextureProperty.Strength] = 0f;
                break;

            case TextureType.Wireframe:
                textureProperty[TextureProperty.Strength] = 0.75f;
                break;

            case TextureType.Outline:
                textureProperty[TextureProperty.Strength] = 0.05f;
                break;

            case TextureType.VoronoiDots:
                textureProperty[TextureProperty.Param2] = 0.25f;
                break;

            case TextureType.Vertex_Scatter:
            case TextureType.Vertex_Expand:
            case TextureType.Vertex_Slice:
                textureProperty[TextureProperty.Strength] = 0.5f;
                break;
        }
    }

    public void AdditionallyModulateTexturePropertiesByType(Dictionary<TextureProperty, float> textureProperty, TextureType textureType)
    {
        //switch (textureType) {
        /* e.g.
        case TextureType.WoodGrain:
            textureProperty[TextureProperty.Param1] *= 10;
            break;
        */
        //}
    }

    public void SetParticleSystemPropertiesToDefault(Dictionary<ParticleSystemProperty, float> particleSystemProperty, ParticleSystemType particleSystemType)
    {
        foreach (ParticleSystemProperty property in System.Enum.GetValues(typeof(ParticleSystemProperty)))
        {
            if (particleSystemProperty.ContainsKey(property))
            {
                particleSystemProperty[property] = particleSystemPropertyDefault[property];
            }
            else
            {
                particleSystemProperty.Add(property, particleSystemPropertyDefault[property]);
            }
        }

        switch (particleSystemType)
        {
            case ParticleSystemType.TwisterLines:
                particleSystemProperty[ParticleSystemProperty.Size] = 0.025f;
                break;

            case ParticleSystemType.FireMore:
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.5f;
                particleSystemProperty[ParticleSystemProperty.Size] = 0.5f;
                break;

            case ParticleSystemType.OrganicSplatter:
                particleSystemProperty[ParticleSystemProperty.Size] = 0.4f;
                break;

            case ParticleSystemType.AreaEmbers:
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.75f;
                break;

            case ParticleSystemType.CenteredElectric:
                particleSystemProperty[ParticleSystemProperty.Speed] = 0f;
                break;

            case ParticleSystemType.Smoke:
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.5f;
                particleSystemProperty[ParticleSystemProperty.Size] = 0.5f;
                break;

            case ParticleSystemType.CircularSmoke:
                particleSystemProperty[ParticleSystemProperty.Alpha] = 0.1f;
                break;

            case ParticleSystemType.Shards:
            case ParticleSystemType.RoughShards:
                particleSystemProperty[ParticleSystemProperty.Size] = 0.1f;
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.6f;
                particleSystemProperty[ParticleSystemProperty.Gravity] = 0.1f;
                particleSystemProperty[ParticleSystemProperty.Speed] = 0.35f;
                break;

            case ParticleSystemType.FireThrow:
                particleSystemProperty[ParticleSystemProperty.Size] = 0.35f;
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.5f;
                break;

            case ParticleSystemType.SoftFire:
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.5f;
                particleSystemProperty[ParticleSystemProperty.Size] = 0.5f;
                particleSystemProperty[ParticleSystemProperty.Alpha] = 0.5f;
                break;

            case ParticleSystemType.SpiralSmoke:
                particleSystemProperty[ParticleSystemProperty.Amount] = 0.4f;
                particleSystemProperty[ParticleSystemProperty.Size] = 0.5f;
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.5f;
                break;

            case ParticleSystemType.TwisterSmoke:
                particleSystemProperty[ParticleSystemProperty.Amount] = 0.4f;
                particleSystemProperty[ParticleSystemProperty.Size] = 0.5f;
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.5f;
                break;

            case ParticleSystemType.ShrinkSmoke:
            case ParticleSystemType.ThickSmoke:
            case ParticleSystemType.PlopSmoke:
                particleSystemProperty[ParticleSystemProperty.Size] = 0.5f;
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.5f;
                break;

            case ParticleSystemType.LightStreaks:
                particleSystemProperty[ParticleSystemProperty.Size] = 0.7f;
                break;

            case ParticleSystemType.SoftSmoke:
                particleSystemProperty[ParticleSystemProperty.Size] = 0.5f;
                particleSystemProperty[ParticleSystemProperty.Shape] = 0.5f;
                particleSystemProperty[ParticleSystemProperty.Speed] = 0f;
                break;

            case ParticleSystemType.TwistedSmoke:
                particleSystemProperty[ParticleSystemProperty.Alpha] = 0.05f;
                break;
        }

    }

    public Dictionary<ParticleSystemProperty, float> CloneParticleSystemProperty(Dictionary<ParticleSystemProperty, float> originalParticleSystemProperty)
    {
        Dictionary<ParticleSystemProperty, float> newParticleSystemProperty = null;
        if (originalParticleSystemProperty != null)
        {
            newParticleSystemProperty = new Dictionary<ParticleSystemProperty, float>();
            foreach (KeyValuePair<ParticleSystemProperty, float> item in originalParticleSystemProperty)
            {
                newParticleSystemProperty.Add(item.Key, item.Value);
            }
        }
        return newParticleSystemProperty;
    }

    public List<TextureProperty> GetTexturePropertiesList(TextureType textureType)
    {
        List<TextureProperty> properties = new List<TextureProperty>();

        if (textureType == TextureType.Vertex_Scatter)
        {
            properties.Add(TextureProperty.Strength);
            properties.Add(TextureProperty.Glow);
            properties.Add(TextureProperty.Param1);
            properties.Add(TextureProperty.Param2);

        }
        else if (textureType == TextureType.Vertex_Expand)
        {
            properties.Add(TextureProperty.Strength);
            properties.Add(TextureProperty.Glow);

        }
        else if (textureType == TextureType.Vertex_Slice)
        {
            properties.Add(TextureProperty.ScaleY);
            properties.Add(TextureProperty.ScaleX);
            properties.Add(TextureProperty.Strength);
            properties.Add(TextureProperty.Glow);

        }
        else
        {

            int paramsNumber = 0;
            if (textureExtraParamsNumber.ContainsKey(textureType))
            {
                paramsNumber = textureExtraParamsNumber[textureType];
            }

            foreach (TextureProperty property in Enum.GetValues(typeof(TextureProperty)))
            {
                switch (property)
                {
                    case TextureProperty.Param1:
                        if (paramsNumber >= 1) { properties.Add(property); }
                        break;

                    case TextureProperty.Param2:
                        if (paramsNumber >= 2) { properties.Add(property); }
                        break;

                    case TextureProperty.Param3:
                        if (paramsNumber >= 3) { properties.Add(property); }
                        break;

                    default:
                        properties.Add(property);
                        break;
                }
            }

        }

        return properties;
    }

    public List<ParticleSystemProperty> GetParticleSystemPropertiesList(ParticleSystemType particleSystemType)
    {
        List<ParticleSystemProperty> properties = new List<ParticleSystemProperty>();
        foreach (ParticleSystemProperty property in Enum.GetValues(typeof(ParticleSystemProperty)))
        {
            properties.Add(property);
        }
        return properties;
    }

    public void ReplaceThing(Thing oldThing, Thing newThing)
    {
        GameObject thingObject = (GameObject)Instantiate(thingGameObject);
        Thing thing = thingObject.GetComponent<Thing>();
        thingObject.SetActive(oldThing.gameObject.activeSelf);

        Vector3 oldScale = oldThing.transform.localScale;

        thing.thingId = newThing.thingId;
        Managers.thingManager.MakeDeepThingClone(newThing.gameObject, thingObject,
                alsoCloneIfNotContainingScript: true, isForPlacement: true);

        thing.placementId = oldThing.placementId;
        thingObject.transform.parent = placements.transform;

        thing.transform.position = oldThing.transform.position;
        thing.transform.rotation = oldThing.transform.rotation;
        thing.transform.localScale = oldScale;

        thing.MemorizeOriginalTransform(isOriginalPlacement: true);

        Misc.Destroy(oldThing.gameObject);
    }

    public bool IsClosestSurfaceNearbyOurPerson(ThingPart sourceThingPart, float maxDistance)
    {
        bool isIt = false;

        if (sourceThingPart.transform.parent != null)
        {
            Thing sourceThing = sourceThingPart.transform.parent.GetComponent<Thing>();
            if (sourceThing != null)
            {

                float? bestDistance = null;
                ThingPart closestThingPart = null;
                Vector3[] directions = GetSphereDirections();
                foreach (Vector3 direction in directions)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(sourceThingPart.transform.position, direction, out hit, 2f))
                    {
                        if (bestDistance == null || hit.distance < (float)bestDistance)
                        {
                            ThingPart hitThingPart = hit.collider.gameObject.GetComponent<ThingPart>();
                            if (hitThingPart != null && hitThingPart != sourceThingPart && hitThingPart.transform.parent != null)
                            {
                                Thing hitThing = hitThingPart.transform.parent.GetComponent<Thing>();
                                if (hitThing != null && hitThing != sourceThing && !hitThing.uncollidable)
                                {
                                    bestDistance = hit.distance;
                                    closestThingPart = hitThingPart;
                                }
                            }
                        }
                    }
                }

                if (closestThingPart != null)
                {
                    if (Managers.personManager.GetIsThisObjectOfOurPerson(closestThingPart.gameObject))
                    {
                        isIt = true;
                    }
                }

            }

        }

        return isIt;
    }

    public Vector3[] GetSphereDirections(int numDirections = 16)
    {
        // answers.unity3d.com/answers/539155/view.html
        var pts = new Vector3[numDirections];
        var inc = Math.PI * (3 - Math.Sqrt(5));
        var off = 2f / numDirections;

        foreach (var k in Enumerable.Range(0, numDirections))
        {
            var y = k * off - 1 + (off / 2);
            var r = Math.Sqrt(1 - y * y);
            var phi = k * inc;
            var x = (float)(Math.Cos(phi) * r);
            var z = (float)(Math.Sin(phi) * r);
            pts[k] = new Vector3(x, y, z);
        }

        return pts;
    }

    public string GetVersionInfo(int version)
    {
        string s = "";

        switch (version)
        {
            case 1:
                s = "To emulate an old bug-used-as-feature-by-people behavior, for " +
                        "downwards-compatibility reasons, \"send nearby\" commands of " +
                        "things saved at this version are treated to mean \"send one nearby\" " +
                        "when part of emitted or thrown items stuck to someone.";
                break;

            case 2:
                s = "If the thingPart includes an image, the material will be forced " +
                        "to become default and white (and black during loading). In version 3+ " +
                        "it will be left as is, and e.g. glowing becomes a glowing image, and" +
                        "thingPart colors are being respected.";
                break;

            case 3:
                s = "The default font material in version 4+ is non-glowing. Version 3- " +
                        "fonts will take on the glow material.";
                break;

            case 4:
                s = "In version 4-, bouncy & slidy for thrown/ emitted things were both " +
                        "selectable, but mutually exclusive in effect (defaulting on bouncy). " +
                        "Since v5+ they mix.";
                break;

            case 5:
                s = "In version 5-, \"tell web\" and \"tell any web\" didn't exist as special " +
                        "tell scope commands, so they will be understood as being tell/ tell any " +
                        "with \"web\" as data.";
                break;

            case 6:
                s = "In version 6-, one unit of the \"set constant rotation\" command equals " +
                        "10 rotation degree (instead of 1 in later version).";
                break;

            case 7:
                s = "In version 8, the \"tell in front\" and \"tell first front\" commands were added. In version 7-, " +
                        "\"in front\"/ \"first in front\" are considered normal tell data text.";
                break;

            case 8:
                s = "As of version 9, sounds played via the Loop command adhere to the Thing's Surround Sound " +
                        "attribute. In version 8-, that setting was ignored.";
                break;

            case currentThingVersion:
                s = "The current version.";
                break;
        }

        return s;
    }

    public GameObject GetClosestThingOfNameIn(GameObject originObject, List<GameObject> parentNodesToSearchThrough, string targetName)
    {
        GameObject closestThing = null;

        float? bestDistance = null;
        Vector3 originPosition = originObject.transform.position;
        foreach (GameObject parentNode in parentNodesToSearchThrough)
        {
            Component[] components = parentNode.GetComponentsInChildren(typeof(Thing), true);
            foreach (Thing thing in components)
            {
                GameObject thingObject = thing.gameObject;
                if (thingObject.name == targetName &&
                        thingObject != CreationHelper.thingBeingEdited &&
                        thingObject != originObject &&
                        !(thing.isInInventoryOrDialog || thing.isGiftInDialog)
                        )
                {
                    float thisDistance = Vector3.Distance(originPosition, thing.transform.position);
                    if (bestDistance == null || thisDistance < (float)bestDistance)
                    {
                        bestDistance = thisDistance;
                        closestThing = thingObject;
                    }
                }
            }
        }

        return closestThing;
    }

    public bool IsVertexTexture(TextureType textureType)
    {
        return textureType == TextureType.Vertex_Scatter ||
                textureType == TextureType.Vertex_Expand ||
                textureType == TextureType.Vertex_Slice;
    }

    public void SearchThings(string term, Action<List<string>> callback)
    {
        StartCoroutine(Managers.serverManager.SearchThings(term, (response) =>
        {
            if (response.error == null)
            {
                callback(response.thingIds);
            }
            else
            {
                Log.Error(response.error);
            }
        }));
    }

    public bool IsParticleSystemTypeWithOnlyAlphaSetting(ParticleSystemType type)
    {
        return particleSystemTypeWithOnlyAlphaSetting.ContainsKey(type);
    }

    public bool IsTextureTypeWithOnlyAlphaSetting(TextureType type)
    {
        return textureTypeWithOnlyAlphaSetting.ContainsKey(type);
    }

    public bool IsAlgorithmTextureType(TextureType type)
    {
        return algorithmTextureTypes.ContainsKey(type);
    }

    public Material GetSharedOrDistinctTextureMaterial(ThingPart thingPart, int textureIndex, bool isBeingEdited)
    {
        Material thisMaterial = null;

        string path = "Textures/" + thingPart.textureTypes[textureIndex].ToString().Replace("_", "/");
        ThingPartState state = thingPart.states[0];

        if (isBeingEdited || thingPart.states.Count >= 2 || thingPart.textureTypes[textureIndex] == TextureType.None)
        {
            thisMaterial = Instantiate(Resources.Load(path) as Material);

            if ((thingPart.textureTypes[textureIndex] != TextureType.None && thingPart.states.Count >= 2))
            {
                thingPart.ApplyTextureColorByThis(state.textureColors[textureIndex], thisMaterial);
                Dictionary<TextureProperty, float> texturePropertyClone = CloneTextureProperty(state.textureProperties[textureIndex]);
                if (texturePropertyClone != null)
                {
                    thingPart.ModulateTheseTextureProperties(texturePropertyClone, thingPart.textureTypes[textureIndex]);
                    thingPart.ApplyTexturePropertiesToMaterial(texturePropertyClone, thisMaterial);
                }
            }

        }
        else
        {
            string id = GetSharedTextureId(thingPart, textureIndex);
            if (!String.IsNullOrEmpty(id))
            {
                Material foundMaterial = null;

                if (sharedTextureMaterials.TryGetValue(id, out foundMaterial))
                {
                    const bool reinstanciateSharedTextureMaterialsToAvoidFlicker = true;
                    if (reinstanciateSharedTextureMaterialsToAvoidFlicker)
                    {
                        thisMaterial = Instantiate(foundMaterial);
                    }
                    else
                    {
                        thisMaterial = foundMaterial;
                    }

                }
                else
                {
                    try
                    {
                        thisMaterial = Instantiate(Resources.Load(path) as Material);
                    }
                    catch (Exception exception)
                    {
                        Log.Debug("Material " + path + " not found, switching to None");
                        thisMaterial = UnityEngine.Object.Instantiate(Resources.Load("Textures/None") as Material);
                    }

                    thisMaterial.name = id;
                    thingPart.ApplyTextureColorByThis(state.textureColors[textureIndex], thisMaterial);

                    Dictionary<TextureProperty, float> texturePropertyClone = CloneTextureProperty(state.textureProperties[textureIndex]);
                    thingPart.ModulateTheseTextureProperties(texturePropertyClone, thingPart.textureTypes[textureIndex]);
                    thingPart.ApplyTexturePropertiesToMaterial(texturePropertyClone, thisMaterial);

                    sharedTextureMaterials.Add(id, thisMaterial);

                }
            }

        }

        return thisMaterial;
    }

    string GetSharedTextureId(ThingPart thingPart, int textureIndex)
    {
        string id = "";
        ThingPartState state = thingPart.states[0];

        if (state.textureProperties != null && state.textureProperties[textureIndex] != null)
        {
            id += ((int)thingPart.textureTypes[textureIndex]).ToString() + "_";

            Color color = state.textureColors[textureIndex];
            id += JsonToThingConverter.GetNormalizedColorString(color) + "_";

            bool vertexShaderWhichGlitchesWhenShared = IsVertexTexture(thingPart.textureTypes[textureIndex]);
            if (vertexShaderWhichGlitchesWhenShared) { id += "_vertex" + (++vertexTextureCounter); }

            foreach (KeyValuePair<TextureProperty, float> item in state.textureProperties[textureIndex])
            {
                id += item.Value.ToString().Replace("0.", ".") + "_";
            }
        }

        return id;
    }

    public ThingPart GetRandomThingPartForTesting()
    {
        ThingPart thingPart = null;
        #if UNITY_EDITOR
            thingPart = (ThingPart)placements.GetComponentInChildren( typeof(ThingPart), true );
        #endif
        return thingPart;
    }

    public void AddOutlineHighlightMaterial(Thing thing, bool useInnerGlow = false)
    {
        bool alsoHighlightText = !useInnerGlow;

        if (outlineHighlightMaterial == null)
        {
            const string outlinePath = "Materials/HighlightAndAlwaysVisible";
            const string innerGlowPath = "Materials/InnerGlowHighlight";

            outlineHighlightMaterial = Resources.Load(outlinePath, typeof(Material)) as Material;
            innerGlowHighlightMaterial = Resources.Load(innerGlowPath, typeof(Material)) as Material;
        }

        if (!thing.isHighlighted)
        {
            thing.isHighlighted = true;

            Component[] parts = thing.GetComponentsInChildren(typeof(ThingPart), true);
            foreach (ThingPart part in parts)
            {
                if (!part.isText || alsoHighlightText)
                {
                    Renderer thisRenderer = part.gameObject.GetComponent<MeshRenderer>();
                    Material[] oldMaterials = thisRenderer.materials;
                    Material[] newMaterials = new Material[oldMaterials.Length + 1];

                    for (int i = 0; i < oldMaterials.Length; i++)
                    {
                        newMaterials[i] = oldMaterials[i];
                    }

                    newMaterials[newMaterials.Length - 1] = useInnerGlow ?
                            innerGlowHighlightMaterial : outlineHighlightMaterial;
                    thisRenderer.materials = newMaterials;
                }
            }
        }
    }

    public void RemoveOutlineHighlightMaterial(Thing thing, bool useInnerGlow = false)
    {
        bool alsoHighlightText = !useInnerGlow;

        if (thing.isHighlighted)
        {
            thing.isHighlighted = false;

            Component[] parts = thing.transform.GetComponentsInChildren(typeof(ThingPart), true);
            foreach (ThingPart part in parts)
            {
                if (!part.isText || alsoHighlightText)
                {
                    Renderer thisRenderer = part.gameObject.GetComponent<MeshRenderer>();
                    Material[] oldMaterials = thisRenderer.materials;
                    Material[] newMaterials = new Material[oldMaterials.Length - 1];

                    for (int i = 0; i < newMaterials.Length; i++)
                    {
                        newMaterials[i] = oldMaterials[i];
                    }

                    thisRenderer.materials = newMaterials;
                }
            }
        }
    }

    public GameObject GetIncludedSubThingTopMasterThingPart(Transform transformToCheck)
    {
        GameObject masterThingPart = null;
        Transform thisTransform = transformToCheck;

        while (thisTransform != null)
        {
            IncludedSubThingsWrapper wrapper = thisTransform.GetComponent<IncludedSubThingsWrapper>();
            if (wrapper != null && wrapper.masterThingPart)
            {
                masterThingPart = wrapper.masterThingPart.gameObject;
            }
            thisTransform = thisTransform.parent;
        }

        return masterThingPart;
    }

    public GameObject GetIncludedSubThingDirectMasterThingPart(Transform transformToCheck)
    {
        GameObject masterThingPart = null;
        Transform thisTransform = transformToCheck;

        while (thisTransform != null)
        {
            IncludedSubThingsWrapper wrapper = thisTransform.GetComponent<IncludedSubThingsWrapper>();
            if (wrapper != null && wrapper.masterThingPart)
            {
                masterThingPart = wrapper.masterThingPart.gameObject;
                break;
            }
            thisTransform = thisTransform.parent;
        }


        return masterThingPart;
    }

    public bool ThingPartBaseSupportsReflectionPart(ThingPartBase thingPartBase)
    {
        return thingPartBasesSupportingReflectionParts.Contains(thingPartBase);
    }

    public bool ThingPartBaseSupportsLimitedReflectionPart(ThingPartBase thingPartBase)
    {
        return thingPartBasesSupportingLimitedReflectionParts.Contains(thingPartBase);
    }

    public int FindAndReplaceInScripts(ThingPart thingPart, string find, string replace, bool forSingleState)
    {
        int foundCount = 0;
        Thing thing = thingPart.transform.parent.GetComponent<Thing>();

        int stateMin = 0;
        int stateMax = thingPart.states.Count - 1;
        if (forSingleState)
        {
            stateMin = thingPart.currentState;
            stateMax = thingPart.currentState;
        }

        string[] findAsArray = new string[] { find };

        for (int stateI = stateMin; stateI <= stateMax; stateI++)
        {
            ThingPartState state = thingPart.states[stateI];
            for (int lineI = 0; lineI < state.scriptLines.Count; lineI++)
            {
                string line = state.scriptLines[lineI];
                string[] splitted = line.Split(findAsArray, StringSplitOptions.None);
                int thisFoundCount = splitted.Length - 1;
                if (thisFoundCount > 0)
                {
                    state.scriptLines[lineI] = line.Replace(find, replace);
                    foundCount += thisFoundCount;
                    state.ParseScriptLinesIntoListeners(thing, thingPart);
                }
            }
        }

        thingPart.SetStatePropertiesByTransform();
        return foundCount;
    }

    public Component[] GetAllThings()
    {
        GameObject thrownParent = Managers.treeManager.GetObject("/Universe/ThrownOrEmittedThings");

        Component[] array1 = Managers.thingManager.placements.GetComponentsInChildren(typeof(Thing), true);
        Component[] array2 = thrownParent.GetComponentsInChildren(typeof(Thing), true);
        Component[] array3 = Managers.personManager.ourPerson.GetComponentsInChildren(typeof(Thing), true);
        Component[] array4 = Managers.personManager.People.GetComponentsInChildren(typeof(Thing), true);

        Component[] allThings = new Component[array1.Length + array2.Length + array3.Length + array4.Length];

        int index = 0;
        array1.CopyTo(allThings, index); index += array1.Length;
        array2.CopyTo(allThings, index); index += array2.Length;
        array3.CopyTo(allThings, index); index += array3.Length;
        array4.CopyTo(allThings, index); index += array4.Length;

        return allThings;
    }

    public Component[] GetAllAttractors()
    {
        GameObject thrownParent = Managers.treeManager.GetObject("/Universe/ThrownOrEmittedThings");

        Component[] array1 = Managers.thingManager.placements.GetComponentsInChildren(typeof(AttractThings), true);
        Component[] array2 = thrownParent.GetComponentsInChildren(typeof(AttractThings), true);
        Component[] array3 = Managers.personManager.ourPerson.GetComponentsInChildren(typeof(AttractThings), true);
        Component[] array4 = Managers.personManager.People.GetComponentsInChildren(typeof(AttractThings), true);

        Component[] allAttractors = new Component[array1.Length + array2.Length + array3.Length + array4.Length];

        int index = 0;
        array1.CopyTo(allAttractors, index); index += array1.Length;
        array2.CopyTo(allAttractors, index); index += array2.Length;
        array3.CopyTo(allAttractors, index); index += array3.Length;
        array4.CopyTo(allAttractors, index); index += array4.Length;

        return allAttractors;
    }

    public Component[] GetAllRigidbodies()
    {
        GameObject thrownParent = Managers.treeManager.GetObject("/Universe/ThrownOrEmittedThings");

        Component[] array1 = Managers.thingManager.placements.GetComponentsInChildren(typeof(Rigidbody), true);
        Component[] array2 = thrownParent.GetComponentsInChildren(typeof(Rigidbody), true);
        Component[] array3 = Managers.personManager.ourPerson.GetComponentsInChildren(typeof(Rigidbody), true);
        Component[] array4 = Managers.personManager.People.GetComponentsInChildren(typeof(Rigidbody), true);

        Component[] allBodies = new Component[array1.Length + array2.Length + array3.Length + array4.Length];

        int index = 0;
        array1.CopyTo(allBodies, index); index += array1.Length;
        array2.CopyTo(allBodies, index); index += array2.Length;
        array3.CopyTo(allBodies, index); index += array3.Length;
        array4.CopyTo(allBodies, index); index += array4.Length;

        return allBodies;
    }

    public void UpdateAllVisibilityAndCollision(bool contextLaserIsOn = false)
    {
        bool weAreEditor = Managers.areaManager != null && Managers.areaManager.weAreEditorOfCurrentArea;

        bool forceVisible = weAreEditor && Our.seeInvisibleAsEditor;
        bool forceCollidable = (weAreEditor && Our.touchUncollidableAsEditor) ||
                contextLaserIsOn || Our.mode == EditModes.Inventory;

        Component[] things = GetAllThings();
        foreach (Thing thing in things)
        {
            if (thing.containsInvisibleOrUncollidable || thing.subThingMasterPart != null)
            {
                thing.UpdateAllVisibilityAndCollision(forceVisible: forceVisible, forceCollidable: forceCollidable);
            }
        }
    }

    public static void SetLayerForThingAndParts(Thing thing, string layerName)
    {
        ThingManager.SetLayerForThingAndParts(thing, LayerMask.NameToLayer(layerName));
    }

    public static void SetLayerForThingAndParts(Thing thing, int layer)
    {
        thing.gameObject.layer = layer;
        Component[] thingParts = thing.gameObject.GetComponentsInChildren<ThingPart>();
        foreach (ThingPart thingPart in thingParts)
        {
            thingPart.gameObject.layer = layer;
        }
    }

    public Thing ReCreatePlacementAfterPlacementAttributeChange(Thing thing)
    {
        if (thing == null) { return null; }

        GameObject newThingObject = (GameObject)Instantiate(thing.gameObject,
                thing.transform.position, thing.transform.rotation, placements.transform);
        Thing newThing = newThingObject.GetComponent<Thing>();
        newThing = thing.GetComponent<Thing>();
        newThing.placementId = thing.placementId;

        newThing.isLocked = thing.isLocked;
        newThing.isInvisibleToEditors = thing.isInvisibleToEditors;
        newThing.suppressScriptsAndStates = thing.suppressScriptsAndStates;
        newThing.suppressCollisions = thing.suppressCollisions;
        newThing.suppressLights = thing.suppressLights;
        newThing.suppressParticles = thing.suppressParticles;
        newThing.suppressHoldable = thing.suppressHoldable;
        newThing.suppressShowAtDistance = thing.suppressShowAtDistance;

        newThing.distanceToShow = thing.distanceToShow;

        Managers.thingManager.MakeDeepThingClone(thing.gameObject, newThingObject,
                alsoCloneIfNotContainingScript: true, isForPlacement: true);

        GameObject oldThing = thing.gameObject;
        string oldPlacementId = thing.placementId;

        thing = newThingObject.GetComponent<Thing>();
        thing.placementId = oldPlacementId;
        thing.MemorizeOriginalTransform(isOriginalPlacement: true);
        thing.AutoUpdateAllVisibilityAndCollision();

        Destroy(oldThing);

        Managers.soundManager.Play("success", null, 0.2f);

        return thing;
    }

    public ThingPartBase? GetSubdividableGroup(ThingPartBase baseType)
    {
        ThingPartBase? baseGroup = null;
        if (IsSubdividableCube(baseType))
        {
            baseGroup = ThingPartBase.Cube;
        }
        else if (IsSubdividableQuad(baseType))
        {
            baseGroup = ThingPartBase.Quad;
        }
        return baseGroup;
    }

    public void StartCreateThingViaJson(string json)
    {
        if (json[0] == '{' && CreationHelper.thingBeingEdited == null &&
                (Our.mode == EditModes.Area || Our.mode == EditModes.None))
        {

            string overLimitInfo;
            if (!Managers.thingManager.GetPlacementsReachedLimit(out overLimitInfo))
            {
                Our.SetMode(EditModes.Thing);

                CreationHelper.thingBeingEdited = (GameObject)Instantiate(Managers.thingManager.thingGameObject);
                CreationHelper.thingBeingEdited.name = CreationHelper.thingDefaultName;

                Transform thingTransform = CreationHelper.thingBeingEdited.transform;
                thingTransform.parent = Managers.thingManager.placements.transform;
                thingTransform.rotation = Quaternion.identity;
                thingTransform.localScale = Vector3.one;

                GameObject headCore = Managers.treeManager.GetObject("/OurPersonRig/HeadCore");
                thingTransform.position = headCore.transform.position + headCore.transform.forward * 1f;

                try
                {
                    JsonToThingConverter.SetThing(CreationHelper.thingBeingEdited, json, alwaysKeepThingPartsSeparate: true);
                    Managers.soundManager.Play("putDown");
                    Managers.dialogManager.SwitchToNewDialog(DialogType.Create);
                }
                catch
                {
                    Managers.soundManager.Play("no");
                    Our.SetPreviousMode();
                    Destroy(CreationHelper.thingBeingEdited);
                    CreationHelper.thingBeingEdited = null;
                    const string text = "Creation import cancelled as thing definition was invalid. " +
                            "See anyland.com/info/thing-format.html";
                    Managers.dialogManager.ShowInfo(text, textColor: TextColor.Red);
                }

            }
            else
            {
                Managers.dialogManager.ShowInfo(overLimitInfo);
            }

        }
    }

    public void UpdateShowThingPartDirectionArrows(Thing thing, bool doShow)
    {
        Component[] thingParts = thing.GetComponentsInChildren<ThingPart>();
        foreach (ThingPart thingPart in thingParts)
        {
            DirectionArrows directionArrows = thingPart.GetComponent<DirectionArrows>();

            if (thingPart.showDirectionArrowsWhenEditing && doShow)
            {
                if (directionArrows == null)
                {
                    thingPart.gameObject.AddComponent<DirectionArrows>();
                }
            }
            else
            {
                if (directionArrows != null)
                {
                    Destroy(directionArrows.arrows);
                    Destroy(directionArrows);
                }
            }

        }
    }

    public bool IsSubdividableCube(ThingPartBase baseType)
    {
        ThingPartBase[] baseTypes = {
            ThingPartBase.Cube,

            ThingPartBase.Cube3x2,
            ThingPartBase.Cube4x2,
            ThingPartBase.Cube5x2,
            ThingPartBase.Cube6x2,

            ThingPartBase.Cube2x3,
            ThingPartBase.Cube3x3,
            ThingPartBase.Cube4x3,
            ThingPartBase.Cube5x3,
            ThingPartBase.Cube6x3,

            ThingPartBase.Cube2x4,
            ThingPartBase.Cube3x4,
            ThingPartBase.Cube4x4,
            ThingPartBase.Cube5x4,
            ThingPartBase.Cube6x4,

            ThingPartBase.Cube2x5,
            ThingPartBase.Cube3x5,
            ThingPartBase.Cube4x5,
            ThingPartBase.Cube5x5,
            ThingPartBase.Cube6x5,

            ThingPartBase.Cube6x6,

            ThingPartBase.Cube5x6deprecated,
        };
        return Array.IndexOf(baseTypes, baseType) >= 0;
    }

    public bool IsSubdividableQuad(ThingPartBase baseType)
    {
        ThingPartBase[] baseTypes = {
            ThingPartBase.Quad,

            ThingPartBase.Quad3x2,
            ThingPartBase.Quad4x2,
            ThingPartBase.Quad5x2,
            ThingPartBase.Quad6x2,

            ThingPartBase.Quad2x3,
            ThingPartBase.Quad3x3,
            ThingPartBase.Quad4x3,
            ThingPartBase.Quad5x3,
            ThingPartBase.Quad6x3,

            ThingPartBase.Quad2x4,
            ThingPartBase.Quad3x4,
            ThingPartBase.Quad4x4,
            ThingPartBase.Quad5x4,
            ThingPartBase.Quad6x4,

            ThingPartBase.Quad2x5,
            ThingPartBase.Quad3x5,
            ThingPartBase.Quad4x5,
            ThingPartBase.Quad5x5,
            ThingPartBase.Quad6x5,

            ThingPartBase.Quad6x6
        };
        return Array.IndexOf(baseTypes, baseType) >= 0;
    }

    void InitSettings()
    {
        texturePropertyAbbreviations =
            new Dictionary<TextureProperty, string>() {
                {TextureProperty.ScaleX,   "x"},
                {TextureProperty.ScaleY,   "y"},
                {TextureProperty.Strength, "a"},
                {TextureProperty.OffsetX,  "m"},
                {TextureProperty.OffsetY,  "n"},
                {TextureProperty.Rotation, "r"},
                {TextureProperty.Glow,     "g"},
                {TextureProperty.Param1,   "o"},
                {TextureProperty.Param2,   "t"},
                {TextureProperty.Param3,   "e"}
            };

        texturePropertyDefault =
            new Dictionary<TextureProperty, float>() {
                {TextureProperty.ScaleX,    0.5f},
                {TextureProperty.ScaleY,    0.5f},
                {TextureProperty.OffsetX,   0f},
                {TextureProperty.OffsetY,   0f},
                {TextureProperty.Strength,  0.5f},
                {TextureProperty.Rotation,  0f},
                {TextureProperty.Glow,      0f},
                {TextureProperty.Param1,    0.5f},
                {TextureProperty.Param2,    0.5f},
                {TextureProperty.Param3,    0.5f}
            };

        textureTypeWithOnlyAlphaSetting =
            new Dictionary<TextureType, bool>() {
                {TextureType.SideGlow, true},
                {TextureType.Wireframe, true},
                {TextureType.Outline, true}
            };

        algorithmTextureTypes =
            new Dictionary<TextureType, bool>() {
                {TextureType.Gradient,      true},
                {TextureType.PerlinNoise1,  true},
                {TextureType.QuasiCrystal,  true},
                {TextureType.VoronoiDots,   true},
                {TextureType.VoronoiPolys,  true},
                {TextureType.WavyLines,     true},
                {TextureType.WoodGrain,     true},
                {TextureType.PlasmaNoise,   true},
                {TextureType.Pool,          true},
                {TextureType.Bio,           true},
                {TextureType.FractalNoise,  true},
                {TextureType.LightSquares,  true},
                {TextureType.Machine,       true},
                {TextureType.SweptNoise,    true},
                {TextureType.Abstract,      true},
                {TextureType.Dashes,        true},
                {TextureType.LayeredNoise,  true},
                {TextureType.SquareRegress, true},
                {TextureType.Swirly,        true}
            };

        textureAlphaCaps =
            new Dictionary<TextureType, float>() {
                {TextureType.QuasiCrystal,  0.5f},
                {TextureType.VoronoiDots,   0.5f},
                {TextureType.VoronoiPolys,  0.5f},
                {TextureType.WavyLines,     0.5f},
                {TextureType.WoodGrain,     0.5f},
                {TextureType.Machine,       0.5f},
                {TextureType.Dashes,        0.5f},
                {TextureType.SquareRegress, 0.5f},
                {TextureType.Swirly,        0.5f}
            };

        textureExtraParamsNumber =
            new Dictionary<TextureType, int>() {
                {TextureType.Gradient,      1},
                {TextureType.PerlinNoise1,  1},
                {TextureType.QuasiCrystal,  1},
                {TextureType.VoronoiDots,   3},
                {TextureType.VoronoiPolys,  3},
                {TextureType.WavyLines,     2},
                {TextureType.WoodGrain,     3},
                {TextureType.PlasmaNoise,   3},
                {TextureType.Pool,          3},
                {TextureType.Bio,           2},
                {TextureType.FractalNoise,  3},
                {TextureType.LightSquares,  3},
                {TextureType.SweptNoise,    3},
                {TextureType.Abstract,      2},
                {TextureType.LayeredNoise,  2},
                {TextureType.SquareRegress, 3},
                {TextureType.Swirly,        3}
            };

        particleSystemPropertyAbbreviations =
            new Dictionary<ParticleSystemProperty, string>() {
                {ParticleSystemProperty.Amount,  "m"},
                {ParticleSystemProperty.Alpha,   "a"},
                {ParticleSystemProperty.Speed,   "s"},
                {ParticleSystemProperty.Size,    "z"},
                {ParticleSystemProperty.Gravity, "g"},
                {ParticleSystemProperty.Shape,   "h"}
            };

        particleSystemPropertyDefault =
            new Dictionary<ParticleSystemProperty, float>() {
                {ParticleSystemProperty.Amount,  0.25f},
                {ParticleSystemProperty.Alpha,   0.25f},
                {ParticleSystemProperty.Speed,   0.15f},
                {ParticleSystemProperty.Size,    0.25f},
                {ParticleSystemProperty.Gravity, 0.5f},
                {ParticleSystemProperty.Shape,   0.25f}
            };

        particleSystemTypeWithOnlyAlphaSetting =
            new Dictionary<ParticleSystemType, bool>() {
                {ParticleSystemType.NoisyWater,     true},
                {ParticleSystemType.GroundSmoke,    true},
                {ParticleSystemType.Rain,           true},
                {ParticleSystemType.Fog,            true},
                {ParticleSystemType.TwistedSmoke,   true},
                {ParticleSystemType.Embers,         true},
                {ParticleSystemType.Beams,          true},
                {ParticleSystemType.Rays,           true},
                {ParticleSystemType.CircularSmoke,  true},
                {ParticleSystemType.PopSmoke,       true},
                {ParticleSystemType.WaterFlow,      true},
                {ParticleSystemType.WaterFlowSoft,  true},
                {ParticleSystemType.Sparks,         true},
                {ParticleSystemType.Flame,          true}
            };

        thingPartBasesSupportingReflectionParts = new ThingPartBase[] {
            ThingPartBase.Cube,
            ThingPartBase.Pyramid,
            ThingPartBase.LowPolySphere,
            ThingPartBase.Icosphere,
            ThingPartBase.Ramp,
            ThingPartBase.JitterCube,
            ThingPartBase.Cone,
            ThingPartBase.HalfSphere,
            ThingPartBase.Trapeze,
            ThingPartBase.Sphere,
            ThingPartBase.Cylinder,
            ThingPartBase.Spike,
            ThingPartBase.JitterSphere,
            ThingPartBase.LowPolyCylinder,
            ThingPartBase.ChamferCube,
            ThingPartBase.CubeBevel1,
            ThingPartBase.Ring1,
            ThingPartBase.Ring2,
            ThingPartBase.Ring3,
            ThingPartBase.Ring4,
            ThingPartBase.Ring5,
            ThingPartBase.Ring6,
            ThingPartBase.CubeBevel2,
            ThingPartBase.CubeBevel3,
            ThingPartBase.CubeRotated,
            ThingPartBase.CurvedRamp,
            ThingPartBase.Hexagon,
            ThingPartBase.HexagonBevel,
            ThingPartBase.Capsule,
            ThingPartBase.HalfCylinder,
            ThingPartBase.RoundCube,
            ThingPartBase.QuarterSphereRotated,
            ThingPartBase.Octagon,
            ThingPartBase.HighPolySphere,
            ThingPartBase.BowlCube,
            ThingPartBase.BowlCubeSoft,
            ThingPartBase.Wheel,
            ThingPartBase.WheelVariant,
            ThingPartBase.Wheel2,
            ThingPartBase.Wheel2Variant,
            ThingPartBase.Wheel3,
            ThingPartBase.Wheel4,
            ThingPartBase.Bowl1Soft,
            ThingPartBase.Bowl1,
            ThingPartBase.Bowl2,
            ThingPartBase.Bowl3,
            ThingPartBase.Bowl4,
            ThingPartBase.Bowl5,
            ThingPartBase.Bowl6,
            ThingPartBase.CubeHole,
            ThingPartBase.HalfCubeHole,
            ThingPartBase.LowJitterCube,
            ThingPartBase.LowJitterCubeSoft,
            ThingPartBase.JitterChamferCylinder,
            ThingPartBase.JitterChamferCylinderSoft,
            ThingPartBase.JitterHalfCone,
            ThingPartBase.JitterHalfConeSoft,
            ThingPartBase.JitterCone,
            ThingPartBase.JitterConeSoft,
            ThingPartBase.Gear,
            ThingPartBase.GearVariant,
            ThingPartBase.GearVariant2,
            ThingPartBase.GearSoft,
            ThingPartBase.GearVariantSoft,
            ThingPartBase.GearVariant2Soft,
            ThingPartBase.Rocky,
            ThingPartBase.RockySoft,
            ThingPartBase.RockyVerySoft,
            ThingPartBase.Spikes,
            ThingPartBase.SpikesSoft,
            ThingPartBase.SpikesVerySoft,
            ThingPartBase.HoleWall,
            ThingPartBase.JaggyWall,
            ThingPartBase.WavyWall,
            ThingPartBase.JitterCubeSoft,
            ThingPartBase.JitterSphereSoft
        };

        thingPartBasesSupportingLimitedReflectionParts = new ThingPartBase[] {
            ThingPartBase.Drop,
            ThingPartBase.Drop2,
            ThingPartBase.Drop3,
            ThingPartBase.DropSharp,
            ThingPartBase.DropSharp2,
            ThingPartBase.DropSharp3,
            ThingPartBase.Drop3Flat,
            ThingPartBase.DropSharp3Flat,
            ThingPartBase.DropCut,
            ThingPartBase.DropSharpCut,
            ThingPartBase.DropRing,
            ThingPartBase.DropRingFlat,
            ThingPartBase.DropPear,
            ThingPartBase.DropPear2,
            ThingPartBase.Drop3Jitter,
            ThingPartBase.SharpBent,
            ThingPartBase.Tetrahedron,
            ThingPartBase.Pipe,
            ThingPartBase.Pipe2,
            ThingPartBase.Pipe3,
            ThingPartBase.ShrinkDisk,
            ThingPartBase.ShrinkDisk2,
            ThingPartBase.DirectionIndicator,
            ThingPartBase.Quad,
            ThingPartBase.WavyWallVariant,
            ThingPartBase.WavyWallVariantSoft,
        };

        smoothingAngles = new Dictionary<ThingPartBase, int>() {
            {ThingPartBase.Bowl1, 80},
            {ThingPartBase.Bowl2, 80},
            {ThingPartBase.Bowl3, 80},
            {ThingPartBase.Bowl4, 80},
            {ThingPartBase.Bowl5, 80},
            {ThingPartBase.Bowl6, 80},
            {ThingPartBase.Bowl1Soft, 140},
            {ThingPartBase.BowlCube, 50},
            {ThingPartBase.BowlCubeSoft, 80},
            {ThingPartBase.CubeHole, 80},
            {ThingPartBase.Octagon, 10},
            {ThingPartBase.HalfBowlSoft, 140},
            {ThingPartBase.HalfCubeHole, 80},
            {ThingPartBase.HalfCylinder, 80},
            {ThingPartBase.Heptagon, 10},
            {ThingPartBase.Pentagon, 10},
            {ThingPartBase.QuarterBowlCube, 50},
            {ThingPartBase.QuarterBowlCubeSoft, 80},
            {ThingPartBase.QuarterBowlSoft, 140},
            {ThingPartBase.QuarterCylinder, 80},
            {ThingPartBase.QuarterPipe1, 80},
            {ThingPartBase.QuarterPipe2, 80},
            {ThingPartBase.QuarterPipe3, 80},
            {ThingPartBase.QuarterPipe4, 80},
            {ThingPartBase.QuarterPipe5, 80},
            {ThingPartBase.QuarterPipe6, 80},
            {ThingPartBase.QuarterSphere, 60},
            {ThingPartBase.Ring1, 180},
            {ThingPartBase.Ring2, 180},
            {ThingPartBase.Ring3, 180},
            {ThingPartBase.Ring4, 180},
            {ThingPartBase.Ring5, 180},
            {ThingPartBase.Ring6, 180},
            {ThingPartBase.SphereEdge, 80},
            {ThingPartBase.QuarterTorus1, 180},
            {ThingPartBase.QuarterTorus2, 180},
            {ThingPartBase.QuarterTorus3, 180},
            {ThingPartBase.QuarterTorus4, 180},
            {ThingPartBase.QuarterTorus5, 180},
            {ThingPartBase.QuarterTorus6, 180},
            {ThingPartBase.QuarterTorusRotated1, 180},
            {ThingPartBase.QuarterTorusRotated2, 180},
            {ThingPartBase.QuarterTorusRotated3, 180},
            {ThingPartBase.QuarterTorusRotated4, 180},
            {ThingPartBase.QuarterTorusRotated5, 180},
            {ThingPartBase.QuarterTorusRotated6, 180},
            {ThingPartBase.QuarterSphereRotated, 80},
            {ThingPartBase.Branch, 120},
            {ThingPartBase.FineSphere, 140},
            {ThingPartBase.GearSoft, 120},
            {ThingPartBase.GearVariantSoft, 120},
            {ThingPartBase.GearVariant2Soft, 60},
            {ThingPartBase.HalfSphere, 60},
            {ThingPartBase.JitterChamferCylinderSoft, 120},
            {ThingPartBase.JitterConeSoft, 120},
            {ThingPartBase.JitterCubeSoft, 120},
            {ThingPartBase.JitterHalfConeSoft, 90},
            {ThingPartBase.JitterSphereSoft, 120},
            {ThingPartBase.LowJitterCubeSoft, 120},
            {ThingPartBase.Pipe, 80},
            {ThingPartBase.Pipe2, 80},
            {ThingPartBase.Pipe3, 80},
            {ThingPartBase.Wheel, 20},
            {ThingPartBase.Wheel2, 20}, // 21
            {ThingPartBase.Wheel3, 15},
            {ThingPartBase.Wheel4, 60},
            {ThingPartBase.Bubbles, 120},
            {ThingPartBase.HoleWall, 60},
            {ThingPartBase.JaggyWall, 10},
            {ThingPartBase.WavyWall, 25},
            {ThingPartBase.WavyWallVariantSoft, 30},
            {ThingPartBase.Spikes, 15},
            {ThingPartBase.SpikesSoft, 90},
            {ThingPartBase.SpikesVerySoft, 160},
            {ThingPartBase.Rocky, 15},
            {ThingPartBase.RockySoft, 30},
            {ThingPartBase.RockyVerySoft, 120},
            {ThingPartBase.Drop, 60},
            {ThingPartBase.Drop2, 80},
            {ThingPartBase.Drop3, 80},
            {ThingPartBase.Drop3Jitter, 35},
            {ThingPartBase.DropBent, 80},
            {ThingPartBase.DropBent2, 80},
            {ThingPartBase.DropCut, 80},
            {ThingPartBase.DropPear, 80},
            {ThingPartBase.DropPear2, 80},
            {ThingPartBase.DropRing, 80},
            {ThingPartBase.DropSharp, 80},
            {ThingPartBase.DropSharp2, 80},
            {ThingPartBase.DropSharp3, 80},
            {ThingPartBase.DropSharpCut, 80},
            {ThingPartBase.SharpBent, 80},
            {ThingPartBase.ShrinkDisk, 60},
            {ThingPartBase.ShrinkDisk2, 60},
            {ThingPartBase.Sphere, 60}
        };

    }

}
