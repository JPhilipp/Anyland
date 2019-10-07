using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using ZenFulcrum.EmbeddedBrowser;

public class BehaviorScriptManager : MonoBehaviour, IGameManager
{
    // Manages people-made Thing Part scripts, called
    // Behavior Scripts.

	public ManagerStatus status 	 { get; private set; }
	public string 		 failMessage { get; private set; }
    
    public string[] comparators = {};
    public string[] disallowedVariableNames = {};
    public string[] functionNames = {};
    public string[] functionNamesWithTwoParams = {};
    
    public const int maxVariableCalculationsPerFrame = 50;
    int variableCalculationsThisFrame = 0;
    int variableCalculationsLimitHit = 0;
    float timeToClearVariableCalculationsLimitHit = -1f;
    
    public const string validVariableNameChars = Validator.lowerCaseLettersAndNumbers + "_.";

    public string[] placeholdersReturningStrings = {};

    static bool didExpressionTest = false;

    WindowsVoice voice = null;
    
    int tellCountForThisUpdate = 0;
    const int maxTellCountPerUpdateToAvoidInfiniteLoops = 250;
    
    public void Startup()
    {
		status = ManagerStatus.Initializing;
		
		comparators = new string[]
		{
            "<=", ">=", "==", "<>", "!=",
            "=<", "=>", "><",
            "<", ">", "="
        };
        disallowedVariableNames = new string[] { "false", "true", "is", "when", "then", "and", "or", "if", "not" };

        ResetTimeToClearVariableCalculationsLimitHit();
        
        placeholdersReturningStrings = new string[]
        {
            "area name", "thing name", "closest held", "people names", "typed", "area values", "thing values",
            "person values", "person"
        };
        
		status = ManagerStatus.Started;
	}
	
	void Update()
	{
        HandleVariableCalculationsCapping();
        tellCountForThisUpdate = 0;
    }
    
    public bool IterateTellCountIfStillUnderLimit()
    {
        bool isUnderLimit = tellCountForThisUpdate < maxTellCountPerUpdateToAvoidInfiniteLoops;
        if (isUnderLimit)
        {
            tellCountForThisUpdate++;
        }
        return isUnderLimit;
    }
    
    void HandleVariableCalculationsCapping()
    {
        variableCalculationsThisFrame = 0;
        
        if (Time.time >= timeToClearVariableCalculationsLimitHit)
        {
            if (variableCalculationsLimitHit > 0)
            {
                Managers.soundManager.Play("no", forcePlay: true);
                variableCalculationsLimitHit = 0;
            }
        }
	}
	
	void ResetTimeToClearVariableCalculationsLimitHit()
	{
        timeToClearVariableCalculationsLimitHit = Time.time + 2.5f;
	}
	
	public bool RegisterNewVariableCalculationAttempted()
	{
        bool isAllowed = false;
        
        if (variableCalculationsThisFrame < maxVariableCalculationsPerFrame && variableCalculationsLimitHit <= 2)
        {
            variableCalculationsThisFrame++;
            if (variableCalculationsThisFrame == maxVariableCalculationsPerFrame)
            {
                variableCalculationsLimitHit++;
            }
            isAllowed = true;
        }

        return isAllowed;
	}
	
    public void TriggerEventsRelatedToPosition()
    {
        Component[] components = Managers.thingManager.placements.GetComponentsInChildren( typeof(Thing), true );

        const float distanceConsideredNear = 3f;
        const float distanceConsideredWalkedInto = 2f;
        const float distanceConsideredInVicinity = 7.5f;
        
        Vector3 ourPosition = Managers.personManager.ourPerson.Torso.transform.position;
        
        foreach (Thing thing in components)
        {
            float distance = Vector3.Distance(thing.transform.position, ourPosition);
            
            if (distance <= distanceConsideredInVicinity)
            {
                if (distance <= distanceConsideredNear)
                {
                    if (distance <= distanceConsideredWalkedInto)
                    {
                        thing.TriggerEventAsStateAuthority(StateListener.EventType.OnWalkedInto);
                    }
                    thing.TriggerEventAsStateAuthority(StateListener.EventType.OnNeared);
                }
                if (!thing.triggeredOnSomeoneNewInVicinity)
                {
                    thing.triggeredOnSomeoneNewInVicinity = true;
                    thing.TriggerEventAsStateAuthority(StateListener.EventType.OnSomeoneNewInVicinity);
                }
                thing.TriggerEventAsStateAuthority(StateListener.EventType.OnSomeoneInVicinity);
            }

        }
    }
    
    public void TriggerTellNearbyEvent(string data, Vector3 originPosition)
    {
        Component[] things = Managers.thingManager.GetAllThings();
        foreach (Thing thing in things)
        {
            if ( Vector3.Distance(thing.transform.position, originPosition) <= 7.5f )
            {
                thing.TriggerEvent(StateListener.EventType.OnToldByNearby, data);
            }
        }
    }
   
    public void TriggerTellFirstOfAnyEvent(string data, Vector3 originPosition, GameObject ownThingObject)
    {
        Component[] things = Managers.thingManager.GetAllThings();
        things = things.OrderBy(
            x => Vector3.Distance(originPosition, x.transform.position)
            ).ToArray();

        foreach (Thing thing in things)
        {
            if (thing.gameObject != ownThingObject)
            {
                bool didTriggerSomething = thing.TriggerEvent(StateListener.EventType.OnToldByAny, data);
                if (didTriggerSomething) { break; }
            }
        }
    }
    
    public void TriggerTellInFront(string data, ThingPart thingPart, bool firstAndUnblockedPathOnly = false)
    {
        Ray ray = new Ray(thingPart.transform.position, thingPart.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray).OrderBy(h => h.distance).ToArray();
        foreach (RaycastHit hit in hits)
        {
            ThingPart otherThingPart = hit.transform.GetComponent<ThingPart>();
            if (otherThingPart != null && otherThingPart != thingPart &&
                otherThingPart.transform.parent != null)
            {
                Thing thing = otherThingPart.transform.parent.GetComponent<Thing>();
                if (thing && !thing.isPassable)
                {
                    otherThingPart.TriggerEvent(StateListener.EventType.OnToldByAny, data);
                    
                    if (firstAndUnblockedPathOnly)
                    {
                        break;
                    }
                }
            }
        }
    }
    
    public void TriggerTellBodyEventToAttachments(Person person, string data, bool weAreStateAuthority = false)
    {
        foreach (KeyValuePair<AttachmentPointId, GameObject> entry in person.AttachmentPointsById) {
            if (entry.Value != null)
            {
                Thing[] things = entry.Value.GetComponentsInChildren<Thing>();
                if (things.Length >= 1)
                {
                    things[0].TriggerEvent(StateListener.EventType.OnToldByBody, data, weAreStateAuthority: weAreStateAuthority);
                }
            }
        }

        TriggerTellBodyEventToHandHoldable(person, data, TopographyId.Left, weAreStateAuthority: weAreStateAuthority);
        TriggerTellBodyEventToHandHoldable(person, data, TopographyId.Right, weAreStateAuthority: weAreStateAuthority);
    }
    
    void TriggerTellBodyEventToHandHoldable(Person person, string data, TopographyId topographyId, bool weAreStateAuthority = false)
    {
        GameObject hand = person.GetHandByTopographyId(topographyId);
        GameObject holdable = person.GetThingInHand(hand, restrictToFindingHoldables: true);
        if (holdable != null)
        {
            Thing thing = holdable.GetComponent<Thing>();
            if (thing != null)
            {
                thing.TriggerEvent(StateListener.EventType.OnToldByBody, data, weAreStateAuthority: weAreStateAuthority);
            }
        }
    }
    
    public void TriggerTellAnyEvent(string data, bool weAreStateAuthority = false)
    {
        Component[] things = Managers.thingManager.GetAllThings();
        foreach (Thing thing in things)
        {
            thing.TriggerEvent(StateListener.EventType.OnToldByAny, data, weAreStateAuthority);
        }
    }

    public void TriggerTellAnyWebEvent(string data)
    {
        bool isMasterClient = Our.IsMasterClient(errOnSideOfTrueIfStillJoining: true);
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject rootObject in rootObjects)
        {
            Component[] browsers = rootObject.GetComponentsInChildren( typeof(Browser), true );
            foreach (Browser browser in browsers)
            {
                if (browser.gameObject.name != "GuideBrowser")
                {
                    browser.CallFunction("AnylandToldByAny", data, isMasterClient).Done();
                }
            }
        }
    }
    
    public void TriggerVariableChangeToThings(string data = null, Person relevantPerson = null)
    {
        Component[] components = Managers.thingManager.GetAllThings();
        foreach (Thing thing in components)
        {
            if (thing.containsBehaviorScriptVariables)
            {
                thing.TriggerEvent(StateListener.EventType.OnVariableChange, data, relevantPerson: relevantPerson);
            }
        }
    }
    
    GameObject[] GetAllPlacementThingsAsArray()
    {
        Component[] thingComponents = Managers.thingManager.placements.GetComponentsInChildren( typeof(Thing), true );
        GameObject[] thingObjects = new GameObject[thingComponents.Length];
        int i = 0;
        foreach (Component thingComponent in thingComponents)
        {
            thingObjects[i++] = thingComponent.gameObject;
        }
        return thingObjects;
    }
    
    public void TriggerOnHears(string speech)
    {
        const float maxDistance = 7.5f;
        Vector3 ourPosition = Managers.personManager.ourPerson.Head.transform.position;
        
        GameObject thrownParent = Managers.treeManager.GetObject("/Universe/ThrownOrEmittedThings");

        Component[] placements = Managers.thingManager.placements.GetComponentsInChildren( typeof(Thing), true );
        Component[] throwns = thrownParent.GetComponentsInChildren( typeof(Thing), true );
        Component[] placementsAndThrowns = placements.Concat(throwns).ToArray();
        
        foreach (Thing thing in placementsAndThrowns)
        {
            if ( Vector3.Distance(thing.transform.position, ourPosition) <= maxDistance )
            {
                thing.TriggerEvent(StateListener.EventType.OnHears, speech, weAreStateAuthority: true);
            }
            thing.TriggerEvent(StateListener.EventType.OnHearsAnywhere, speech, weAreStateAuthority: true);
        }
        
        Component[] attachments = Managers.personManager.ourPerson.GetComponentsInChildren( typeof(Thing), true );
        foreach (Thing thing in attachments)
        {
            thing.TriggerEvent(StateListener.EventType.OnHears, speech, weAreStateAuthority: true);
            thing.TriggerEvent(StateListener.EventType.OnHearsAnywhere, speech, weAreStateAuthority: true);
        }
    }
    
    public void Speak(ThingPart thingPart, string text, VoiceProperties properties)
    {
        if (voice == null)
        {
            try
            {
                voice = gameObject.AddComponent<WindowsVoice>() as WindowsVoice;
                voice.Init();
            }
            catch (Exception exception)
            {
                voice = null;
                Log.Debug("Failed to initialize Windows Voice");
            }
        }
        
        if (voice != null)
        {
            bool hasSurroundSound = false;
            Thing thing = thingPart.transform.parent.gameObject.GetComponent<Thing>();
            if (thing != null) { hasSurroundSound = thing.hasSurroundSound; }

            Vector3 ourPosition = Managers.personManager.ourPerson.Head.transform.position;
            float distance = Vector3.Distance(ourPosition, thingPart.transform.position);

            const float maxDistanceToHear = 12.5f;
            const float startOfHearingFallOff = 2f;
            float relativeVolume = 1f;
            if (distance > startOfHearingFallOff && !hasSurroundSound)
            {
                relativeVolume = 1f - (distance          - startOfHearingFallOff) /
                                      (maxDistanceToHear - startOfHearingFallOff);
            }
            
            const float generalVolumeAdjust = 0.5f;
            relativeVolume *= generalVolumeAdjust;
            
            if (properties == null) { properties = new VoiceProperties(); }
            
            VoiceProperties adjustedProperties = new VoiceProperties();
            adjustedProperties.gender = properties.gender;
            adjustedProperties.volume = (int)( (float)properties.volume * relativeVolume );
            adjustedProperties.pitch = properties.pitch;
            adjustedProperties.speed = properties.speed;
            
            if (adjustedProperties.volume > 0)
            {
                voice.Speak(text, adjustedProperties);
            }
        }
    }
    
    public void StopSpeech()
    {
        if (voice != null)
        {
            Destroy(voice);
            voice = null;
        }
    }
    
    public string ReplaceTextPlaceholders(string s, Thing thing, ThingPart thingPart)
    {
        if ( Misc.ContainsCaseInsensitive(s, "value") )
        {
            const string variablesSuffix = " values]";
            const string thingVariablesPlaceholder  = "[thing" + variablesSuffix;
            const string areaVariablesPlaceholder   = "[area" + variablesSuffix;
            const string personVariablesPlaceholder = "[person" + variablesSuffix;

            const string variableSuffix       = " value]";
            const string thingVariablePrefix  = "[";
            const string areaVariablePrefix   = "[area.";
            const string personVariablePrefix = "[person.";
            
            Person relevantPerson = null;

            if ( Misc.ContainsCaseInsensitive(s, variablesSuffix) )
            {
                bool weMaySeeAll = thing.IsPlacement() || Managers.areaManager.weAreEditorOfCurrentArea;
                
                if ( Misc.ContainsCaseInsensitive(s, areaVariablesPlaceholder) )
                {
                    string areaVariables = weMaySeeAll ?
                        GetVariablesString(Managers.areaManager.behaviorScriptVariables) : "";
                    s = Misc.ReplaceCaseInsensitive(s, areaVariablesPlaceholder, areaVariables);
                }
                if ( Misc.ContainsCaseInsensitive(s, personVariablesPlaceholder) )
                {
                    relevantPerson = GetPersonVariablesRelevantPerson(thingPart);
                    string personVariables = weMaySeeAll && relevantPerson != null ?
                        GetVariablesString(relevantPerson.behaviorScriptVariables) : "";
                    s = Misc.ReplaceCaseInsensitive(s, personVariablesPlaceholder, personVariables);
                }

                if ( Misc.ContainsCaseInsensitive(s, thingVariablesPlaceholder) )
                {
                    s = Misc.ReplaceCaseInsensitive(s, thingVariablesPlaceholder,
                        GetVariablesString(thing.behaviorScriptVariables) );
                }
                
                if ( Misc.ContainsCaseInsensitive(s, personVariablePrefix) &&
                    Misc.ContainsCaseInsensitive(s, variablesSuffix) )
                {
                    if (weMaySeeAll)
                    {
                        s = ReplacePersonVariableNamesForAll(s);
                    }
                    Regex remainingVariablePlaceholders = new Regex(@"\[([^\]]+) values\]", RegexOptions.IgnoreCase);
                    s = remainingVariablePlaceholders.Replace(s, "");
                }
            }

            if ( Misc.ContainsCaseInsensitive(s, variableSuffix) )
            {
                if ( Misc.ContainsCaseInsensitive(s, areaVariablePrefix) )
                {
                    foreach (KeyValuePair<string,float> variable in Managers.areaManager.behaviorScriptVariables)
                    {
                        string sFind = "[" + variable.Key + variableSuffix;
                        s = Misc.ReplaceCaseInsensitive(s, sFind, variable.Value.ToString() );
                    }
                }
                
                if ( Misc.ContainsCaseInsensitive(s, thingVariablePrefix) )
                {
                    foreach (KeyValuePair<string,float> variable in thing.behaviorScriptVariables)
                    {
                        string sFind = thingVariablePrefix + variable.Key + variableSuffix;
                        s = Misc.ReplaceCaseInsensitive(s, sFind, variable.Value.ToString() );
                    }
                }
                
                if ( Misc.ContainsCaseInsensitive(s, personVariablePrefix) )
                {
                    if (relevantPerson == null)
                    {
                        relevantPerson = GetPersonVariablesRelevantPerson(thingPart);
                    }
                    if (relevantPerson != null)
                    {
                        foreach (KeyValuePair<string,float> variable in relevantPerson.behaviorScriptVariables)
                        {
                            string sFind = "[" + variable.Key + variableSuffix;
                            s = Misc.ReplaceCaseInsensitive(s, sFind, variable.Value.ToString() );
                        }
                    }
                }
                
                if ( Misc.ContainsCaseInsensitive(s, areaVariablePrefix) ||
                    Misc.ContainsCaseInsensitive(s, thingVariablePrefix) )
                {
                    Regex remainingVariablePlaceholders = new Regex(@"\[([^\]]+) value\]", RegexOptions.IgnoreCase);
                    s = remainingVariablePlaceholders.Replace(s, "0");
                }

            }
            
        }

        return s;
    }
    
    string ReplacePersonVariableNamesForAll(string s)
    {
        List<string> variableNames = GetAllPersonVariableNames();
        foreach (string variableName in variableNames)
        {
            string toFind = "[" + variableName + " values]";
            if ( Misc.ContainsCaseInsensitive(s, toFind) )
            {
                string toReplace = GetAllPersonVariableValues(variableName);
                s = Misc.ReplaceCaseInsensitive(s, toFind, toReplace);
            }
        }
        return s;
    }
    
    List<string> GetAllPersonVariableNames()
    {
        List<string> variableNames = new List<string>();
        
        List<Person> persons = Managers.personManager.GetPersons();
        
        foreach (Person person in persons)
        {
            foreach (KeyValuePair<string,float> variable in person.behaviorScriptVariables)
            {
                if ( !variableNames.Contains(variable.Key) )
                {
                    variableNames.Add(variable.Key);
                }
            }
        }
        
        variableNames.Sort();
        
        return variableNames;
    }
    
    string GetAllPersonVariableValues(string variableName)
    {
        string s = "";
        
        List<Person> persons = Managers.personManager.GetPersons(sortByName: true);
        foreach (Person person in persons)
        {
            float value = 0f;
            foreach (KeyValuePair<string,float> variable in person.behaviorScriptVariables)
            {
                if (variable.Key == variableName)
                {
                    value = variable.Value;
                    break;
                }
            }
            
            if (s != "") { s += Environment.NewLine; }
            s += person.screenName.ToUpper() + ": " + value.ToString();
        }
        
        return s;
    }
    
    public Person GetPersonVariablesRelevantPerson(ThingPart thingPart)
    {
        Person person = Managers.personManager.GetPersonThisObjectIsOf(thingPart.gameObject);
        if (person == null)
        {
            GameObject closestHead = Managers.personManager.GetPersonHeadClosestToPosition(thingPart.transform.position);
            if (closestHead != null)
            {
                person = Managers.personManager.GetPersonThisObjectIsOf(closestHead);
            }
        }
        return person;
    }

    string GetVariablesString(Dictionary<string,float> variables)
    {
        string s = "";
        foreach (KeyValuePair<string,float> variable in variables)
        {
            s += variable.Key + ": " + variable.Value.ToString() + Environment.NewLine;
        }
        s = s.ToUpper();
        return s;
    }
    
    public void ResetAllThingBehaviorScriptVariables()
    {
        Component[] things = Managers.thingManager.GetAllThings();
        foreach (Thing thing in things)
        {
            thing.behaviorScriptVariables = new Dictionary<string,float>();
        }
    }
    
    public void ResetArea()
    {
        Managers.temporarilyDestroyedThingsManager.RestoreAll();

        Managers.personManager.ourPerson.lastHandledInformedOthersOfStatesTime = Time.time;
        Managers.areaManager.behaviorScriptVariables = new Dictionary<string,float>();

        Component[] things = Managers.thingManager.GetAllThings();
        foreach (Thing thing in things)
        {
            thing.behaviorScriptVariables = new Dictionary<string,float>();
            thing.ResetStates();
            if ( thing.IsPlacement() && thing.subThingMasterPart == null )
            {
                thing.RestoreOriginalPlacement(ignoreScale: true);
            }
        }
        
        if (Managers.personManager.ourPerson.isMasterClient)
        {
            const float secondsDelayToAvoidRacing = 1f;
            CancelInvoke("InvokableTriggerVariableChangeToThings");
            Invoke("InvokableTriggerVariableChangeToThings", secondsDelayToAvoidRacing);
        }
    }
    
    public void ResetAllPersonVariablesInArea()
    {
        List<Person> persons = Managers.personManager.GetCurrentAreaPersons();
        foreach (Person person in persons)
        {
            person.behaviorScriptVariables = new Dictionary<string,float>();
        }
    }
    
    void InvokableTriggerVariableChangeToThings()
    {
        TriggerVariableChangeToThings();
    }
    
    public string NormalizeVariableName(string s)
    {
        s = s.Trim();
        s = s.ToLower();
        s = s.Replace(";", "");
        s = s.Replace(PersonManager.syncDataSeparator, "");
        return s;
    }
    
    public BehaviorScriptVariableScope GetVariableScope(string s)
    {
        BehaviorScriptVariableScope scope = BehaviorScriptVariableScope.None;
        
        bool isBasicallyValid =
            Validator.ContainsOnly(s, BehaviorScriptManager.validVariableNameChars) &&
            System.Char.IsLetter(s[0]) &&
            System.Array.IndexOf(Managers.behaviorScriptManager.disallowedVariableNames, s) == -1;
        
        if (isBasicallyValid)
        {
            if ( s.Contains(".") )
            {
                string[] parts = Misc.Split(s, ".", options: StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    switch (parts[0])
                    {
                        case "area":   scope = BehaviorScriptVariableScope.Area; break;
                        case "person": scope = BehaviorScriptVariableScope.Person; break;
                    }
                }
            }
            else
            {
                scope = BehaviorScriptVariableScope.Thing;
            }
        }

        return scope;
    }

    public void DestroyThing(Thing rootThing, ThingDestruction destruction)
    {
        if (rootThing != null && rootThing.name != Universe.objectNameIfAlreadyDestroyed)
        {
            bool isPlacement = rootThing.IsPlacement();
            bool isThrownOrEmitted = rootThing.isHeldAsHoldable || rootThing.isThrownOrEmitted;
        
            if (isPlacement || isThrownOrEmitted)
            {
                if (destruction.burst)
                {
                    Effects.BreakIntoPieces(rootThing, destruction);
                    rootThing.name = Universe.objectNameIfAlreadyDestroyed;
                }

                if (isPlacement)
                {
                    Managers.personManager.DoRedundantlyInformAboutThingDestruction(
                        rootThing.placementId, destruction);
                    Managers.temporarilyDestroyedThingsManager.AddPlacement(
                        rootThing, destruction.restoreInSeconds);
                }
                else if (isThrownOrEmitted)
                {
                    Misc.Destroy(rootThing.gameObject);
                    if (rootThing.isHeldAsHoldableByOurPerson)
                    {
                        Managers.personManager.CachePhotonHeldThingsData();
                    }
                }

            }
        }
    }
    
    public void DestroyOtherThingsInRadius(ThingPart originThingPart, OtherThingDestruction otherDestruction)
    {
        Thing rootThing = originThingPart.GetMyRootThing();
        
        Collider[] hitColliders = Physics.OverlapSphere(
            originThingPart.transform.position, otherDestruction.radius);
        foreach (Collider collider in hitColliders)
        {
            ThingPart otherThingPart = collider.gameObject.GetComponent<ThingPart>();
            if (otherThingPart != null)
            {
                Thing otherRootThing = otherThingPart.GetMyRootThing();
                if (otherRootThing != rootThing && otherRootThing.biggestSize <= otherDestruction.maxThingSize)
                {
                    DestroyThing(otherRootThing, otherDestruction.thingDestruction);
                }
            }
        }
    }
    
    public static void TestReplaceVariablesWithValues(Thing thing, ThingPart thingPart)
    {
        #if !UNITY_EDITOR
            return;
        #endif
                    
        if (!didExpressionTest)
        {
            didExpressionTest = true;
            bool incorrectFound = false;
            
            thing.behaviorScriptVariables = new Dictionary<string,float>();
            Managers.areaManager.behaviorScriptVariables = new Dictionary<string,float>();
            
            thing.behaviorScriptVariables.Add("foo", 257.6f);
            Managers.areaManager.behaviorScriptVariables.Add("area.gold", 6f);
            
            Dictionary<string,float> tests = new Dictionary<string,float> {
                { "18",                    18.00f },
                { "foo",                  257.60f },
                { "bar",                    0.00f },
                { "foo / 10",              25.76f },
                { "foo/10",                25.76f },
                { "foo / 10 + foo * 2",   540.96f },
                { "foo/10+foo*2",         540.96f },
                { "floor(6.6)",             6.00f },
                { "floor(foo)",           257.00f },
                { "floor(foo / 10)",       25.00f },
                { " floor  (  foo  / 10  ) ",     25.00f },
                { "ceil( floor(foo / 10) * 2.5)", 63.00f },
                { "area.gold + area.bar",   6.00f },
                { "area.gold + area.gold", 12.00f },
                { "smaller(23 17)",        17.00f },
                { "smaller(foo 17)",       17.00f },
                { "smaller(17 foo)",       17.00f },
                { "smaller(17 foo 18) * [foo (bar smaller **", -1f },
            };

            MathParserTK.MathParser parser = new MathParserTK.MathParser();
            
            foreach (KeyValuePair<string,float> test in tests)
            {
                string expression = test.Key;
                float correctResult = test.Value;
                
                Debug.Log("Before: " + expression);
                expression = ReplaceVariablesWithValues(thing, thingPart, expression, doDebug: true);
                Debug.Log("After:  " + expression);

                float result = -1f;
                try
                {
                    result = (float)parser.Parse(expression);
                }
                catch
                {
                }
                Debug.Log("Result: " + result);

                bool isCorrect = result == correctResult;
                if (!isCorrect) { incorrectFound = true; }
                Debug.Log(isCorrect ?
                    "Correct" :
                    ">>> Incorrect (expected " + correctResult.ToString() + ")"
                    );
                
                Debug.Log("------------------");
            }

            Debug.Log(!incorrectFound ? "- All Correct -" : "- Some incorrect -");
        }
    }
    
    public static string ReplaceVariablesWithValues(Thing thing, ThingPart thingPart, string expression, Person requiredRelevantPerson = null, bool doDebug = false)
    {
        string replacedExpression = "";
        
        expression = AddSpacesToScriptVariableExpression(expression);
        Person relevantPerson = null;

        string[] parts = Misc.Split(expression);
        if (doDebug) { Debug.Log( "parts = " + String.Join("  ", parts) ); }
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            part = part.Trim();
            
            if ( System.Char.IsLetter(part[0]) )
            {
                string nextPart = i + 1 < parts.Length ? parts[i + 1] : null;

                bool isFunction = nextPart == "(";
                if (isFunction)
                {

                }
                else if ( System.Array.IndexOf(Managers.behaviorScriptManager.disallowedVariableNames, part) == -1 )
                {
                    bool foundVariable = false;
                
                    foreach (KeyValuePair<string,float> variable in thing.behaviorScriptVariables)
                    {
                        if (part == variable.Key)
                        {
                            part = variable.Value.ToString();
                            foundVariable = true;
                            break;
                        }
                    }
                    
                    if ( !foundVariable && part.StartsWith(BehaviorScriptParser.areaVariablePrefix) )
                    {
                        foreach (KeyValuePair<string,float> variable in Managers.areaManager.behaviorScriptVariables)
                        {
                            if (part == variable.Key)
                            {
                                part = variable.Value.ToString();
                                foundVariable = true;
                                break;
                            }
                        }
                    }
                    
                    if ( !foundVariable && part.StartsWith(BehaviorScriptParser.personVariablePrefix) )
                    {
                        if (relevantPerson == null)
                        {
                            relevantPerson = Managers.behaviorScriptManager.GetPersonVariablesRelevantPerson(thingPart);
                        }
                        if ( relevantPerson != null &&
                            (requiredRelevantPerson == null || relevantPerson == requiredRelevantPerson) )
                        {
                            foreach (KeyValuePair<string,float> variable in relevantPerson.behaviorScriptVariables)
                            {
                                if (part == variable.Key)
                                {
                                    part = variable.Value.ToString();
                                    foundVariable = true;
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (!foundVariable)
                    {
                        part = "0";
                    }

                }

            }
            
            replacedExpression += part + " ";
        }
        
        replacedExpression = replacedExpression.Replace(" + -", " -");

        replacedExpression = replacedExpression.Replace(" (", "(");
        replacedExpression = replacedExpression.Replace("( ", "(");
        replacedExpression = replacedExpression.Replace(" )", ")");
        replacedExpression = replacedExpression.Replace(") ", ")");
        replacedExpression = replacedExpression.Replace("  ", " ");
        
        replacedExpression = replacedExpression.Trim();

        replacedExpression = ModifyTwoParamFunctionSyntax(replacedExpression, doDebug: doDebug);

        // Log.Debug("Expression before: " + expression);
        // Log.Debug("Expression after: " + replacedExpression);

        return replacedExpression;
    }
    
    static string ModifyTwoParamFunctionSyntax(string expression, bool doDebug = false)
    {
        string[] parts = Misc.Split(expression);
        string replacedExpression = "";

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            part = part.Trim();
            
            bool isFunction = part.Contains("(");
            if (isFunction)
            {
                int nextIndex = i + 1;
                bool mayBeFunctionWithTwoParams = nextIndex < parts.Length && parts[nextIndex].Contains(")");
                if (mayBeFunctionWithTwoParams)
                {
                    string combined = part + " " + parts[i + 1];
                    combined = combined.Replace("(", ";");
                    combined = combined.Replace(")", "");
                    combined = combined.Replace(" ", ";");
                    
                    string[] nameAndParams = Misc.Split(combined, ";");
                    
                    bool isFunctionWithTwoParams = nameAndParams.Length == 3;
                    if (isFunctionWithTwoParams)
                    {
                        part = "(" + nameAndParams[1] + ")" + nameAndParams[0] + "(" + nameAndParams[2] + ")";
                        i++;
                    }
                }
            
            }
            
            replacedExpression += part + " ";
        }

        replacedExpression = replacedExpression.Trim();

        return replacedExpression;
    }

    static string AddSpacesToScriptVariableExpression(string expression)
    {
        string expressionNew = "";

        bool isPartOfVariableName = false;
        for (int i = 0; i < expression.Length; i++)
        {
            string letter = expression[i].ToString();

            bool newIsIsPartOfVariableName = BehaviorScriptManager.validVariableNameChars.Contains(letter);
            if ( isPartOfVariableName != newIsIsPartOfVariableName)
            {
                expressionNew += " ";
            }

            expressionNew += letter;
            
            isPartOfVariableName = newIsIsPartOfVariableName;
        }

        expressionNew = expressionNew.Replace("(", "( ");
        expressionNew = expressionNew.Replace(")", ") ");
        expressionNew = expressionNew.Replace("  ", " ");

        expressionNew = expressionNew.Trim();

        return expressionNew;
    }
    
    public string RemovePrivacyRelevantFromWebTellData(string data)
    {
        const string anonymized = "anonymized";
        
        List<Person> persons = Managers.personManager.GetPersons();
        foreach (Person person in persons)
        {
            string screenName = person.screenName.ToLower();
            if (screenName.Length >= 3)
            {
                data = data.Replace(person.screenName, anonymized);
            }
            else
            {
                data = anonymized;
            }
        }
        
        if (Managers.areaManager.isPrivate || Managers.areaManager.isExcluded)
        {
            if ( Managers.areaManager.currentAreaName.ToLower() == data )
            {
                data = anonymized;
            }
        }

        return data;
    }

}
