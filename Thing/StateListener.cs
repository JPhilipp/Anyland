using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StateListener
{
    public enum EventType
    {
        None,
        OnStarts,
        OnTouches,
        OnTouchEnds,
        OnTriggered,
        OnUntriggered,
        OnTold,
        OnToldByNearby,
        OnToldByAny,
        OnToldByBody,
        OnTaken,
        OnGrabbed,
        OnConsumed,
        OnBlownAt,
        OnTalkedFrom,
        OnTalkedTo,
        OnTurnedAround,
        OnLetGo,
        OnHighSpeed,
        OnGets,
        OnNeared,
        OnSomeoneInVicinity,
        OnSomeoneNewInVicinity,
        OnHitting,
        OnShaken,
        OnWalkedInto,
        OnPointedAt,
        OnLookedAt,
        OnRaised,
        OnLowered,
        OnHears,
        OnHearsAnywhere,
        OnJoystickControlled,
        OnDestroyed,
        OnDestroyedRestored,
        OnTyped,
        OnVariableChange,
        OnSettingEnabled,
        OnSettingDisabled,
        
        OnAnyPartTouches,
        OnAnyPartConsumed,
        OnAnyPartHitting,
        OnAnyPartBlownAt,
        OnAnyPartPointedAt,
        OnAnyPartLookedAt,
    };

    public EventType eventType = EventType.None;
    public bool isForAnyState = false;
    
    public int setState = -1;
    public RelativeStateTarget? setStateRelative = null;
    public float setStateSeconds = 0.01f;
    public TweenType tweenType = TweenType.EaseInOut;
    public int curveViaState = -1;
        
    public string whenData = null;
    public string whenIsData = null;
    public string thenData = null;

    public string callMeThisName = null;
    public Vector3 pushToLocationInArea;
    public bool doHapticPulse = false;

    public ThingDestruction destroyThingWeArePartOf = null;
    public OtherThingDestruction destroyOtherThings = null;
    
    public List<Sound> sounds = null;
    
    public string soundTrackData = null;
    
    public string startLoopSoundName = null;
    public bool doEndLoopSound = false;
    public float loopVolume = 0f;
    public float loopSpatialBlend = 0f;
    
    public RotateThingSettings rotateThingSettings = null;

    public string transportToArea = null;
    public string transportOntoThing = null;
    public bool transportMultiplePeople = false;
    public bool transportNearbyOnly = false;
    public float rotationAfterTransport = 0f;
    
    public string transportViaArea = null;
    public float transportViaAreaSeconds = 0;

    public List< KeyValuePair<TellType,string> > tells = null;

    public string emitId;
    public float emitVelocityPercent;
    public bool emitIsGravityFree = false;
    
    public float? propelForwardPercent = null;
    public float? rotateForwardPercent = null;
    
    public bool addCrumbles = false;
    public bool addEffectIsForAllParts = false;
    
    public AreaRights rights = null;
    
    public string creationPartChangeMode = null;
    public float[] creationPartChangeValues;
    public bool creationPartChangeIsForAll = false;
    public bool creationPartChangeIsLocal = false;
    public bool creationPartChangeIsRandom = false;
    
    public float setLightIntensity = -1f;
    public float setLightRange = -1f;
    public float setLightConeSize = -1f;
    
    public string doTypeText = "";

    public bool pauseLerping = false;
    
    public DialogType? showDialog = null;
    public string showData = null;
    
    public Vector3? velocityMultiplier = null;
    public Vector3? velocitySetter = null;
    public Vector3? forceAdder = null;
    
    public List<string> variableOperations = null;
    
    public string attachThingIdAsHead = null;
    public bool attachToMultiplePeople = false;
    
    public bool letGo = false;

    public int goToInventoryPage = 0;

    public float? resizeNearby = null;
    
    public bool? streamMyCameraView = null;
    public string streamTargetName = null;

    public float? showNameTagsAgainSeconds = null;
    
    public string say = null;
    public VoiceProperties setVoiceProperties = null;
    
    public float? setCustomSnapAngles = null;
    
    public FollowerCameraPosition? setFollowerCameraPosition = null;
    public float? setFollowerCameraLerp = null;
    
    public Vector3? setGravity = null;
    
    public ResetSettings resetSettings = null;

    public string setText = null;
    
    public string turn             = null;
    public string turnThing        = null;
    public string turnSubThing     = null;
    public string turnSubThingName = null;

    public string playVideoId = null;
    public float? playVideoVolume = null;
    
    public BrowserSettings browserSettings = null;
    
    public ProjectPartSettings projectPartSettings = null;
    
    public PartLineSettings  partLineSettings  = null;
    public PartTrailSettings partTrailSettings = null;
    
    public float? limitAreaVisibilityMeters = null;
    
    public Vector3? constantRotation = null;
    
    public Dictionary<Setting,bool> settings = null;
    
    public bool makePersonMasterClient = false;
    
    public QuestAction questAction = null;

    public AttractThingsSettings attractThingsSettings = null;
    
    public DesktopModeSettings desktopModeSettings = null;
}
