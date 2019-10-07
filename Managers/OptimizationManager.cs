using UnityEngine;
using System;
using System.Linq;

public class OptimizationManager : MonoBehaviour, IGameManager
{
    // Manages graphics downgrading & other speed optimizations.
    // Max limits as below apply only to enabled gameObjects and their behavior,
    // e.g. for maxLightsAround = N it's still possible to place many more
    // (and objects simply get disabled or change behavior when further away).

	public ManagerStatus	status 	{ get; private set; }
	public string 			failMessage { get; private set; }

	public bool doOptimizeSpeed         { get; private set; }
	public bool extraEffectsEvenInVR    { get; private set; }
	
	public int maxThingsAround          { get; private set; }
	public int maxLightsAround          { get; private set; }
    public int maxLightsToThrowShadows  { get; private set; }
	public int maxBaseLayerParticleSystemsAround { get; private set; }
	public int maxParticleSystemsAround { get; private set; }
	public int maxThrownOrEmittedThingsForEmitting { get; private set; }
    public int maxImagePartsAroundCount { get; private set; }

    public bool hideTextures { get; private set; }
    
    public GameObject cookieLight;

	const string optimizeSpeed_keyName = "doOptimizeSpeed";
	const string extraEffectsEvenInVR_keyName = "extraEffectsEvenInVR";
	
	const int defaultQualityIndex           = 0;
    const int optimizedForSpeedQualityIndex = 1;

    public const int maxThingsAroundDefault = 250;
    public const int maxLightsAroundDefault = 3;
    public const int maxLightsToThrowShadowsDefault = 1;
    
    public int? maxThingsAroundOverride = null;
    public int? maxLightsAroundOverride = null;
    public int? maxLightsToThrowShadowsOverride = null;

    public bool findOptimizations { get; private set; }
        
    [SerializeField]
    GameObject lagIndicatorPrefab;
    GameObject lagIndicator = null;
                
    Vector3 farAway = new Vector3(0f, 10000f, 0f);

	public void Startup()
	{
		status = ManagerStatus.Initializing;
		
		doOptimizeSpeed      = PlayerPrefs.GetInt(optimizeSpeed_keyName,        0) == 1;
		extraEffectsEvenInVR = PlayerPrefs.GetInt(extraEffectsEvenInVR_keyName, 0) == 1;
		
		hideTextures = false;
		UpdateSettings();
		
		status = ManagerStatus.Started;
	}
	
	void Update()
	{
        if ( findOptimizations && Misc.Chance(1f) )
        {
            lagIndicator.transform.position = farAway;
        }
	}

    public void SetDoOptimizeSpeed(bool state, bool doRefreshScene = false, bool ignoreUpdateSettings = false)
    {
        if (state && extraEffectsEvenInVR)
        {
            SetExtraEffectsEvenInVR(false, ignoreUpdateSettings: true);
            Managers.filterManager.ApplySettings();
        }
        
        doOptimizeSpeed = state;
        PlayerPrefs.SetInt(optimizeSpeed_keyName, doOptimizeSpeed ? 1 : 0);

        if (!ignoreUpdateSettings)
        {
            UpdateSettings();
            Managers.filterManager.ApplySettings();
        }
        
        if (doRefreshScene) { SetPlacementsActiveBasedOnDistance(); }
    }

    public void SetFindOptimizations(bool state)
    {
        findOptimizations = state;
        if (findOptimizations)
        {
            if (lagIndicator == null)
            {
                lagIndicator = Instantiate(lagIndicatorPrefab) as GameObject;
                Misc.RemoveCloneFromName(lagIndicator);
                lagIndicator.transform.position = farAway;
            }
        }
        else
        {
            Destroy(lagIndicator);
            lagIndicator = null;
        }
    }
    
    public void IndicateScriptActivityHere(Vector3 position)
    {
        if ( findOptimizations && lagIndicator != null && Misc.Chance(5f) )
        {
            lagIndicator.transform.position = position;
        }
    }
    
    public void ResetIndicateScriptActivity()
    {
        if (findOptimizations && lagIndicator != null)
        {
            ParticleSystem particleSystem = lagIndicator.GetComponent<ParticleSystem>();
            particleSystem.Clear();
            lagIndicator.transform.position = farAway;
        }
    }

    public void SetExtraEffectsEvenInVR(bool state, bool ignoreUpdateSettings = false)
    {
        if (state && doOptimizeSpeed)
        {
            SetDoOptimizeSpeed(false, ignoreUpdateSettings: true);
        }

        extraEffectsEvenInVR = state;
        PlayerPrefs.SetInt(extraEffectsEvenInVR_keyName, extraEffectsEvenInVR ? 1 : 0);
            
        if (!ignoreUpdateSettings)
        {
            UpdateSettings();
            Managers.filterManager.ApplySettings();
        }
    }

    public void UpdateSettings()
    {
        maxThingsAround = maxThingsAroundOverride != null ?
            (int)maxThingsAroundOverride : maxThingsAroundDefault;
        maxLightsAround = maxLightsAroundOverride != null ?
            (int)maxLightsAroundOverride : maxLightsAroundDefault;
        maxLightsToThrowShadows = maxLightsToThrowShadowsOverride != null ?
            (int)maxLightsToThrowShadowsOverride : maxLightsToThrowShadowsDefault;
        maxParticleSystemsAround = 15;
        maxBaseLayerParticleSystemsAround = 4;
        maxThrownOrEmittedThingsForEmitting = 150;
        int qualityLevelIndex = defaultQualityIndex;
        maxImagePartsAroundCount = 50;

        int pixelLightCount = 2;
        bool useCookieLight = false;

        if (CrossDevice.desktopMode)
        {
            maxLightsAround += 2;
            pixelLightCount = 3;
            maxLightsToThrowShadows++;
            useCookieLight = true;
        }
        
        cookieLight.SetActive(useCookieLight);
        
        if (doOptimizeSpeed)
        {
            maxThingsAround = 175;
            pixelLightCount = 1;
            maxLightsAround = 2;
            maxLightsToThrowShadows = 0;
            maxBaseLayerParticleSystemsAround = 2;
            maxParticleSystemsAround = 5;
            qualityLevelIndex = optimizedForSpeedQualityIndex;
        }

        #if UNITY_EDITOR
            bool stopAllSpeedOptimizationsToStressTest = false;
            if (stopAllSpeedOptimizationsToStressTest)
            {
                const int veryHighNumber = 100000;
                maxLightsAround = veryHighNumber;
                maxThingsAround = veryHighNumber;
                maxParticleSystemsAround = veryHighNumber;
                qualityLevelIndex = defaultQualityIndex;
            }
        #endif
        
        if ( qualityLevelIndex != QualitySettings.GetQualityLevel() )
        {
            const bool applyExpensiveChanges = true;
            QualitySettings.SetQualityLevel(qualityLevelIndex, applyExpensiveChanges);
        }

        QualitySettings.pixelLightCount = pixelLightCount;
    }
    
    public void SetPlacementsActiveBasedOnDistance(string placementIdToAlwaysShow = "")
    {
        if (Managers.personManager == null || Managers.personManager.ourPerson == null ||
            Managers.personManager.ourPerson.Head == null ||
            Managers.areaManager == null)
        {
            return;
        }
        Transform ourTransform = Managers.personManager.ourPerson.Head.transform;
        
        GameObject[] things = Misc.GetChildrenAsArray(Managers.thingManager.placements.transform);
        things = things.OrderBy(
            x => Vector3.Distance(ourTransform.position, x.transform.position)
            ).ToArray();

        int thingsCount = 0;
        int baseLayerParticleSystemsCount = 0;
        int particleSystemsCount = 0;
        int lightsCount = 0;
        int lightsThrowingShadowsCount = 0;
        int allPartsImageCount = 0;
        
        const float distanceAtWhichToShowAnything = 2.5f;
 
        foreach (GameObject thingObject in things)
        {
            if (thingObject != CreationHelper.thingThatWasClonedFrom &&
                thingObject != CreationHelper.thingBeingEdited &&
                thingObject.name != Universe.objectNameIfAlreadyDestroyed)
            {
                Thing thing = thingObject.GetComponent<Thing>();
                bool alwaysShowThisOne =
                    ( !string.IsNullOrEmpty(placementIdToAlwaysShow) && thing.placementId == placementIdToAlwaysShow ) ||
                    thing.isHighlighted;
                if (!alwaysShowThisOne)
                {
                    bool forceShow = false;
                    bool forceHide = false;
                    
                    float distance = Vector3.Distance(ourTransform.position, thingObject.transform.position);

                    if (thing.distanceToShow != null && !thing.isHighlighted)
                    {
                        if ( distance <= (float)thing.distanceToShow )
                        {
                            forceShow = true;
                        }
                        else
                        {
                            forceHide = true;
                        }
                    }

                    bool didEnable = false;
                    bool makeExceptionToShowAnyway = false;
                    if (!thing.suppressShowAtDistance)
                    {
                        makeExceptionToShowAnyway = thing.thingPartCount <= 15 &&
                            (thing.isVeryBig || thing.requiresWiderReach || thing.hasSurroundSound);
                        if (thing.benefitsFromShowingAtDistance || thing.temporarilyBenefitsFromShowingAtDistance ||
                            distance <= distanceAtWhichToShowAnything)
                        {
                            makeExceptionToShowAnyway = true;
                        }
                    }
                    
                    if ( (thingsCount < maxThingsAround || makeExceptionToShowAnyway || forceShow) && !forceHide )
                    {
                        thingsCount++;
                        
                        float requiredSizeToShow = 0f;
                        if ( !(thing.containsBehaviorScript && distance <= 35f) )
                        {
                            if      (distance >= 100f) { requiredSizeToShow = 15f; }
                            else if (distance >=  75f) { requiredSizeToShow =  6f; }
                            else if (distance >=  50f) { requiredSizeToShow =  2f; }
                            else if (distance >=  25f) { requiredSizeToShow =  1f; }
                        }
                        
                        bool isFarAwayWithManyParts = distance >= 50f && thing.thingPartCount >= 25;
                        
                        if ( makeExceptionToShowAnyway ||
                            (thing.biggestSize >= requiredSizeToShow && !isFarAwayWithManyParts) || forceShow )
                        {
                            thing.gameObject.SetActive(true);
                            didEnable = true;

                            if (thing.containsBaseLayerParticleSystem)
                            {
                                baseLayerParticleSystemsCount++;
                                bool doOptimizeBaseLayerParticleSystem =
                                    baseLayerParticleSystemsCount > Managers.optimizationManager.maxBaseLayerParticleSystemsAround &&
                                    !thing.benefitsFromShowingAtDistance;
                                AdjustThingParticleSystemsOptimization(thing.gameObject, doOptimizeBaseLayerParticleSystem);
                            }
                            
                            if (thing.containsParticleSystem)
                            {
                                particleSystemsCount++;
                                bool doOptimizeParticleSystem =
                                    particleSystemsCount > Managers.optimizationManager.maxParticleSystemsAround &&
                                    !thing.benefitsFromShowingAtDistance;
                                thing.SetParticleSystemsStopPlay(!doOptimizeParticleSystem);
                            }
                            
                            if (thing.containsLight)
                            {
                                lightsCount++;
                                thing.AdjustLightsOptimization(
                                    doOptimize: lightsCount > Managers.optimizationManager.maxLightsAround && !forceShow);
                                
                                if (lightsThrowingShadowsCount < Managers.optimizationManager.maxLightsToThrowShadows)
                                {
                                    int? limit = Managers.optimizationManager.maxLightsToThrowShadows - lightsThrowingShadowsCount;
                                    int affectedCount = thing.SetLightShadows(true, limit);
                                    lightsThrowingShadowsCount += affectedCount;
                                }
                                else
                                {
                                    thing.SetLightShadows(false);
                                }
                            }
                            
                            if (thing.allPartsImageCount > 0)
                            {
                                allPartsImageCount += thing.allPartsImageCount;
                                bool isWithinLimits =
                                    allPartsImageCount <= Managers.optimizationManager.maxImagePartsAroundCount ||
                                    thing.IsPlacedSubThing() || thing.movableByEveryone;
                                thing.SetImagePartsActive(isWithinLimits);
                            }
                        }
                    }

                    if (!didEnable) { thing.gameObject.SetActive(false); }
                }
                
            }
        }
        
        Managers.areaManager.LimitVisibilityIfNeeded();
    }
    
    void AdjustThingParticleSystemsOptimization(GameObject thing, bool doOptimize)
    {
        Component[] components = thing.gameObject.GetComponentsInChildren( typeof(ThingPart), true );
        foreach (ThingPart thingPart in components)
        {
            if (doOptimize)
            {
                if (thingPart.materialType == MaterialTypes.Particles ||
                    thingPart.materialType == MaterialTypes.ParticlesBig )
                {
                    thingPart.materialTypeBeforeOptimization = thingPart.materialType;
                    thingPart.materialType = MaterialTypes.None;
                    thingPart.ResetStates();
                }
            }
            else
            {
                if (thingPart.materialTypeBeforeOptimization == MaterialTypes.Particles ||
                    thingPart.materialTypeBeforeOptimization == MaterialTypes.ParticlesBig)
                {
                    thingPart.materialType = thingPart.materialTypeBeforeOptimization;
                    thingPart.materialTypeBeforeOptimization = MaterialTypes.None;
                    thingPart.ResetStates();
                }
            }
        }
    }
    
}
