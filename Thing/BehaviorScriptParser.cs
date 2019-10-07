using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class BehaviorScriptParser
{
    // For parsing the user-screated, so-called Behavior Scripts,
    // which can run on Thing Parts.

    public const float minStateSeconds = 0.05f;
    public const float maxStateSeconds = 30f;
    public const float magnitudeConsideredFast = 1.15f;
    const float minRelativeSoundVolume = 0.001f;
    const float maxRelativeSoundVolume = 5f;
    const string commaKey = "[comma]";
    public const float maxTrailDurationSeconds = 60f;

    public const string areaVariablePrefix   = "area.";
    public const string personVariablePrefix = "person.";
    
    static string[] stringsToGetWithUnderlines = new string[]
    {
        "any part touches",
        "any part consumed",
        "any part hitting",
        "any part blown at",
        "any part pointed at",
        "any part looked at",
        "play url",
        "trigger let go",
        "neared by",
        "tell nearby",
        "tell any web",
        "tell any",
        "tell body",
        "tell first of any",
        "tell web",
        "told by nearby",
        "told by any",
        "told by body",
        "send nearby to",
        "send nearby onto",
        "send one nearby to",
        "send one nearby onto",
        "send all to",
        "send all onto",
        "call me",
        "talked to",
        "talked from",
        "pointed at",
        "looked at",
        "turned around",
        "high speed",
        "end loop",
        "let go",
        "blown at",
        "walked into",
        "someone new in vicinity",
        "someone in vicinity",
        "turn thing",
        "turn sub-thing",
        "stop all parts face someone",
        "stop all parts face up",
        "stop all parts face empty hand",
        "stop all parts face nearest",
        "stop all parts face view",
        "all parts face someone else",
        "all parts face someone",
        "all parts face up",
        "all parts face empty hand while held",
        "all parts face empty hand",
        "all parts face nearest",
        "all parts face view",
        "become untweened",
        "become unsoftened",
        "become soft start",
        "become soft end",
        "become stopped",
        "emit gravity-free",
        "destroy all parts",
        "destroy nearby",
        "give haptic feedback",
        "propel forward",
        "rotate forward",
        "disallow any person size",
        "disallow emitted climbing",
        "disallow emitted transporting",
        "disallow invisibility",
        "disallow highlighting",
        "disallow amplified speech",
        "disallow any destruction",
        "disallow web browsing",
        "disallow untargeted attract and repel",
        "disallow build animations",
        "allow any person size",
        "allow emitted climbing",
        "allow emitted transporting",
        "allow invisibility",
        "allow highlighting",
        "allow amplified speech",
        "allow any destruction",
        "allow web browsing",
        "allow untargeted attract and repel",
        "allow build animations",
        "show slideshow controls",
        "show camera controls",
        "show video controls",
        "show name tags",
        "show video",
        "show board",
        "show thread",
        "show areas",
        "show web",
        "show inventory",
        "show chat keyboard",
        "show line",
        "go to inventory page",
        "touch ends",
        "set light intensity",
        "set light range",
        "set light cone size",
        "set constant rotation to",
        "add crumbles for all parts",
        "set snap angles to",
        "set run speed",
        "set jump speed",
        "set slidiness",
        "add crumbles",
        "do creation part",
        "do all creation parts",
        "material transparent glossy metallic",
        "material very transparent glossy",
        "material slightly transparent",
        "material transparent glossy",
        "material very transparent",
        "material bright metallic",
        "material very metallic",
        "material dark metallic",
        "material transparent texture",
        "material transparent",
        "material metallic",
        "material default",
        "material plastic",
        "material unshiny",
        "material glow",
        "fire with alarm",
        "filling with air",
        "set speed",
        "add speed",
        "multiply speed",
        "change head to",
        "change heads to",
        "align to surface",
        "resize nearby to",
        "stream to",
        "stream stop",
        "set voice",
        "hears anywhere",
        "play track loop",
        "play track",
        "set camera position to",
        "set camera following to",
        "set camera",
        "insert state",
        "remove state",
        "when any state",
        "set gravity to",
        "reset area",
        "reset persons",
        "reset position",
        "reset rotation",
        "reset body",
        "reset legs to default",
        "reset legs to body default",
        "destroyed restores",
        "trail start",
        "trail end",
        "set area visibility to",
        "enable setting",
        "disable setting",
        "set person as authority",
        "set quest achieve",
        "set quest unachieve",
        "set quest remove",
        "set quest",
        "set attract",
        "set repel"
    };
    
    static string[] textPlaceholdersFull =
    {
        "year", "month", "day", "hour", "hour 12", "minute", "second", "millisecond",
        "local hour", "local hour 12", "month unpadded", "day unpadded", "hour unpadded",
        "hour 12 unpadded", "local hour 12 unpadded", "closest person", "closest held",
        "area name", "thing name", "x", "y", "z", "people names", "people count", "typed",
        "area values", "thing values", "person values", "proximity", "url", "person"
    };

    static string[] textPlaceholdersStartsWith =
    {
        "area.", "person."
    };
    
    static string[] turnCommands =
    {
        "on", "off", "visible", "invisible", "collidable", "uncollidable"
    };

    public static StateListener GetStateListenerFromScriptLine(string line, Thing parentThing, ThingPart parentThingPart)
    {
        StateListener listener = new StateListener();

        line = NormalizeLine(line, parentThing);
        line = EscapeCommaIfInQuotes(line);

        char[] sentenceSeparators = {','};
        string[] sentences = line.Split(sentenceSeparators, StringSplitOptions.RemoveEmptyEntries);

        // E.g. [when told button pressed then play foo, tell hello]
        // or   [when touched then type "when starts then play doorbell"]

        string whenPart = "";
        for (int sentenceI = 0; sentenceI < sentences.Length; sentenceI++)
        {
            string sentence = sentences[sentenceI];
            sentence = sentence.Replace(commaKey, ",");
            string thenPart = "";
            
            listener.isForAnyState = sentence.Contains("when_any_state");
            if (listener.isForAnyState)
            {
                parentThingPart.containsForAnyStateListeners = true;
                sentence = sentence.Replace("when_any_state", "when");
            }

            if (sentenceI == 0)
            {
                string[] thenSeparators = new string[] {" then "};
                string[] whenAndThen = sentence.Split(thenSeparators, StringSplitOptions.RemoveEmptyEntries);
                
                if (whenAndThen.Length == 2)
                {
                    whenPart = whenAndThen[0];
                    thenPart = whenAndThen[1];
                    whenPart = whenPart.Replace("when ", "");
                }
                else if (whenAndThen.Length >= 3)
                {
                    whenPart = whenAndThen[0];
                    thenPart = String.Join(" then ", whenAndThen, 1, whenAndThen.Length - 1);
                    whenPart = whenPart.Replace("when ", "");
                }
            }
            else
            {
                thenPart = sentence;
            }
            
            if (whenPart != "" && thenPart != "")
            {
                const string andIsIndicator = " and is ";
                int whenIsIndex = whenPart.IndexOf(andIsIndicator);
                if (whenIsIndex >= 0)
                {
                    string[] beforeAfterWhenIs = Misc.Split(whenPart, andIsIndicator);
                    if (beforeAfterWhenIs.Length == 2)
                    {
                        whenPart = beforeAfterWhenIs[0].Trim();
                        listener.whenIsData = beforeAfterWhenIs[1].Trim();
                    }
                }

                char[] separators = {' '};
                string[] whenWords = whenPart.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                string[] thenWords = thenPart.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                if (whenWords.Length >= 1 && thenWords.Length >= 1)
                {
                    switch (whenWords[0])
                    {
                        case "starts":         listener.eventType = StateListener.EventType.OnStarts; break;
                        case "touches":        listener.eventType = StateListener.EventType.OnTouches; break;
                        case "any_part_touches": listener.eventType = StateListener.EventType.OnAnyPartTouches; break;
                        case "touch_ends":     listener.eventType = StateListener.EventType.OnTouchEnds; break;
                        case "triggered":      listener.eventType = StateListener.EventType.OnTriggered; break;
                        case "trigger_let_go": listener.eventType = StateListener.EventType.OnUntriggered; break;
                        case "neared":         listener.eventType = StateListener.EventType.OnNeared; break;
                        case "hitting":        listener.eventType = StateListener.EventType.OnHitting; break;
                        case "any_part_hitting": listener.eventType = StateListener.EventType.OnAnyPartHitting; break;
                        case "told":           listener.eventType = StateListener.EventType.OnTold; break;
                        case "told_by_nearby": listener.eventType = StateListener.EventType.OnToldByNearby; break;
                        case "told_by_any":    listener.eventType = StateListener.EventType.OnToldByAny; break;
                        case "told_by_body":   listener.eventType = StateListener.EventType.OnToldByBody; break;
                        case "taken":          listener.eventType = StateListener.EventType.OnTaken; break;
                        case "grabbed":        listener.eventType = StateListener.EventType.OnGrabbed; break;
                        case "let_go":         listener.eventType = StateListener.EventType.OnLetGo; break;
                        case "consumed":       listener.eventType = StateListener.EventType.OnConsumed; break;
                        case "any_part_consumed": listener.eventType = StateListener.EventType.OnAnyPartConsumed; break;
                        case "talked_to":      listener.eventType = StateListener.EventType.OnTalkedTo; break;
                        case "talked_from":    listener.eventType = StateListener.EventType.OnTalkedFrom; break;
                        case "pointed_at":     listener.eventType = StateListener.EventType.OnPointedAt; break;
                        case "any_part_pointed_at": listener.eventType = StateListener.EventType.OnAnyPartPointedAt; break;
                        case "looked_at":      listener.eventType = StateListener.EventType.OnLookedAt; break;
                        case "any_part_looked_at": listener.eventType = StateListener.EventType.OnAnyPartLookedAt; break;
                        case "turned_around":  listener.eventType = StateListener.EventType.OnTurnedAround; break;
                        case "shaken":         listener.eventType = StateListener.EventType.OnShaken; break;
                        case "high_speed":     listener.eventType = StateListener.EventType.OnHighSpeed; break;
                        case "gets":           listener.eventType = StateListener.EventType.OnGets; break;
                        case "walked_into":    listener.eventType = StateListener.EventType.OnWalkedInto; break;
                        case "raised":         listener.eventType = StateListener.EventType.OnRaised; break;
                        case "lowered":        listener.eventType = StateListener.EventType.OnLowered; break;
                        case "blown_at":       listener.eventType = StateListener.EventType.OnBlownAt; break;
                        case "typed":          listener.eventType = StateListener.EventType.OnTyped; break;
                        case "any_part_blown_at": listener.eventType = StateListener.EventType.OnAnyPartBlownAt; break;
                        case "someone_in_vicinity": listener.eventType = StateListener.EventType.OnSomeoneInVicinity; break;
                        case "someone_new_in_vicinity": listener.eventType = StateListener.EventType.OnSomeoneNewInVicinity; break;
                        case "hears":          listener.eventType = StateListener.EventType.OnHears; break;
                        case "hears_anywhere": listener.eventType = StateListener.EventType.OnHearsAnywhere; break;
                        case "destroyed":      listener.eventType = StateListener.EventType.OnDestroyed; break;
                        case "controlled":     listener.eventType = StateListener.EventType.OnJoystickControlled; break;
                        case "is":             listener.eventType = StateListener.EventType.OnVariableChange; break;
                        case "destroyed_restores": listener.eventType = StateListener.EventType.OnDestroyedRestored; break;
                        case "enable_setting":  listener.eventType = StateListener.EventType.OnSettingEnabled; break;
                        case "disable_setting": listener.eventType = StateListener.EventType.OnSettingDisabled; break;
                    }
                    
                    if (whenWords.Length >= 2)
                    {
                        listener.whenData = String.Join( " ", whenWords.Skip(1).ToArray() );
                        
                        if ( listener.eventType == StateListener.EventType.OnToldByBody &&
                            listener.whenData.IndexOf("dialog ") >= 0 )
                        {
                            listener.whenData = RemoveSpacesInDialogNames(listener.whenData);
                        }
                    }
                    
                    if (listener.eventType != StateListener.EventType.None)
                    {
                        AddCommand(listener, thenWords, parentThing, thenPart);
                    }
                }
            }
        }

        return listener;
    }
    
    static void SetListenerStateNumber(StateListener listener, string word)
    {
        switch (word)
        {
            case "current":  listener.setStateRelative = RelativeStateTarget.Current; break;
            case "previous": listener.setStateRelative = RelativeStateTarget.Previous; break;
            case "next":     listener.setStateRelative = RelativeStateTarget.Next; break;
            default:
                int number;
                if ( int.TryParse(word, out number) )
                {
                    listener.setState = number - 1;
                }
                break;
        }
    }
    
    static TweenType GetTweenType(string command)
    {
        TweenType tweenType = TweenType.EaseInOut;
        switch (command)
        {
            case "become_untweened":  tweenType = TweenType.Direct; break;
            case "become_unsoftened": tweenType = TweenType.Steady; break;
            case "become_soft_start": tweenType = TweenType.EaseIn; break;
            case "become_soft_end":   tweenType = TweenType.EaseOut; break;
        }
        return tweenType;
    }
    
    static void AddCommand(StateListener listener, string[] words, Thing parentThing, string thenPart)
    {
        string data = "";
        char[] separators = {' '};

        if (words.Length >= 2)
        {
            data = String.Join( " ", words.Skip(1).ToArray() );
        }
        
        switch (words[0])
        {
            case "become": // e.g. "become 2 in 1s" or "become 2" or "become next"
            case "become_untweened": // e.g. "become untweened 2 in 2s"
            case "become_unsoftened":
            case "become_soft_start":
            case "become_soft_end":
                listener.tweenType = GetTweenType(words[0]);
                float theseMinStateSeconds = listener.tweenType == TweenType.Direct ?
                    0f : minStateSeconds;

                const string viaCommand = " via ";
                if ( thenPart.Contains(viaCommand) )
                {
                    string[] splitAtVia = Misc.Split(thenPart, viaCommand);
                    if (splitAtVia.Length == 2)
                    {
                        words = splitAtVia[0].Split( new char[] {' '}, StringSplitOptions.RemoveEmptyEntries );
                        
                        int thisCurveViaState;
                        if ( int.TryParse(splitAtVia[1], out thisCurveViaState) )
                        {
                            if (thisCurveViaState >= 1 && thisCurveViaState <= CreationHelper.maxThingPartStates + 1)
                            {
                                listener.curveViaState = thisCurveViaState - 1;
                            }
                        }
                    }
                }
                
                if (words.Length == 4 && words[2] == "in")
                {
                    SetListenerStateNumber(listener, words[1]);
                    words[3] = words[3].Replace("s", "");
                    float stateSeconds;
                    if ( float.TryParse(words[3], out stateSeconds) )
                    {
                        listener.setStateSeconds = float.Parse(words[3]);
                    }
                }
                else if (words.Length == 2)
                {
                    SetListenerStateNumber(listener, words[1]);
                    listener.setStateSeconds = theseMinStateSeconds;
                }

                listener.setStateSeconds = Misc.ClampMin(listener.setStateSeconds, theseMinStateSeconds);
                break;
                
            case "emit": // e.g. "emit id123 with 100" or "emit id123"
            case "emit_gravity_free":
                thenPart = ReplaceIncludedNamesWithIds(parentThing.includedNameIds, thenPart);
                words = thenPart.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                
                if (words.Length == 4 && words[2] == "with")
                {
                    listener.emitId = words[1];
                    words[3] = words[3].Replace("%", "");
                    float percent;
                    if ( float.TryParse(words[3], out percent) )
                    {
                        listener.emitVelocityPercent = Mathf.Clamp(percent, 0f, 100f);
                    }
                }
                else if (words.Length == 2)
                {
                    listener.emitId = words[1];
                    listener.emitVelocityPercent = 100f;
                }
                listener.setStateSeconds = Misc.ClampMin(listener.setStateSeconds, 0.01f);
                listener.emitIsGravityFree = words[0] == "emit_gravity_free";
                break;
                
            case "propel_forward":
            {
                    float percent;
                    if ( words.Length == 3 && words[1] == "with" && float.TryParse(words[2], out percent) )
                    {
                        listener.propelForwardPercent = Mathf.Clamp(percent, -100f, 100f);
                    }
                    else
                    {
                        listener.propelForwardPercent = 10f;
                    }
                }
                break;

            case "rotate_forward":
            {
                    float percent;
                    if ( words.Length == 3 && words[1] == "with" && float.TryParse(words[2], out percent) )
                    {
                        listener.rotateForwardPercent = Mathf.Clamp(percent, -100f, 100f);
                    }
                    else
                    {
                        listener.rotateForwardPercent = 10f;
                    }
                }
                break;

            case "tell": // e.g. "tell button is pressed"
                if (data != "")
                {
                    if (listener.tells == null) { listener.tells = new List< KeyValuePair<TellType,string> >(); }
                    listener.tells.Add( new KeyValuePair<TellType,string>(TellType.Self, data) );
                }
                break;

            case "tell_nearby": // e.g. "tell_nearby button is pressed"
                if (data != "")
                {
                    if (listener.tells == null) { listener.tells = new List< KeyValuePair<TellType,string> >(); }
                    listener.tells.Add( new KeyValuePair<TellType,string>(TellType.Nearby, data) );
                }
                break;
                
            case "tell_any": // e.g. "tell_any button is pressed"
                if (data != "")
                {
                    if (listener.tells == null) { listener.tells = new List< KeyValuePair<TellType,string> >(); }
                    listener.tells.Add( new KeyValuePair<TellType,string>(TellType.Any, data) );
                }
                break;
                
            case "tell_body": // e.g. "tell_body button is pressed"
                if (data != "")
                {
                    if (listener.tells == null) { listener.tells = new List< KeyValuePair<TellType,string> >(); }
                    listener.tells.Add( new KeyValuePair<TellType,string>(TellType.Body, data) );
                }
                break;
                
            case "tell_first_of_any": // e.g. "tell_first_of_any button is pressed"
                if (data != "")
                {
                    if (listener.tells == null) { listener.tells = new List< KeyValuePair<TellType,string> >(); }
                    listener.tells.Add( new KeyValuePair<TellType,string>(TellType.FirstOfAny, data) );
                }
                break;
                
            case "tell_in_front": // e.g. "tell_in_front button is pressed"
                if (data != "")
                {
                    if (listener.tells == null) { listener.tells = new List< KeyValuePair<TellType,string> >(); }
                    listener.tells.Add( new KeyValuePair<TellType,string>(TellType.InFront, data) );
                }
                break;
                
            case "tell_first_in_front": // e.g. "tell_first_in_front button is pressed"
                if (data != "")
                {
                    if (listener.tells == null) { listener.tells = new List< KeyValuePair<TellType,string> >(); }
                    listener.tells.Add( new KeyValuePair<TellType,string>(TellType.FirstInFront, data) );
                }
                break;
                
            case "tell_web": // e.g. "tell_web button is pressed"
                if (data != "")
                {
                    if (listener.tells == null) { listener.tells = new List< KeyValuePair<TellType,string> >(); }
                    if (parentThing.version >= 6)
                    {
                        listener.tells.Add( new KeyValuePair<TellType,string>(TellType.Web, data) );
                    }
                    else
                    {
                        listener.tells.Add( new KeyValuePair<TellType,string>(TellType.Self, "web" + data) );
                        listener.tells.Add( new KeyValuePair<TellType,string>(TellType.Self, "web " + data) );
                    }
                }
                break;
                
            case "tell_any_web": // e.g. "tell_any_web button is pressed"
                if (data != "")
                {
                    if (listener.tells == null) { listener.tells = new List< KeyValuePair<TellType,string> >(); }
                    if (parentThing.version >= 6)
                    {
                        listener.tells.Add( new KeyValuePair<TellType,string>(TellType.AnyWeb, data) );
                    }
                    else
                    {
                        listener.tells.Add( new KeyValuePair<TellType,string>(TellType.Any, "web" + data) );
                        listener.tells.Add( new KeyValuePair<TellType,string>(TellType.Any, "web " + data) );
                    }
                }
                break;
                
            case "play": // e.g. "play munching" or "play munching with 150"
                if (data != "")
                {
                    if (listener.sounds == null) { listener.sounds = new List<Sound>(); }
                    listener.sounds.Add( GetSound(data) );
                }
                break;

            case "send_all_to": // e.g. "send nearby to chat cafe" (+ optional "onto", as well as "at [-360 to 360] degrees")
            case "send_nearby_to":
            case "send_one_nearby_to":
                if (data != "")
                {
                    listener.transportMultiplePeople = words[0] != "send_one_nearby_to";
                    listener.transportNearbyOnly = words[0] != "send_all_to";
                    listener.rotationAfterTransport = ExtractDegreesValue(ref data);

                    string[] parts = Misc.Split(data, " onto ");
                    if (parts.Length == 2)
                    {
                        if (parts[0] != "")
                        {
                            listener.transportToArea = parts[0];
                        }
                        if (parts[1] != "")
                        {
                            listener.transportOntoThing = parts[1];
                            string[] ontoViaParts = Misc.Split(listener.transportOntoThing, " via ");
                            if (ontoViaParts.Length == 2)
                            { 
                                listener.transportOntoThing = ontoViaParts[0];
                                if ( !String.IsNullOrEmpty(listener.transportToArea) )
                                {
                                    listener.transportToArea += " via " + ontoViaParts[1];
                                }
                            }
                        }
                    }
                    else
                    {
                        listener.transportToArea = data;
                    }
                    
                    if (listener.transportToArea != null)
                    {
                        string[] viaParts = Misc.Split(listener.transportToArea, " via ");
                        if (viaParts.Length == 2 && viaParts[0] != "" && viaParts[1] != "")
                        {
                            // e.g. "... chat cafe via 5.5s train ride"
                            listener.transportToArea = viaParts[0];
                            string[] secondsAndAreaWords = Misc.Split(viaParts[1]);
                            if (secondsAndAreaWords.Length >= 2)
                            {
                                string secondsString = secondsAndAreaWords[0];
                                secondsString = secondsString.Replace("s", "");

                                float areaSeconds;
                                if ( float.TryParse(secondsString, out areaSeconds) )
                                {
                                    string viaArea = String.Join(" ",
                                        secondsAndAreaWords, 1, secondsAndAreaWords.Length - 1);
                                    if (viaArea != "")
                                    {
                                        listener.transportViaArea = viaArea;
                                        listener.transportViaAreaSeconds = Mathf.Clamp(areaSeconds, 1f, 150f);
                                    }
                                }
                            }
                        }
                    }
                }
                break;

            case "send_all_onto": // e.g. "send nearby onto marker stone 1"
            case "send_nearby_onto":
            case "send_one_nearby_onto":
                if (data != "")
                {
                    listener.rotationAfterTransport = ExtractDegreesValue(ref data);
                    listener.transportOntoThing = data;
                    listener.transportMultiplePeople = words[0] != "send_one_nearby_onto";
                    listener.transportNearbyOnly = words[0] != "send_all_onto";
                }
                break;
                
            case "call_me": // e.g. "call me blade"
                if (data != "")
                {
                    string[] charsWeMayNeedInOtherProcessing = new string[] {
                        ";", ",",
                        PersonManager.syncDataSeparator,
                        PersonManager.syncDataItemSeparator,
                    };
                    foreach (string thisChar in charsWeMayNeedInOtherProcessing)
                    {
                        data = data.Replace(thisChar, " ");
                    }
                    listener.callMeThisName = data;
                }
                break;
                
            case "loop": // e.g. "loop fire" or "loop fire with 120% surround"
                if (data != "")
                {
                    string[] nameAndProperties = Misc.Split(data, " with ");
                    if (nameAndProperties.Length == 2)
                    {
                        listener.startLoopSoundName = nameAndProperties[0];
                        listener.loopVolume = 1f;
                        listener.loopSpatialBlend = 1f;

                        string[] properties = Misc.Split(nameAndProperties[1]);
                        for (int i = 0; i < properties.Length; i++)
                        {
                            string property = properties[i];
                        
                            switch (property)
                            {
                                case "surround":
                                    listener.loopSpatialBlend = 0f;
                                    break;
                                    
                                case "half-surround":
                                    listener.loopSpatialBlend = 0.5f;
                                    break;

                                default:
                                    float loopVolume;
                                    property = property.Replace("%", "");
                                    if ( float.TryParse(property, out loopVolume) )
                                    {
                                        listener.loopVolume = Mathf.Clamp( (float) loopVolume / 100,
                                            minRelativeSoundVolume, maxRelativeSoundVolume );
                                    }
                                    break;
                            }
                        }
                        
                    }
                    else
                    {
                        listener.startLoopSoundName = data;
                        listener.loopVolume = 1f;
                        listener.loopSpatialBlend = 1f;

                    }
                    
                }
                break;
                
            case "end_loop": // e.g. "end loop"
                listener.doEndLoopSound = true;
                break;
                
            case "all_parts_face_someone":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.startTowardsClosestPerson = true;
                break;
                
            case "all_parts_face_someone_else":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.startTowardsSecondClosestPerson = true;
                break;
                
            case "all_parts_face_up":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.startTowardsTop = true;
                break;

            case "all_parts_face_empty_hand":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.startTowardsClosestEmptyHand = true;
                break;
                
            case "all_parts_face_empty_hand_while_held":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.startTowardsClosestEmptyHandWhileHeld = true;
                break;

            case "all_parts_face_nearest":
                if (data != "")
                {
                    AddRotateThingSettingsIfNeeded(listener);
                    listener.rotateThingSettings.startTowardsClosestThingOfName = data;
                }
                break;
                
            case "all_parts_face_view":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.startTowardsMainCamera = true;
                break;
                
            case "stop_all_parts_face_someone":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.stopTowardsPerson = true;
                break;
                
            case "stop_all_parts_face_up":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.stopTowardsTop = true;
                break;
                
            case "stop_all_parts_face_empty_hand":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.stopTowardsClosestEmptyHand = true;
                break;

            case "stop_all_parts_face_nearest":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.stopTowardsClosestThingOfName = true;
                break;

            case "stop_all_parts_face_view":
                AddRotateThingSettingsIfNeeded(listener);
                listener.rotateThingSettings.stopTowardsMainCamera = true;
                break;

            case "destroy_all_parts":
                listener.destroyThingWeArePartOf = GetThingDestruction(words);
                break;
                
            case "destroy_nearby":
                listener.destroyOtherThings = GetOtherThingDestruction(words);
                break;
                
            case "give_haptic_feedback":
                listener.doHapticPulse = true;
                break;

            case "disallow_emitted_climbing":
                InitAreaRightsIfNeeded(listener);
                listener.rights.emittedClimbing = false;
                break;
                
            case "disallow_emitted_transporting":
                InitAreaRightsIfNeeded(listener);
                listener.rights.emittedTransporting = false;
                break;
                
            case "disallow_invisibility":
                InitAreaRightsIfNeeded(listener);
                listener.rights.invisibility = false;
                break;
                
            case "disallow_any_person_size":
                InitAreaRightsIfNeeded(listener);
                listener.rights.anyPersonSize = false;
                break;
                
            case "disallow_highlighting":
                InitAreaRightsIfNeeded(listener);
                listener.rights.highlighting = false;
                break;
                
            case "disallow_amplified_speech":
                InitAreaRightsIfNeeded(listener);
                listener.rights.amplifiedSpeech = false;
                break;

            case "disallow_any_destruction":
                InitAreaRightsIfNeeded(listener);
                listener.rights.anyDestruction = false;
                break;
                
            case "disallow_web_browsing":
                InitAreaRightsIfNeeded(listener);
                listener.rights.webBrowsing = false;
                break;
                
            case "disallow_untargeted_attract_and_repel":
                InitAreaRightsIfNeeded(listener);
                listener.rights.untargetedAttractThings = false;
                break;
                
            case "disallow_build_animations":
                InitAreaRightsIfNeeded(listener);
                listener.rights.slowBuildCreation = false;
                break;

            case "allow_emitted_climbing":
                InitAreaRightsIfNeeded(listener);
                listener.rights.emittedClimbing = true;
                break;
                
            case "allow_emitted_transporting":
                InitAreaRightsIfNeeded(listener);
                listener.rights.emittedTransporting = true;
                break;

            case "allow_invisibility":
                InitAreaRightsIfNeeded(listener);
                listener.rights.invisibility = true;
                break;
                
            case "allow_any_person_size":
                InitAreaRightsIfNeeded(listener);
                listener.rights.anyPersonSize = true;
                break;
                
            case "allow_highlighting":
                InitAreaRightsIfNeeded(listener);
                listener.rights.highlighting = true;
                break;
                
            case "allow_amplified_speech":
                InitAreaRightsIfNeeded(listener);
                listener.rights.amplifiedSpeech = true;
                break;
                
            case "allow_any_destruction":
                InitAreaRightsIfNeeded(listener);
                listener.rights.anyDestruction = true;
                break;
                
            case "allow_web_browsing":
                InitAreaRightsIfNeeded(listener);
                listener.rights.webBrowsing = true;
                break;
                
            case "allow_untargeted_attract_and_repel":
                InitAreaRightsIfNeeded(listener);
                listener.rights.untargetedAttractThings = true;
                break;
                
            case "allow_build_animations":
                InitAreaRightsIfNeeded(listener);
                listener.rights.slowBuildCreation = true;
                break;
                
            case "show_board":
                listener.showDialog = DialogType.Forum;
                listener.showData = data;
                break;
                
            case "show_thread":
                listener.showDialog = DialogType.ForumThread;
                listener.showData = data;
                break;
                
            case "show_areas":
                listener.showDialog = DialogType.FindAreas;
                listener.showData = data;
                break;
                
            case "show_inventory":
                listener.showDialog = DialogType.Inventory;
                break;
            
            case "show_chat_keyboard":
                listener.showDialog = DialogType.Keyboard;
                break;
                
            case "show_video_controls":
                listener.showDialog = DialogType.VideoControl;
                break;
                
            case "show_camera_controls":
                listener.showDialog = DialogType.CameraControl;
                break;
                
            case "show_slideshow_controls":
                listener.showDialog = DialogType.SlideshowControl;
                break;

            case "show_name_tags":
                {
                    float seconds = 30f;
                    if ( !string.IsNullOrEmpty(data) )
                    {
                        data = data.Replace("s", "");
                        int parsedSeconds;
                        if ( int.TryParse(data, out parsedSeconds) )
                        {
                            const int secondsInDay = 24 * 60 * 60;
                            seconds = Mathf.Clamp( parsedSeconds, 0.1f, (float)secondsInDay );
                        }
                    }
                    listener.showNameTagsAgainSeconds = seconds;
                }
                break;
                
            case "do_creation_part":
            case "do_all_creation_parts":
                listener.creationPartChangeIsForAll = words[0] == "do_all_creation_parts";
                if ( thenPart.IndexOf(" local ") >= 0 )
                {
                    thenPart = thenPart.Replace(" local ", " ");
                    listener.creationPartChangeIsLocal = true;
                }
                if ( thenPart.IndexOf(" random ") >= 0 )
                {
                    thenPart = thenPart.Replace(" random ", " ");
                    listener.creationPartChangeIsRandom = true;
                }
                
                string[] thenWords = thenPart.Split(separators, StringSplitOptions.RemoveEmptyEntries);

                if (thenWords.Length >= 2)
                {
                    listener.creationPartChangeMode = thenWords[1];
                    if (thenWords.Length >= 3)
                    {
                        const int arrayOffset = 2;
                        listener.creationPartChangeValues = new float[thenWords.Length - arrayOffset];
                        for (int i = 0; i < listener.creationPartChangeValues.Length; i++)
                        {
                            listener.creationPartChangeValues[i] =
                                GetCreationPartChangeFloat(thenWords[i + arrayOffset]);
                        }
                    }
                    else
                    {
                        listener.creationPartChangeValues = new float[0];
                    }
                }
                break;
                
            case "go_to_inventory_page":
                if (words.Length == 2)
                {
                    int number;
                    if ( int.TryParse(words[1], out number) )
                    {
                        listener.goToInventoryPage = Mathf.Clamp(number, 1, InventoryDialog.maxPages);
                    }
                }
                break;
                
            case "add_crumbles":
                listener.addCrumbles = true;
                break;
                
            case "add_crumbles_for_all_parts":
                listener.addCrumbles = true;
                listener.addEffectIsForAllParts = true;
                break;
                
            case "set_light_intensity":
                if (listener.eventType == StateListener.EventType.OnStarts)
                {
                    float percent;
                    if ( float.TryParse(words[1], out percent) )
                    {
                        listener.setLightIntensity =
                            Mathf.Clamp(percent, 0f, 100f) * 0.01f * Universe.maxLightIntensity;
                    }
                }
                break;

            case "set_light_range":
                if (listener.eventType == StateListener.EventType.OnStarts)
                {
                    float lightRange;
                    if ( float.TryParse(words[1], out lightRange) )
                    {
                        listener.setLightRange = Mathf.Clamp(lightRange, 0f, Universe.normalCameraFarClipPlane);
                    }
                }
                break;

            case "set_light_cone_size":
                if (listener.eventType == StateListener.EventType.OnStarts)
                {
                    float lightConeSize;
                    if ( float.TryParse(words[1], out lightConeSize) )
                    {
                        listener.setLightConeSize = Mathf.Clamp(lightConeSize, 0f, 100f) * 0.01f * Universe.maxLightConeSize;
                    }
                }
                break;
                
            case "set_run_speed":
                if (words.Length == 2)
                {
                    float value;
                    if (words[1] == "default")
                    {
                        InitDesktopModeSettingsIfNeeded(listener);
                        listener.desktopModeSettings.runSpeed = DesktopManager.fastMovementSpeedDefault;
                    }
                    else if ( float.TryParse(words[1], out value) )
                    {
                        InitDesktopModeSettingsIfNeeded(listener);
                        listener.desktopModeSettings.runSpeed = Mathf.Clamp(value, DesktopManager.defaultMovementSpeed, 100f);
                    }
                }
                break;
                
            case "set_jump_speed":
                if (words.Length == 2)
                {
                    float value;
                    if (words[1] == "default")
                    {
                        InitDesktopModeSettingsIfNeeded(listener);
                        listener.desktopModeSettings.jumpSpeed = DesktopManager.jumpSpeedDefault;
                    }
                    else if ( float.TryParse(words[1], out value) )
                    {
                        InitDesktopModeSettingsIfNeeded(listener);
                        listener.desktopModeSettings.jumpSpeed = Mathf.Clamp(value, 0f, 100f);
                    }
                }
                break;
                
            case "set_slidiness":
                if (words.Length == 2)
                {
                    float value;
                    if (words[1] == "default")
                    {
                        InitDesktopModeSettingsIfNeeded(listener);
                        listener.desktopModeSettings.slidiness = 0f;
                    }
                    else if ( float.TryParse(words[1], out value) )
                    {
                        InitDesktopModeSettingsIfNeeded(listener);
                        listener.desktopModeSettings.slidiness = Mathf.Clamp(value, 0f, 100f);
                    }
                }
                break;

            case "set_speed":
                if (words.Length == 2)
                {
                    float n;
                    if ( float.TryParse(words[1], out n) )
                    {
                        listener.velocitySetter = new Vector3(n, n, n);
                    }
                }
                else if (words.Length == 4)
                {
                    float x, y, z;
                    if ( float.TryParse(words[1], out x) )
                    {
                        if ( float.TryParse(words[2], out y) )
                        {
                            if ( float.TryParse(words[3], out z) )
                            {
                                listener.velocitySetter = new Vector3(x, y, z);
                            }
                        }
                    }
                }
                
                if (listener.velocitySetter != null)
                {
                    listener.velocitySetter =
                        Misc.ClampVector3( (Vector3)listener.velocitySetter, -1000f, 1000f );
                }
                break;

            case "add_speed":
                if (words.Length == 2)
                {
                    float n;
                    if ( float.TryParse(words[1], out n) )
                    {
                        listener.forceAdder = new Vector3(n, n, n);
                    }
                }
                else if (words.Length == 4)
                {
                    float x, y, z;
                    if ( float.TryParse(words[1], out x) )
                    {
                        if ( float.TryParse(words[2], out y) )
                        {
                            if ( float.TryParse(words[3], out z) )
                            {
                                listener.forceAdder = new Vector3(x, y, z);
                            }
                        }
                    }
                }
                
                if (listener.forceAdder != null)
                {
                    listener.forceAdder =
                        Misc.ClampVector3( (Vector3)listener.forceAdder, -1000f, 1000f );
                }
                break;
                
            case "multiply_speed":
                if (words.Length == 2)
                {
                    float n;
                    if ( float.TryParse(words[1], out n) )
                    {
                        listener.velocityMultiplier = new Vector3(n, n, n);
                    }
                }
                else if (words.Length == 4)
                {
                    float x, y, z;
                    if ( float.TryParse(words[1], out x) )
                    {
                        if ( float.TryParse(words[2], out y) )
                        {
                            if ( float.TryParse(words[3], out z) )
                            {
                                listener.velocityMultiplier = new Vector3(x, y, z);
                            }
                        }
                    }
                }
                
                if (listener.velocityMultiplier != null)
                {
                    listener.velocityMultiplier =
                        Misc.ClampVector3( (Vector3)listener.velocityMultiplier, 0f, 1000f );
                }
                break;

            case "set_camera_position_to":
                switch (data)
                {
                    case "default":
                        listener.setFollowerCameraPosition = FollowerCameraPosition.InHeadVr;
                        break;
                    case "optimized view":
                        listener.setFollowerCameraPosition = FollowerCameraPosition.InHeadDesktopOptimized;
                        break;
                    case "view from behind me":
                        listener.setFollowerCameraPosition = FollowerCameraPosition.BehindUp;
                        break;
                    case "view from further behind me":
                        listener.setFollowerCameraPosition = FollowerCameraPosition.FurtherBehindUp;
                        break;
                    case "bird's eye":
                        listener.setFollowerCameraPosition = FollowerCameraPosition.BirdsEye;
                        break;
                    case "looking at me":
                        listener.setFollowerCameraPosition = FollowerCameraPosition.LooksAtMe;
                        break;
                    case "left hand":
                        listener.setFollowerCameraPosition = FollowerCameraPosition.AtLeftHand;
                        break;
                    case "right hand":
                        listener.setFollowerCameraPosition = FollowerCameraPosition.AtRightHand;
                        break;
                }
                break;
                
            case "set_camera_following_to":
                switch (data)
                {
                    case "default":       listener.setFollowerCameraLerp = 1f; break;
                    case "smoothly":      listener.setFollowerCameraLerp = 0.025f; break;
                    case "very smoothly": listener.setFollowerCameraLerp = 0.0075f; break;
                    case "none":          listener.setFollowerCameraLerp = 0f; break;
                }
                break;

            case "type":
                data = data.Replace("\"", "");
                data = data.Replace("_", " ");
                if (data != "")
                {
                    listener.doTypeText = data;
                }
                break;

            case "change_head_to":
            case "change_heads_to":
                if (data != "")
                {
                    data = ReplaceIncludedNamesWithIdsInData(parentThing.includedNameIds, data).Trim();
                    if ( !String.IsNullOrEmpty(data) )
                    {
                        listener.attachThingIdAsHead = data;
                        listener.attachToMultiplePeople = words[0] == "change_one_head_to";
                    }
                }
                break;
                
            case "attach_head":
                if (data != "")
                {
                    data = ReplaceIncludedNamesWithIdsInData(parentThing.includedNameIds, data).Trim();
                    if ( !String.IsNullOrEmpty(data) )
                    {
                        listener.attachThingIdAsHead = data;
                    }
                }
                break;
                
            case "resize_nearby_to":
                {
                    data = data.Replace("%", "");
                    float percent;
                    if ( float.TryParse(data, out percent) )
                    {
                        if (percent >= Universe.minScalePercentAsEditor && percent <= Universe.maxScalePercentAsEditor)
                        {
                            percent = Mathf.Clamp(percent, Universe.minScalePercentAsEditor, Universe.maxScalePercentAsEditor);
                            bool resizeSoSubtleItMayConfuse = percent != 100f && Mathf.Abs(100f - percent) < 10f;
                            if (!resizeSoSubtleItMayConfuse)
                            {
                                listener.resizeNearby = percent * 0.01f;
                            }
                        }
                    }
                }
                break;
                
            case "let_go":
                listener.letGo = true;
                break;

            case "stream_to":
                if ( !string.IsNullOrEmpty(data) )
                {
                    listener.streamMyCameraView = true;
                    listener.streamTargetName = data;
                }
                break;
                
            case "stream_stop":
                listener.streamMyCameraView = false;
                break;
                
            case "say":
                listener.say = data.Replace("\"", " ").Trim();
                break;
            
            case "set_voice":
                listener.setVoiceProperties = GetVoicePropertiesFromData(data);
                break;
                
            case "set_snap_angles_to":
                if (data != "")
                {
                    if (data == "default")
                    {
                        listener.setCustomSnapAngles = 0f;
                    }
                    else
                    {
                        float angle;
                        if ( float.TryParse(data, out angle) )
                        {
                            listener.setCustomSnapAngles = Mathf.Clamp(angle, 0f, 360f);
                        }
                    }
                }
                break;
                
            case "play_track":
                if (data != "")
                {
                    listener.soundTrackData = data;
                }
                break;
                
            case "set_gravity_to":
                if (data != "")
                {
                    if (data == "default")
                    {
                        listener.setGravity = new Vector3(0f, Universe.defaultGravity, 0f);
                    }
                    else
                    {
                        Vector3? gravity = Misc.GetSpaceSeparatedStringToVector3(data, Universe.maxGravity);
                        if (gravity != null)
                        {
                            listener.setGravity = gravity;
                        }
                    }
                }
                break;

            case "is":
                if (words.Length >= 2)
                {
                    string remainingWords = String.Join(" ", words, 1, words.Length - 1);
                    if (remainingWords != "")
                    {
                        if (listener.variableOperations == null)
                        {
                            listener.variableOperations = new List<string>();
                        }
                        listener.variableOperations.Add(remainingWords);
                    }
                }
                break;
                
            case "reset_area":
                AddResetSettingsIfNeeded(listener);
                listener.resetSettings.area = true;
                break;
                
            case "reset_persons":
                AddResetSettingsIfNeeded(listener);
                listener.resetSettings.allPersonVariablesInArea = true;
                break;
                
            case "reset_position":
                AddResetSettingsIfNeeded(listener);
                listener.resetSettings.position = true;
                break;
                
            case "reset_rotation":
                AddResetSettingsIfNeeded(listener);
                listener.resetSettings.rotation = true;
                break;
                
            case "reset_body":
                AddResetSettingsIfNeeded(listener);
                listener.resetSettings.body = true;
                break;
                
            case "reset_legs_to_default":
                AddResetSettingsIfNeeded(listener);
                listener.resetSettings.legsToUniversalDefault = true;
                break;
                
            case "reset_legs_to_body_default":
                AddResetSettingsIfNeeded(listener);
                listener.resetSettings.legsToBodyDefault = true;
                break;
                
            case "write":
                listener.setText = data.Replace("\"", " ").Trim();
                if ( listener.setText == listener.setText.ToLower() )
                {
                    listener.setText = listener.setText.ToUpper();
                }
                break;

            case "turn":
                if ( words.Length >= 2 && turnCommands.Contains(words[1]) )
                {
                    listener.turn = words[1];
                }
                break;
                
            case "turn_thing":
                if ( words.Length >= 2 && turnCommands.Contains(words[1]) )
                {
                    listener.turnThing = words[1];
                }
                break;
                
            case "turn_sub_thing":
                if (words.Length >= 2)
                {
                    string lastWord = words[words.Length - 1];
                    if ( turnCommands.Contains(lastWord) )
                    {
                        listener.turnSubThing = lastWord;
                        if (words.Length >= 3)
                        {
                            listener.turnSubThingName = String.Join(" ", words, 1, words.Length - 2);
                        }
                    }
                }
                break;
                
            case "trail_start":
                {
                    listener.partTrailSettings = new PartTrailSettings();
                    listener.partTrailSettings.isStart = true;
                    
                    string[] withSplit = Misc.Split(thenPart, " with ");
                    if (withSplit.Length == 2)
                    {
                        string[] parameters = Misc.Split(withSplit[1]);
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            string param = parameters[i];
                            switch (param)
                            {
                                case "thick-start":
                                    listener.partTrailSettings.thickStart = true;
                                    break;
                                    
                                case "thick-end":
                                    listener.partTrailSettings.thickEnd = true;
                                    break;

                                default:
                                    string seconds = param.Replace("s", "");
                                    float duration;
                                    if ( float.TryParse(seconds, out duration) )
                                    {
                                        listener.partTrailSettings.durationSeconds =
                                            Mathf.Clamp(duration, 0.01f, maxTrailDurationSeconds);
                                    }
                                    break;
                            }
                            
                        }
                    }
                }
                break;
                
            case "trail_end":
                listener.partTrailSettings = new PartTrailSettings();
                listener.partTrailSettings.isStart = false;
                break;

            case "project":
                {
                    listener.projectPartSettings = new ProjectPartSettings();
                    
                    string[] withSplit = Misc.Split(thenPart, " with ");
                    if (withSplit.Length == 2)
                    {

                        string[] parameters = Misc.Split(withSplit[1]);
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            string param = parameters[i];
                            string previousParam = i >= 1 ? parameters[i - 1] : null;
                            
                            switch (param)
                            {
                                case "reach":
                                    string percentString = previousParam.Replace("%", "");
                                    float percentage;
                                    if ( float.TryParse(percentString, out percentage) )
                                    {
                                        percentage = Mathf.Clamp(percentage, 0f, 1000f);
                                        listener.projectPartSettings.relativeReach =
                                                percentage * 0.01f;
                                    }
                                    break;

                                case "default":
                                    {
                                        string distanceString = previousParam.Replace("%", "");
                                        float distance;
                                        if ( float.TryParse(distanceString, out distance) )
                                        {
                                            listener.projectPartSettings.defaultDistance =
                                                Mathf.Clamp(distance, 0f, 1000f);
                                        }
                                    }
                                    break;

                                case "max":
                                    {
                                        string maxString = previousParam.Replace("%", "");
                                        float distance;
                                        if ( float.TryParse(maxString, out distance) )
                                        {
                                            listener.projectPartSettings.maxDistance =
                                                Mathf.Clamp(distance, 0.01f, 10000f);
                                        }
                                    }
                                    break;
                                    
                                case "alignment":
                                    listener.projectPartSettings.align = ProjectPartAlignment.TowardsSurface;
                                    break;
                                    
                                case "counter-alignment":
                                    listener.projectPartSettings.align = ProjectPartAlignment.AwayFromSurface;
                                    break;
                            }
                            
                        }
                    }
                }
                break;
                
            case "set_area_visibility_to":
                if (data == "default")
                {
                    listener.limitAreaVisibilityMeters = -1f;
                }
                else
                {
                    string distanceString = data.Replace("m", "");
                    float distance;
                    if ( float.TryParse(distanceString, out distance) )
                    {
                        listener.limitAreaVisibilityMeters = Mathf.Clamp(distance, 2.5f, 10000f);
                    }
                }
                break;
                
            case "set_person_as_authority":
                listener.makePersonMasterClient = true;
                break;

            case "show_line":
                {
                    const float maxWidth = PartLineSettings.maxWidth;
                    listener.partLineSettings = new PartLineSettings();
                    
                    string[] withSplit = Misc.Split(thenPart, " with ");
                    if (withSplit.Length == 2)
                    {
                        string[] parameters = Misc.Split(withSplit[1]);
                        for (int i = 0; i < parameters.Length; i++) {
                            string param = parameters[i];
                            string previousParam = i >= 1 ? parameters[i - 1] : null;
                            
                            switch (param)
                            {
                                case "width":
                                {
                                        float width;
                                        if ( float.TryParse(previousParam, out width) )
                                        {
                                            listener.partLineSettings.startWidth = Mathf.Clamp(width, 0f, maxWidth);
                                            listener.partLineSettings.endWidth   = Mathf.Clamp(width, 0f, maxWidth);
                                        }
                                    }
                                    break;
                                    
                                case "start-width":
                                    {
                                        float width;
                                        if ( float.TryParse(previousParam, out width) )
                                        {
                                            listener.partLineSettings.startWidth = Mathf.Clamp(width, 0f, maxWidth);
                                        }
                                    }
                                    break;
                                    
                                case "end-width":
                                    {
                                        float width;
                                        if ( float.TryParse(previousParam, out width) )
                                        {
                                            listener.partLineSettings.endWidth = Mathf.Clamp(width, 0f, maxWidth);
                                        }
                                    }
                                    break;
                            }
                            
                        }
                    }
                }
                break;

            case "show_video":
                if (words.Length >= 2)
                {
                    TextLink textLink = new TextLink();
                    string remainingWords = String.Join(" ", words, 1, words.Length - 1);
                    if ( textLink.TryParseText(remainingWords) )
                    {
                        listener.playVideoId = textLink.content;
                        
                        if ( remainingWords.Contains(" with ") )
                        {
                            foreach (string thisWord in words)
                            {
                                string word = thisWord.Replace("%", "");
                                float percent;
                                if ( float.TryParse(word, out percent) )
                                {
                                    listener.playVideoVolume = Mathf.Clamp(percent * 0.01f, 0f, 3f);
                                }
                            }
                        }
                        
                    }
                }
                break;
                
            case "show_web":
                {
                    if (words.Length >= 2)
                    {
                        string url = words[1];
                        if ( url.IndexOf("://") == -1 ) { url = "http://" + url; }

                        if ( url.IndexOf("http://") == 0 || url.IndexOf("https://") == 0 )
                        {
                            BrowserSettings settings = new BrowserSettings();
                            settings.url = url;
                        
                            string[] withSplit = Misc.Split(thenPart, " with ");
                            if (withSplit.Length == 2)
                            {
                                string[] parameters = Misc.Split(withSplit[1]);
                                for (int i = 0; i < parameters.Length; i++)
                                {
                                    string param = parameters[i];
                                    string previousParam = i >= 1 ? parameters[i - 1] : null;
                                    
                                    switch (param)
                                    {
                                        case "zoom":
                                            if (previousParam != null)
                                            {
                                                string zoomString = previousParam.Replace("%", "");
                                                float percent;
                                                if (float.TryParse(zoomString, out percent) )
                                                {
                                                    settings.zoomPercent = Mathf.Clamp(percent, 1f, 1000f);
                                                }
                                            }
                                            break;
                                            
                                        case "navigation-free":
                                            settings.allowUrlNavigation = false;
                                            break;
                                            
                                        case "cursor-free":
                                            settings.allowCursor = false;
                                            break;

                                        case "useJoystick":
                                            settings.useJoystick = true;
                                            break;
                                            
                                        case "unsynced":
                                            settings.syncUrlChangesBetweenPeople = false;
                                            break;
                                    }
                                    
                                }
                            }
                            
                            listener.browserSettings = settings;

                        }
                    }
                }
                break;
                
                case "set_constant_rotation_to":
                    if (words.Length == 4)
                    {
                        const float max = 10000f;
                        listener.constantRotation = new Vector3(
                            StringToFloat(words[1], max),
                            StringToFloat(words[2], max),
                            StringToFloat(words[3], max)
                        );
                        if (parentThing.version <= 6)
                        {
                            listener.constantRotation *= 10f;
                        }
                    }
                    break;
                
                case "set_quest_achieve":
                case "set_quest_unachieve":
                case "set_quest_remove":
                    if ( !string.IsNullOrEmpty(data) )
                    {
                        QuestAction questAction = new QuestAction();
                        questAction.questName = data.Trim().ToLower();
                        
                        switch (words[0])
                        {
                            case "set_quest_achieve":   questAction.actionType = QuestActionType.Achieve; break;
                            case "set_quest_unachieve": questAction.actionType = QuestActionType.Unachieve; break;
                            case "set_quest_remove":    questAction.actionType = QuestActionType.Remove; break;
                        }
                        
                        listener.questAction = questAction;
                    }
                    break;
                    
                case "enable_setting":
                case "disable_setting":
                    if ( !string.IsNullOrEmpty(data) )
                    {
                        string settingString = Misc.ToTitleCase(data).Replace(" ", "");
                        try
                        {
                            Setting setting = (Setting) Enum.Parse( typeof(Setting), settingString, true );
                            if (listener.settings == null)
                            {
                                listener.settings = new Dictionary<Setting,bool>();
                                listener.settings[setting] = words[0] == "enable_setting";
                            }
                        }
                        catch (Exception exception)
                        {
                        }
                    }
                    break;

                case "set_attract":
                case "set_repel":
                    ParseAttractRepel(listener, data, words);
                    break;
        }
    }

    static void AddRotateThingSettingsIfNeeded(StateListener listener)
    {
        if (listener.rotateThingSettings == null)
        {
            listener.rotateThingSettings = new RotateThingSettings();
        }
    }
    
    static void AddResetSettingsIfNeeded(StateListener listener) {
        if (listener.resetSettings == null)
        {
            listener.resetSettings = new ResetSettings();
        }
    }

    static void ParseAttractRepel(StateListener listener, string data, string[] words)
    {
        if ( !string.IsNullOrEmpty(data) )
        {
            AttractThingsSettings settings = new AttractThingsSettings();
                        
            string[] parts = Misc.Split(data);
                                
            foreach (string part in parts)
            {
                float strength;
                if ( float.TryParse(part, out strength) )
                {
                    if (words[0] == "set_repel")
                    {
                        strength *= -1f;
                    }
                    strength = Mathf.Clamp(
                        strength,
                       -AttractThingsSettings.maxStrength,
                        AttractThingsSettings.maxStrength
                    );
                    settings.strength = strength;

                }
                else if (part == "forward-only")
                {
                    settings.forwardOnly = true;
                
                }
                else {
                    settings.thingNameFilter = part == "*" ? null : part;

                }
            }

            listener.attractThingsSettings = settings;
        }
    }

    static float StringToFloat(string s, float max = 0f)
    {
        float value = 0f;
        
        float parsedValue;
        if ( float.TryParse(s, out parsedValue) )
        {
            value = parsedValue;
            if (max != 0f) { value = Mathf.Clamp(value, -max, max); }
        }
        
        return value;
    }

    static ThingDestruction GetThingDestruction(string[] words)
    {
        ThingDestruction destruction = new ThingDestruction();
        
        if (words.Length >= 3 && words[1] == "with")
        {
            for (int i = 2; i < words.Length; i++)
            {
                string word = words[i];
                switch (word)
                {
                    case "burst":
                        destruction.burst = true;
                        break;
                        
                    case "force":
                        {
                            destruction.burst = true;
                            string previousWord = words[i - 1];
                            float force;
                            if ( float.TryParse(previousWord, out force) )
                            {
                                destruction.burstVelocity = Mathf.Clamp(force, 0f, 1000f);
                            }
                        }
                        break;
                        
                    case "parts":
                        {
                            destruction.burst = true;
                            string previousWord = words[i - 1];
                            int parts;
                            if ( int.TryParse(previousWord, out parts) )
                            {
                                destruction.maxParts = Mathf.Clamp(parts, 1, 250);
                            }
                        }
                        break;

                    case "gravity-free":
                        destruction.burst = true;
                        destruction.gravity = false;
                        break;

                    case "bouncy":
                        destruction.burst = true;
                        destruction.bouncy = true;
                        break;
                        
                    case "slidy":
                        destruction.burst = true;
                        destruction.slidy = true;
                        break;
                        
                    case "uncollidable":
                        destruction.burst = true;
                        destruction.collides = false;
                        break;
                        
                    case "self-uncollidable":
                        destruction.burst = true;
                        destruction.collidesWithSiblings = false;
                        break;

                    case "disappear":
                        {
                            string previousWord = words[i - 1];
                            float seconds;
                            if ( float.TryParse(previousWord, out seconds) )
                            {
                                destruction.burst = true;
                                destruction.hidePartsInSeconds = Mathf.Clamp(seconds, 0.1f, 60f);
                            }
                        }
                        break;
                        
                    case "grow":
                    case "shrink":
                        {
                            string previousWord = words[i - 1];
                            float speed;
                            if ( float.TryParse(previousWord, out speed) )
                            {
                                speed = Mathf.Clamp(speed, 0.01f, 100f);
                                if (word == "shrink") { speed *= -1; }
                                destruction.burst = true;
                                destruction.growth = speed;
                            }
                        }
                        break;
                        
                    case "restore":
                        {
                            string previousWord = words[i - 1];
                            previousWord = previousWord.Replace("s", "");
                            float seconds;
                            if ( float.TryParse(previousWord, out seconds) )
                            {
                                const float secondsInDay = 86400f;
                                destruction.restoreInSeconds =
                                    Mathf.Clamp(seconds, 0.01f, secondsInDay);
                            }
                        }
                        break;
                }
            }
        }

        return destruction;
    }
    
    static OtherThingDestruction GetOtherThingDestruction(string[] words)
    {
        OtherThingDestruction destruction = new OtherThingDestruction();
        destruction.thingDestruction = GetThingDestruction(words);

        if (words.Length >= 3 && words[1] == "with")
        {
            for (int i = 2; i < words.Length; i++)
            {
                string word = words[i];
                switch (word)
                {
                    case "radius":
                        {
                            string previousWord = words[i - 1];
                            previousWord = previousWord.Replace("m", "");
                            float radius;
                            if ( float.TryParse(previousWord, out radius) )
                            {
                                destruction.radius = Mathf.Clamp(radius, 0.001f, 10000f);
                            }
                        }
                        break;
                        
                    case "max-size":
                        {
                            string previousWord = words[i - 1];
                            previousWord = previousWord.Replace("m", "");
                            float maxThingSize;
                            if ( float.TryParse(previousWord, out maxThingSize) )
                            {
                                destruction.maxThingSize = Mathf.Clamp(maxThingSize, 0.001f, 10000f);
                            }
                        }
                        break;
                }
            }
        }

        return destruction;
    }

    static Sound GetSound(string data)
    {
        Sound sound = new Sound();

        string[] nameAndProperties = Misc.Split(data, " with ");
        if (nameAndProperties.Length == 2)
        {
            sound.name = nameAndProperties[0];
            string[] properties = Misc.Split(nameAndProperties[1]);
            for (int i = 0; i < properties.Length; i++)
            {
                string property = properties[i];
            
                switch (property) {
                    case "very-low-pitch":
                        sound.pitch = 0.5f;
                        break;
                        
                    case "low-pitch":
                        sound.pitch = 0.75f;
                        break;
                        
                    case "high-pitch":
                        sound.pitch =
                            (SoundLibraryManager.defaultPitch + SoundLibraryManager.maxPitch) * 0.5f;
                        break;
                        
                    case "very-high-pitch":
                        sound.pitch = SoundLibraryManager.maxPitch;
                        break;

                    case "varied-pitch":
                        sound.pitchVariance = 0.1f;
                        break;
                        
                    case "very-varied-pitch":
                        sound.pitchVariance = 0.3f;
                        break;
                        
                    case "echo":
                        sound.echo = true;
                        break;
                        
                    case "low-pass":
                        sound.lowPass = true;
                        break;
                        
                    case "high-pass":
                        sound.highPass = true;
                        break;
                        
                    case "stretch":
                        sound.stretch = true;
                        break;
                        
                    case "reversal":
                        sound.reverse = true;
                        break;
                        
                    case "surround":
                        sound.surround = true;
                        break;
                        
                    default:
                        string nextProperty = i < properties.Length - 1 ? properties[i + 1] : null;
                        switch (nextProperty)
                        {
                            case "repeat":
                            case "repeats":
                                int soundRepeatCount;
                                if ( int.TryParse(property, out soundRepeatCount) )
                                {
                                    sound.repeatCount = Mathf.Clamp(soundRepeatCount, 0, 50);
                                }
                                break;
                                
                            case "octave":
                            case "octaves":
                                float octaveChange;
                                if ( float.TryParse(property, out octaveChange) )
                                {
                                    sound.pitch = Misc.AdjustPitchInOctaves(octaveChange);
                                    sound.pitch = Mathf.Clamp(sound.pitch, 0.00001f, 100f);
                                }
                                break;
                                
                            case "delay":
                                float secondsDelay;
                                property = property.Replace("s", "");
                                if ( float.TryParse(property, out secondsDelay) )
                                {
                                    sound.secondsDelay = Mathf.Clamp(secondsDelay, 0.001f, 30f);
                                }
                                break;

                            case "skip":
                                float secondsToSkip;
                                property = property.Replace("s", "");
                                if ( float.TryParse(property, out secondsToSkip) )
                                {
                                    sound.secondsToSkip = Mathf.Clamp(secondsToSkip, 0.001f, 30f);
                                }
                                break;

                            case "duration":
                                float secondsDuration;
                                property = property.Replace("s", "");
                                if ( float.TryParse(property, out secondsDuration) )
                                {
                                    sound.secondsDuration = Mathf.Clamp(secondsDuration, 0.001f, 60f);
                                }
                                break;

                            case "volume":
                            default:
                                string volumeString = property.Replace("%", "");
                                float relativeVolume;
                                if ( float.TryParse(volumeString, out relativeVolume) )
                                {
                                    relativeVolume = Mathf.Clamp( (float) relativeVolume / 100,
                                        minRelativeSoundVolume, maxRelativeSoundVolume );
                                    sound.volume *= relativeVolume;
                                }
                                break;

                        }
                        break;
                }
            }
            
        }
        else
        {
            sound.name = data;
        }
        
        return sound;
    }

    static VoiceProperties GetVoicePropertiesFromData(string data)
    {
        VoiceProperties voiceProperties = null;
        if (data != "")
        {
            voiceProperties = new VoiceProperties();
            string[] properties = Misc.Split(data, " ");

            for (int i = 0; i < properties.Length; i++) {
                string property = properties[i];
                string nextProperty = i < properties.Length - 1 ? properties[i + 1] : null;

                if (property == "male")
                {
                    voiceProperties.gender = VoiceProperties.Gender.Male;
                }
                else if (property == "female")
                {
                    voiceProperties.gender = VoiceProperties.Gender.Female;
                }
                else {

                    switch (nextProperty)
                    {
                        case "pitch":
                            {
                                float value;
                                if ( float.TryParse(property, out value) )
                                {
                                    voiceProperties.pitch = (int) Mathf.Clamp(value, -10f, 10f);
                                }
                            }
                            break;
                            
                        case "speed":
                            {
                                float value;
                                if ( float.TryParse(property, out value) )
                                {
                                    voiceProperties.speed = (int) Mathf.Clamp(value, -10f, 10f);
                                }
                            }
                            break;
                            
                        default:
                            {
                                string volumeString = property.Replace("%", "");
                                float value;
                                if ( float.TryParse(volumeString, out value) )
                                {
                                    voiceProperties.volume = (int) Mathf.Clamp(value, 0, 200);
                                }
                            }
                            break;
                    }

                 }
            }
        }
        return voiceProperties;
    }
                    
    static void InitAreaRightsIfNeeded(StateListener listener)
    {
        if (listener.rights == null)
        {
            listener.rights = new AreaRights();
            listener.rights.SetAllToNull();
        }
    }
    
    static void InitDesktopModeSettingsIfNeeded(StateListener listener)
    {
        if (listener.desktopModeSettings == null)
        {
            listener.desktopModeSettings = new DesktopModeSettings();
        }
    }
    
    static float ExtractDegreesValue(ref string stringValue)
    {
        float degreesFound = 0f;
        for (int degrees = -360; degrees <= 360; degrees += 45)
        {
            string degreesString = " at " + degrees.ToString() + " degrees";
            if ( stringValue.IndexOf(degreesString) >= 0 )
            {
                stringValue = stringValue.Replace(degreesString, " ");
                stringValue = stringValue.Replace("  ", " ");
                stringValue = stringValue.Trim();
                degreesFound = (float)degrees;
                if (degreesFound == 360f) { degreesFound = 0f; }
                break;
            }
        }
        return degreesFound;
    }
    
    static float GetCreationPartChangeFloat(string stringValue)
    {
        float f = 0f;
        float newF;
        if ( float.TryParse(stringValue, out newF) )
        {
            const float max = 1000;
            if (newF >= -max && newF <= max)
            {
                f = newF;
            }
        }
        return f;
    }
    
    static string ReplaceIncludedNamesWithIds(Dictionary<string,string> includedNameIds, string text)
    {
        char[] separators = {' '};

        foreach ( KeyValuePair<string,string> nameId in includedNameIds.OrderBy(i => -i.Key.Length) )
        {
            string newText = text.Replace(nameId.Key, nameId.Value);
            if (newText != text)
            {
                string[] words = newText.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                if ( (words.Length == 4 && words[2] == "with") || words.Length == 2 )
                {
                    text = newText;
                    break;
                }
            }
        }
        
        return text;
    }
    
    static string ReplaceIncludedNamesWithIdsInData(Dictionary<string,string> includedNameIds, string data) {
        foreach ( KeyValuePair<string,string> nameId in includedNameIds.OrderBy(i => -i.Key.Length) )
        {
            data = data.Replace(nameId.Key, nameId.Value);
        }
        return data;
    }
    
    public static string NormalizeLine(string s, Thing parentThing, bool forUseInAutoCompletion = false)
    {
        if ( !s.Contains("[youtube:") && !s.Contains("show web") )
        {
            s = s.ToLower();
        }
        s = s.Trim();

        s = s.Replace(", then ", ", ");
        s = s.Replace(",then ", ",");
        
        s = s.Replace("turn all parts", "all parts turn");

        s = s.Replace("when any part touched ", "when any part touches ");
        s = s.Replace("when any state touched ", "when any state touches ");
        
        s = s.Replace(" send nearby one ", " send one nearby ");
        
        s = s.Replace(" set visibility to ", " set area visibility to ");
        
        s = s.Replace("dialog me opened", "dialog own profile opened");
        s = s.Replace("dialog me closed", "dialog own profile closed");
        s = s.Replace("body dialog board", "body dialog forum");

        s = s.Replace("type \"when touched", "type \"when _touched");
        s = s.Replace("when touched ", "when touches ");
        s = s.Replace("type \"when _touched", "type \"when touched");
        
        s = s.Replace("when hit ", "when hitting ");
        s = s.Replace("when any part hit ", "when any part hitting ");
        s = s.Replace("send nearby to area ", "send nearby to ");

        s = GetAllWithUnderlines(s, forUseInAutoCompletion, parentThing.version);
        
        if ( s.Contains(" then say ") )
        {
            s = s.Replace(",", commaKey);
        }
        
        return s;
    }
    
    static string GetAllWithUnderlines(string s, bool forUseInAutoCompletion, int thingVersion)
    {
        if (forUseInAutoCompletion)
        {
            s = GetWithUnderlines(s, "do creation part material");
            s = GetWithUnderlines(s, "do all creation parts material");
            s = GetWithUnderlines(s, "when is");
        }

        for (int i = 0; i < BehaviorScriptParser.stringsToGetWithUnderlines.Length; i++)
        {
            s = GetWithUnderlines(s, stringsToGetWithUnderlines[i]);
        }
        
        if (thingVersion >= 8)
        {
            s = GetWithUnderlines(s, "tell in front");
            s = GetWithUnderlines(s, "tell first in front");
        }

        if (forUseInAutoCompletion)
        {
            s = GetWithUnderlines(s, "stop all parts");
            s = GetWithUnderlines(s, "all parts");
            s = GetWithUnderlines(s, "any part");
            s = GetWithUnderlines(s, "set light");
            s = GetWithUnderlines(s, "change head");
        }
        
        return s;
    }
    
    static string EscapeCommaIfInQuotes(string s)
    {
        System.Text.StringBuilder stringBuilder = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (char c in s)
        {
            if (c == '"') { inQuotes = !inQuotes; }
            if (inQuotes && c == ',')
            {
                stringBuilder.Append(commaKey);
            }
            else
            {
                stringBuilder.Append(c);
            }
        }
        string output = stringBuilder.ToString();
        return output;
    }
    
    public static string GetWithUnderlines(string s, string part)
    {
        string partUnderlined = part.Replace(" ", "_");
        partUnderlined = partUnderlined.Replace("-", "_");
        partUnderlined = partUnderlined.Replace("'", "");
        return s.Replace(part, partUnderlined);
    }
    
    static string RemoveSpacesInDialogNames(string s)
    {
        foreach ( DialogType dialogType in Enum.GetValues( typeof(DialogType) ) )
        {
            string name = dialogType.ToString().ToLower();
            string nameWithSpaces = Misc.CamelCaseToSpaceSeparated( dialogType.ToString() );
            if (name != nameWithSpaces)
            {
                s = s.Replace(nameWithSpaces, name);
            }
        }
        return s;
    }

    public static bool IsPlaceholderContent(string s)
    {
        s = s.ToLower();
        
        bool startsWithPlaceholder = false;
        foreach (string placeholder in BehaviorScriptParser.textPlaceholdersStartsWith)
        {
            if ( s.StartsWith(placeholder) )
            {
                startsWithPlaceholder = true;
                break;
            }
        }
        
        return startsWithPlaceholder || BehaviorScriptParser.textPlaceholdersFull.Contains(s);
    }
    
}
