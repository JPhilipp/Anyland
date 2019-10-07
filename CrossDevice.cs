using UnityEngine;
using Valve.VR;

public static class CrossDevice
{
    // For handling cross-device needs, like different input subtleties
    // between Rift Touch and Vive controllers.

    public static DeviceType type { get; private set; }

    public static bool desktopMode = false;
    public static bool rigPositionIsAuthority = true;
    public static Side desktopDialogSide = Side.Left;
    public static bool oculusTouchLegacyMode = false;
    const string oculusTouchLegacyMode_key = "oculusTouchLegacyMode";
    
    public static bool hasStick = false;
    public static bool hasSeparateTriggerAndGrab = false;
    
    public static ulong button_grab     { get; private set; }
    public static ulong button_grabTip  { get; private set; }
    public static ulong button_teleport { get; private set; }
    public static ulong button_context  { get; private set; }
    public static ulong button_delete   { get; private set; }
    public static ulong button_legPuppeteering { get; private set; }
    
    public static bool joystickWasFound { get; private set; }

    /*
        // Strings may slightly differ, e.g. "Vive. MV" or "Vive MV.", and not just per OS
        const string model_vive = "Vive MV";
        const string model_index = "Valve Index"; // Speculative, we don't know this one yet
        const string model_rift = "Oculus Rift CV1";
        const string model_lenovoExplorer = "Lenovo Explorer";
        const string model_hpWindowsMixedReality = "HP Windows Mixed Reality Headset";
        const string model_samsungOdyssey = "Samsung Windows Mixed Reality 800ZAA";
        const string model_acer = "Acer AH100";
    */

    public static void Init()
    {
		oculusTouchLegacyMode = PlayerPrefs.GetInt(oculusTouchLegacyMode_key, 0) == 1;
        string model = UnityEngine.XR.XRDevice.model != null ?
            UnityEngine.XR.XRDevice.model : "";
        type = GetTypeByModelName(model);
        SetButtonMappingForType();
    }
    
    static string GetModelOverride()
    {
        string modelOverride = null;
        
        string[] names = Input.GetJoystickNames();
        if (names != null)
        {
            foreach (string name in names)
            {
                if ( !string.IsNullOrEmpty(name) &&
                    ( name.Contains("Knuckles") || name.Contains("Index") )
                    )
                {
                    joystickWasFound = true;
                    modelOverride = "Valve Index";
                    // Debug.Log("Found Knuckles due to: " + name);
                    break;
                }
            }
        }

        return modelOverride;
    }

    static DeviceType GetTypeByModelName(string model)
    {
        DeviceType thisType = DeviceType.Other;
        
        string modelOverride = GetModelOverride();
        if ( !string.IsNullOrEmpty(modelOverride) )
        {
            // Debug.Log("Model name was " + model + ", but changing to " + modelOverride);
            model = modelOverride;
        }

        model = model.ToLower();
        
        if ( model.Contains("rift") )
        {
            thisType = DeviceType.OculusTouch;
        }
        else if ( model.Contains("index") )
        {
            thisType = DeviceType.Index;
        }
        else if ( model.Contains("vive") )
        {
            thisType = DeviceType.Vive;
        }
        else if (
            model.Contains("mixed") ||
            model.Contains("lenovo explorer") ||
            model.Contains("acer")
        )
        {
            thisType = DeviceType.WindowsMixedReality;
        }

        return thisType;
    }
    
    static void SetButtonMappingForType()
    {
        ulong buttonXA = ( 1ul << (int)EVRButtonId.k_EButton_A );
        
        switch (type)
        {
            case DeviceType.OculusTouch:
                button_grab    = SteamVR_Controller.ButtonMask.Grip;
                button_grabTip = SteamVR_Controller.ButtonMask.Trigger;
                if (oculusTouchLegacyMode)
                {
                    button_teleport = buttonXA;
                    button_context  = SteamVR_Controller.ButtonMask.ApplicationMenu;
                    button_delete   = SteamVR_Controller.ButtonMask.Touchpad;
                }
                else
                {
                    button_teleport = SteamVR_Controller.ButtonMask.Touchpad;
                    button_context  = SteamVR_Controller.ButtonMask.ApplicationMenu;
                    button_delete   = buttonXA;
                }
                hasStick = true;
                hasSeparateTriggerAndGrab = true;
                break;
                
            case DeviceType.Index:
                button_grab     = SteamVR_Controller.ButtonMask.Grip;
                button_grabTip  = SteamVR_Controller.ButtonMask.Trigger;
                button_teleport = SteamVR_Controller.ButtonMask.Touchpad;
                button_context  = SteamVR_Controller.ButtonMask.ApplicationMenu;
                button_delete   = buttonXA;
                hasStick = true;
                hasSeparateTriggerAndGrab = true;
                break;
                
            case DeviceType.Vive:
            case DeviceType.WindowsMixedReality:
            default:
                button_grab     = SteamVR_Controller.ButtonMask.Trigger;
                button_grabTip  = SteamVR_Controller.ButtonMask.Trigger;
                button_teleport = SteamVR_Controller.ButtonMask.Touchpad;
                button_context  = SteamVR_Controller.ButtonMask.ApplicationMenu;
                button_delete   = SteamVR_Controller.ButtonMask.Grip;
                hasStick = false;
                hasSeparateTriggerAndGrab = false;
                break;
        }
        
        button_legPuppeteering = button_teleport;
    }
    
    public static void AdjustControllerTransformIfNeeded(Transform transform)
    {
        if ( transform.CompareTag("HandCore") )
        {
            switch (type)
            {
                case DeviceType.OculusTouch:
                {
                    transform.Rotate( new Vector3(23.5f, 0f, 0f) );
                    float ourScale = Managers.personManager != null ? Managers.personManager.GetOurScale() : 1f;
                    transform.Translate( Vector3.forward * (-0.02f * ourScale) );
                }
                break;
                    
                case DeviceType.Index:
                {
                    transform.Rotate( new Vector3(23.5f, 0f, 0f) );
                    float ourScale = Managers.personManager != null ? Managers.personManager.GetOurScale() : 1f;
                    transform.Translate( Vector3.forward * (-0.02f * ourScale) );
                }
                break;
                    
                case DeviceType.WindowsMixedReality:
                {
                    transform.Rotate( new Vector3(30f, 0f, 0f) );
                }
                break;

            }
        }
    }
    
    public static bool GetPress(SteamVR_Controller.Device controller, ulong buttonType, Side handSide)
    {
        bool isIt = false;
        if (desktopMode)
        {
            isIt = GetMouseButton(buttonType, handSide);
        }
        else if (controller != null)
        {
            isIt = controller.GetPress(buttonType);
        }
        return isIt;
    }
    
    public static bool GetPressUp(SteamVR_Controller.Device controller, ulong buttonType, Side handSide)
    {
        bool isIt = false;
        if (desktopMode)
        {
            isIt = GetMouseButtonUp(buttonType, handSide);
        }
        else if (controller != null)
        {
            isIt = controller.GetPressUp(buttonType);
        }
        return isIt;
    }
    
    public static bool GetPressDown(SteamVR_Controller.Device controller, ulong buttonType, Side handSide)
    {
        bool isIt = false;
        if (desktopMode)
        {
            isIt = GetMouseButtonDown(buttonType, handSide);
        }
        else if (controller != null)
        {
            isIt = controller.GetPressDown(buttonType);
        }
        return isIt;
    }
    
    static bool GetMouseButton(ulong buttonType, Side handSide)
    {
        bool isIt = false;
        int mouseButton = GetMouseButtonInt(buttonType, handSide);
        if (mouseButton != -1)
        {
            isIt = Input.GetMouseButton(mouseButton);
        }
        return isIt;
    }
    
    static bool GetMouseButtonUp(ulong buttonType, Side handSide)
    {
        bool isIt = false;
        int mouseButton = GetMouseButtonInt(buttonType, handSide);
        if (mouseButton != -1)
        {
            isIt = Input.GetMouseButtonUp(mouseButton);
        }
        return isIt;
    }
    
    static bool GetMouseButtonDown(ulong buttonType, Side handSide)
    {
        bool isIt = false;
        int mouseButton = GetMouseButtonInt(buttonType, handSide);
        if (mouseButton != -1)
        {
            isIt = Input.GetMouseButtonDown(mouseButton);
        }
        return isIt;
    }
    
    public static int GetMouseButtonInt(ulong buttonType, Side handSide)
    {
        int button = -1;
        if (buttonType == button_grab || buttonType == button_grabTip)
        {
            if ( Our.GetCurrentNonStartDialog() == null )
            {
                button = 0;
            }
        }
        else if (buttonType == button_teleport)
        {
        }
        else if (buttonType == button_delete)
        {
            button = 2;
        }
        else if (buttonType == button_context)
        {
            if (handSide == desktopDialogSide)
            {
                button = 1;
            }
        }
        return button;
    }
    
    public static void TriggerHapticPulse(Hand hand, ushort pulseAmount)
    {
        if (Managers.areaManager != null && Managers.areaManager.startedLaunchFadeIn &&
            hand != null && Universe.features.hapticPulse)
        {
            if (desktopMode)
            {
                Managers.soundManager.Play("bump", hand.transform, 0.05f);
            }
            else
            {
                if (hand.controller != null && hand.controller.connected)
                {
                    hand.controller.TriggerHapticPulse(pulseAmount);
                }
            }
        }
    }
    
    public static void SetOculusTouchLegacyMode(bool state)
    {
        oculusTouchLegacyMode = state;
        PlayerPrefs.SetInt(oculusTouchLegacyMode_key, state ? 1 : 0);
        SetButtonMappingForType();
    }
    
}
