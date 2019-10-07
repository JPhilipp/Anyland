using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class CreationHelper
{
    // Structures helping with Create Thing editing. Many limit values here
    // are shared with the server.

    public static GameObject thingBeingEdited;
    public static GameObject thingPartWhoseStatesAreEdited;
    public static GameObject thingThatWasClonedFrom;
    public static string thingThatWasClonedFromIdIfRelevant = null;
    public static int currentThingPartStateLinesPage = 0;

    public static string thingDefaultName = "thing";
    
    public static bool replaceInstancesInAreaOneTime = false;
    public static bool didSeeAlwaysMergePartsConfirm = false;

    public static TransformClipboard transformClipboard = new TransformClipboard();

    public static Texture2D referenceImage = null;
    public static GameObject referenceObject = null;

    public static Transform lastColorPickTransform;
    public static Vector3 lastColorPickTransformOriginalPosition;
    public static Transform lastExpanderColorPickTransform;
    public static Vector3 lastExpanderColorPickTransformOriginalPosition;
    
    public static int shapesTab = 0;
    public const int maxShapesTab = 10;
    
    public const float thingMassDefault = 1f;
    public const float thingDragDefault = 0f;
    public const float thingAngularDragDefault = 0.05f;
    
    public static float? customSnapAngles = null;
    
    public const int maxThingPartCountPerCreation = 1000;
    public const int thingJsonMaxLength = 300000;
    
    public const int thingDescriptionMaxLength = 225;
    
    public const int maxIncludedSubThings = 1000; 
    public const int maxPlacedSubThings = 100;
    public const int maxPlacedSubThingsToRecreate = 20;
    
    public static bool alreadyExceededMaxThingPartCountOnCloning = false;
    
    public static bool showDialogShapesTab = false;
    
    public static ThingPartStatesCopy statesCopy = null;

    public const int maxThingPartStates = 50;
    public const int maxBehaviorScriptLines = 100;
    public const int maxBehaviorScriptLineLength = 10000;
    
    public const string thingPartDefaultText = "ABC";
    public const int thingPartTextMaxLength = 10000;

    public static Dictionary<MaterialTab,Color> currentColor = new Dictionary<MaterialTab,Color>()
    {
        { MaterialTab.material,       Color.cyan  },
        { MaterialTab.texture1,       Color.black },
        { MaterialTab.texture2,       Color.black },
        { MaterialTab.particleSystem, Color.white }
    };
    public static MaterialTab currentMaterialTab = MaterialTab.material;
    
    public static Dictionary<MaterialTab,Color> currentBaseColor = new Dictionary<MaterialTab,Color>()
    {
        { MaterialTab.material,       Color.cyan  },
        { MaterialTab.texture1,       Color.black },
        { MaterialTab.texture2,       Color.black },
        { MaterialTab.particleSystem, Color.white }
    };
    
    public static TextureProperty[] currentTextureProperty = 
        new TextureProperty[] { TextureProperty.ScaleY, TextureProperty.ScaleY };
    public static ParticleSystemProperty currentParticleSystemProperty = ParticleSystemProperty.Amount;
    
    public static Color lastHueColor = Color.cyan;
    public static Material lastMaterial;
    
    public static string lastScriptLineEntered = "";

    public static bool wasInAreaEditingMode = false;
    
    public static MaterialTypes materialType = MaterialTypes.None;
    public static ParticleSystemType particleSystemType = ParticleSystemType.None;
    public static TextureType[] textureTypes = new TextureType[]
    {
        TextureType.None, TextureType.None
    };
    public const float particleSystemPropertyDefault = 0.25f;
    
    public static DialogType dialogBeforeBrushWasPicked = DialogType.None;
    
    public static Vector3 thingPartPickupPosition = Vector3.zero;
    
    public static int GetTextureIndex()
    {
        int index = -1;
        switch (currentMaterialTab)
        {
            case MaterialTab.texture1: index = 0; break;
            case MaterialTab.texture2: index = 1; break;
        }
        return index;
    }
}
