using UnityEngine;
using UnityEngine.Assertions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using UnityEngine.SceneManagement;

public static class Misc
{
    // Holding misellaneous, general-need functionality.

    public static bool[] trueFalse = new bool[] { true, false };

    public static Vector3? PlayerPrefsGetVector3(string key)
    {
        Vector3? vector = null;

        if (PlayerPrefs.HasKey(key + "_x") &&
                PlayerPrefs.HasKey(key + "_y") &&
                PlayerPrefs.HasKey(key + "_z"))
        {

            vector = new Vector3(
                PlayerPrefs.GetFloat(key + "_x"),
                PlayerPrefs.GetFloat(key + "_y"),
                PlayerPrefs.GetFloat(key + "_z")
            );
        }

        return vector;
    }

    public static void PlayerPrefsSetVector3(string key, Vector3 vector)
    {
        PlayerPrefs.SetFloat(key + "_x", vector.x);
        PlayerPrefs.SetFloat(key + "_y", vector.y);
        PlayerPrefs.SetFloat(key + "_z", vector.z);
    }

    public static string AddThousandSeparatorComma(int number)
    {
        return number.ToString("n0");
    }

    public static int BoolToInt(bool value)
    {
        return value ? 1 : 0;
    }

    public static string ToTitleCase(string s)
    {
        if (s == null || s.Length == 0) { return s; }

        string[] splits = s.Split(' ');
        for (int i = 0; i < splits.Length; i++)
        {
            if (splits[i].Length > 1)
            {
                splits[i] = Char.ToUpper(splits[i][0]) + splits[i].Substring(1);
                break;
            }
        }

        return String.Join(" ", splits);
    }

    public static Vector3 ReduceVector3Digits(Vector3 v, int digits)
    {
        return new Vector3(
            (float)Math.Round(v.x, digits),
            (float)Math.Round(v.y, digits),
            (float)Math.Round(v.z, digits)
        );
    }

    public static string RemoveFromStart(string s, string startString)
    {
        if (s != null && s.IndexOf(startString) == 0)
        {
            s = s.Substring(startString.Length);
        }
        return s;
    }

    public static Side TopographyIdToSide(TopographyId topographyId)
    {
        return topographyId == TopographyId.Left ? Side.Left : Side.Right;
    }

    public static Vector3 GetRandomVector3(float maxOffset)
    {
        return new Vector3(
                UnityEngine.Random.Range(-maxOffset, maxOffset),
                UnityEngine.Random.Range(-maxOffset, maxOffset),
                UnityEngine.Random.Range(-maxOffset, maxOffset)
                );
    }

    public static void ReloadScene()
    {
        AreaManager.areaToLoadAfterSceneChange = Managers.areaManager.currentAreaName;
        CleanUpBeforeSceneChange();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public static void CleanUpBeforeSceneChange()
    {
        Managers.broadcastNetworkManager.Disconnect();
        Managers.personManager.ourPerson.ClearComponentNetworkViewIds();
        #if UNITY_STANDALONE_WIN
            Managers.steamManager.ShutdownSteam();
        #endif
        Managers.ClearStartupCompletedHandler();
    }

    public static bool ContainsAny(string haystack, IEnumerable<string> needles)
    {
        return needles.Any(haystack.Contains);
    }

    public static void RandomizeRandomizer()
    {
        // Needed to reset for further randomization if one had to
        // seed it before with e.g. UnityEngine.Random.InitState(1)
        UnityEngine.Random.seed = System.Environment.TickCount;
    }

    public static bool ContainsCaseInsensitive(string s, string sFind)
    {
        // A case-insensitive variant of native s.Contains()
        return s != null && sFind != null &&
                s.IndexOf(sFind, StringComparison.InvariantCultureIgnoreCase) >= 0;
    }

    public static string GetVector3ToSpaceSeparatedString(Vector3 v)
    {
        return v.x + " " + v.y + " " + v.z;
    }

    public static Vector3? GetSpaceSeparatedStringToVector3(string s, float max)
    {
        Vector3? vector3 = null;

        string[] vector3string = Misc.Split(s);
        if (vector3string.Length == 3)
        {

            float x, y, z;
            if (float.TryParse(vector3string[0], out x))
            {
                if (float.TryParse(vector3string[1], out y))
                {
                    if (float.TryParse(vector3string[2], out z))
                    {
                        vector3 = new Vector3(
                                Mathf.Clamp(x, -max, max),
                                Mathf.Clamp(y, -max, max),
                                Mathf.Clamp(z, -max, max)
                                );
                    }
                }
            }

        }

        return vector3;
    }

    public static float ClampMin(float value, float min)
    {
        if (value < min) { value = min; }
        return value;
    }

    public static float ClampMax(float value, float max)
    {
        if (value > max) { value = max; }
        return value;
    }

    public static string GetCompressedStringFromFloat(float value, int digits = 2, bool keepDot = false)
    {
        // Cuts to N digits and removes leading and trailing zeroes.
        // turns e.g. 0.2334 into .23, and 0.204 into 0.2.
        // Also see GetDecompressedFloatFromString()

        string s = value.ToString("F" + digits);

        s = s.TrimStart('0');
        s = s.TrimEnd('0');
        if (!keepDot && s.Length >= 2)
        {
            s = s.TrimEnd('.');
        }

        return s;
    }

    public static float GetDecompressedFloatFromString(string s)
    {
        // Reversal of GetCompressedStringFromFloat()

        float value = 0f;

        if (s != null)
        {

            if (s[0] == '.') { s = "0" + s; }

            float thisValue;
            if (float.TryParse(s, out thisValue))
            {
                value = thisValue;
            }

        }

        return value;
    }

    public static void DestroyMultiple(params GameObject[] gameObjects)
    {
        for (int i = 0; i < gameObjects.Length; i++)
        {
            GameObject.Destroy(gameObjects[i]);
        }
    }

    public static void DestroyMultiple(params TextMesh[] textMeshes)
    {
        for (int i = 0; i < textMeshes.Length; i++)
        {
            GameObject.Destroy(textMeshes[i]);
        }
    }

    public static void ShuffleArray<T>(T[] arr)
    {
        for (int i = arr.Length - 1; i > 0; i--)
        {
            int r = UnityEngine.Random.Range(0, i);
            T tmp = arr[i];
            arr[i] = arr[r];
            arr[r] = tmp;
        }
    }

    public static float AdjustPitchInOctaves(float octaveChange)
    {
        // https://answers.unity.com/questions/127562/pitch-in-unity.html
        const float semiTones = 12f;
        return Mathf.Pow(1.05946f, semiTones * octaveChange);
    }

    public static string CamelCaseToSpaceSeparated(string str)
    {
        var res = new StringBuilder();
        res.Append(str[0]);
        for (var i = 1; i < str.Length; i++)
        {
            if (char.IsUpper(str[i])) { res.Append(' '); }
            res.Append(str[i]);
        }
        return res.ToString().ToLower();
    }

    public static string GetImgurImageUrl(string s)
    {
        // Returns e.g. "http://i.imgur.com/PDHUwH7.png" if passed string is valid
        string imageUrl = null;
        if (!string.IsNullOrEmpty(s))
        {
            s = s.Replace("https://", "http://");
            if (s.StartsWith("http://i.imgur.com/") && (s.EndsWith(".png") || s.EndsWith(".jpg")))
            {
                imageUrl = s;
            }
        }
        return imageUrl;
    }

    public static Side GetOppositeSide(Side side)
    {
        return side == Side.Left ? Side.Right : Side.Left;
    }

    public static void SetAllObjectLayers(GameObject thisObject, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        Component[] components = thisObject.GetComponentsInChildren(typeof(Transform), true);
        foreach (Component component in components)
        {
            component.gameObject.layer = layer;
        }
    }

    public static string BoolAsCheckmarkOrCross(bool thisBool)
    {
        return thisBool ? "✔" : "✖";
    }

    public static string BoolAsCheckmarkOrCross(bool? thisBool)
    {
        return BoolAsCheckmarkOrCross(thisBool == true);
    }

    public static string BoolAsYesNo(bool thisBool)
    {
        return thisBool == true ? "Yes" : "No";
    }

    public static string BoolAsYesNo(bool? thisBool)
    {
        return BoolAsYesNo(thisBool == true);
    }

    public static int GetHourIn12HourFormat(int hour)
    {
        return hour == 0 || hour == 12 || hour == 24 ? 12 : hour % 12;
    }

    public static Vector3 ClampVector3(Vector3 v, float min, float max)
    {
        return new Vector3(
            Mathf.Clamp(v.x, min, max),
            Mathf.Clamp(v.y, min, max),
            Mathf.Clamp(v.z, min, max)
        );
    }

    public static void OpenWindowsExplorerAtPath(string path)
    {
        path = path.Replace(@"/", @"\");
        System.Diagnostics.Process.Start("explorer.exe", "/select," + path);
    }

    public static string ReplaceCaseInsensitive(this string str, string oldValue, string newValue)
    {
        int prevPos = 0;
        string retval = str;
        int pos = retval.IndexOf(oldValue, StringComparison.InvariantCultureIgnoreCase);

        while (pos > -1)
        {
            retval = retval.Remove(pos, oldValue.Length);
            retval = retval.Insert(pos, newValue);
            prevPos = pos + newValue.Length;
            pos = retval.IndexOf(oldValue, prevPos, StringComparison.InvariantCultureIgnoreCase);
        }

        return retval;
    }

    public static void SetLocalPositionX(GameObject thisGameObject, float value)
    {
        Misc.SetLocalPositionX(thisGameObject.transform, value);
    }

    public static void SetLocalPositionY(GameObject thisGameObject, float value)
    {
        Misc.SetLocalPositionY(thisGameObject.transform, value);
    }

    public static void SetLocalPositionZ(GameObject thisGameObject, float value)
    {
        Misc.SetLocalPositionZ(thisGameObject.transform, value);
    }

    public static void SetLocalPositionX(Transform thisTransform, float value)
    {
        Vector3 vector3 = thisTransform.localPosition;
        vector3.x = value;
        thisTransform.localPosition = vector3;
    }

    public static void SetLocalPositionY(Transform thisTransform, float value)
    {
        Vector3 vector3 = thisTransform.localPosition;
        vector3.y = value;
        thisTransform.localPosition = vector3;
    }

    public static void SetLocalPositionZ(Transform thisTransform, float value)
    {
        Vector3 vector3 = thisTransform.localPosition;
        vector3.z = value;
        thisTransform.localPosition = vector3;
    }

    public static string RemoveCloneFromName(string gameObjectName)
    {
        return gameObjectName.Replace("(Clone)", "");
    }

    public static string RemoveCloneFromName(GameObject gameObject)
    {
        gameObject.name = gameObject.name.Replace("(Clone)", "");
        return gameObject.name;
    }

    public static string RemoveCloneFromName(Transform transform)
    {
        transform.name = transform.name.Replace("(Clone)", "");
        return transform.name;
    }

    public static bool ColorsAreSame(Color c1, Color c2)
    {
        return c1.r == c2.r &&
               c1.g == c2.g &&
               c1.b == c2.b;
    }

    public static int GetThisCharInStringCount(string text, char c)
    {
        int count = 0;
        foreach (char ch in text)
        {
            if (ch.Equals(c)) { count++; }
        }
        return count;
    }

    public static long GetDirectorySizeInBytes(string path, string extension = "*")
    {
        long bytes = 0;

        if (Directory.Exists(path))
        {
            string[] files = Directory.GetFiles(path, "*." + extension);
            foreach (string name in files)
            {
                FileInfo info = new FileInfo(name);
                bytes += info.Length;
            }
        }

        return bytes;
    }

    public static void Destroy(GameObject thisObject)
    {
        // Destroy() isn't immediate and DestroyImmediate() not suggested/ doesn't work in all contexts
        thisObject.tag = "Untagged";
        thisObject.name = Universe.objectNameIfAlreadyDestroyed;
        GameObject.Destroy(thisObject);
    }

    public static bool IsDestroyed(GameObject thisObject)
    {
        return thisObject == null || thisObject.name == Universe.objectNameIfAlreadyDestroyed;
    }

    public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
    {
        Vector3 dir = point - pivot;
        dir = Quaternion.Euler(angles) * dir;
        point = dir + pivot;
        return point;
    }

    public static string ReplaceRepeated(string s, string sToFind, string sToReplace)
    {
        string oldS = "";
        while (s != oldS)
        {
            oldS = s;
            s = s.Replace(sToFind, sToReplace);
        }
        return s;
    }

    public static bool CtrlIsPressed()
    {
        return Input.GetKey(KeyCode.LeftControl) ||
               Input.GetKey(KeyCode.RightControl);
    }

    public static bool AltIsPressed()
    {
        return Input.GetKey(KeyCode.LeftAlt) ||
               Input.GetKey(KeyCode.RightAlt);
    }

    public static Color ColorStringToColor(string colorString)
    {
        // accepts strings like "255,0,255"
        Color color = Color.white;
        if (!string.IsNullOrEmpty(colorString))
        {
            string[] rgb = Misc.Split(colorString, ",");
            int alpha = 255;
            int length = rgb.Length;
            if (length == 4) { alpha = int.Parse(rgb[3]); }
            if (length >= 3 && length <= 4)
            {
                color = new Color32(
                        (byte)int.Parse(rgb[0]),
                        (byte)int.Parse(rgb[1]),
                        (byte)int.Parse(rgb[2]),
                        (byte)alpha
                        );
            }
        }
        return color;
    }

    public static string GetHowLongAgoText(string dateTimeString)
    {
        string s = "";

        const string unitPrefix = " ";
        const float secondsInMinute = 60;
        const float minutesInHour = 60;
        const float hoursInDay = 24;
        const float daysInMonth = 30.44f;
        const float monthsInYear = 12;

        DateTime dateTime = Convert.ToDateTime(dateTimeString);
        TimeSpan timeSpan = new TimeSpan(DateTime.Now.Ticks - dateTime.Ticks);
        float seconds = Mathf.Round((float)Math.Abs(timeSpan.TotalSeconds));

        s = seconds + unitPrefix + GetPluralOrSingular("second", seconds);
        if (seconds >= secondsInMinute)
        {
            float minutes = Mathf.Floor(seconds / secondsInMinute);
            s = minutes + unitPrefix + GetPluralOrSingular("minute", minutes);
            if (minutes >= minutesInHour)
            {
                float hours = Mathf.Floor(minutes / minutesInHour);
                s = hours + unitPrefix + GetPluralOrSingular("hour", hours);
                if (hours >= hoursInDay)
                {
                    float days = Mathf.Floor(hours / hoursInDay);

                    s = days + unitPrefix + GetPluralOrSingular("day", days);
                    if (days >= daysInMonth)
                    {
                        float months = Mathf.Floor(days / daysInMonth);
                        s = months + unitPrefix + GetPluralOrSingular("month", months);
                        if (months >= monthsInYear)
                        {
                            float years = Mathf.Floor(months / monthsInYear);
                            s = years + unitPrefix + GetPluralOrSingular("year", years);
                        }
                    }

                }
            }
        }
        s += " ago";

        return s;
    }

    public static string GetPluralOrSingular(string word, float amount)
    {
        return amount == 1f ? word : word + 's';
    }

    public static string HtmlEncode(string s)
    {
        s = s.Replace("&", "&amp;");
        s = s.Replace("\"", "&quot;");
        s = s.Replace("'", "&#39;");
        s = s.Replace("<", "&lt;");
        s = s.Replace(">", "&gt;");
        return s;
    }

    public static Vector3 AddToAllVectorValues(Vector3 vector, float value)
    {
        vector.x += value;
        vector.y += value;
        vector.z += value;
        return vector;
    }

    public static string GetHowManyDaysAgo(string dateString)
    {
        DateTime startDate = DateTime.Parse(dateString);
        DateTime now = DateTime.Now;
        TimeSpan elapsed = now.Subtract(startDate);
        double daysAgo = elapsed.TotalDays;
        return Mathf.Round((float)daysAgo).ToString();
    }

    public static string GetDateStringXYearsInFuture(int x)
    {
        DateTime now = DateTime.Now;
        now.AddYears(x);
        return now.ToString();
    }

    public static string[] Split(string text, string stringToUseForSplitting = " ", StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries)
    {
        if (text != null && text != "" && text.IndexOf(stringToUseForSplitting) >= 0)
        {
            string[] separators = new string[] { stringToUseForSplitting };
            return text.Split(separators, options);
        }
        else
        {
            return new string[1] { text };
        }
    }

    public static int CapInt(int numberToCap, int maxToCapTo)
    {
        if (numberToCap > maxToCapTo) { numberToCap = maxToCapTo; }
        return numberToCap;
    }

    public static void DeleteCookiesFileIfExists()
    {
        string path = GetAppRootPath() + "cookies.dat";
        if (File.Exists(path)) { File.Delete(path); }
    }

    public static string GetAppRootPath()
    {
        string path = Application.dataPath;
        if (Application.platform == RuntimePlatform.OSXPlayer)
        {
            path += "/../../";
        }
        else
        {
            path += "/../";
        }
        return path;
    }

    public static DateTime DeserializeDateTime(string stringDateTimeUtc)
    {
        DateTime dateTimeUtc = DateTime.Parse(stringDateTimeUtc);
        dateTimeUtc = DateTime.SpecifyKind(dateTimeUtc, DateTimeKind.Utc);
        return dateTimeUtc;
    }

    public static string SerializeDateTime(DateTime dateTimeUtc)
    {
        dateTimeUtc = DateTime.SpecifyKind(dateTimeUtc, DateTimeKind.Utc);
        return dateTimeUtc.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public static string WrapWithNewlines(string input, int rowLength, int maxLines = -1)
    {
        // Via http://codereview.stackexchange.com/a/54730

        StringBuilder result = new StringBuilder();
        StringBuilder line = new StringBuilder();
        Stack<string> stack = new Stack<string>(ReverseString(input).Split(' '));
        int lineCount = 0;

        while (stack.Count > 0)
        {
            var word = stack.Pop();
            if (word.Length > rowLength)
            {
                string head = word.Substring(0, rowLength);
                string tail = word.Substring(rowLength);

                word = head;
                stack.Push(tail);
            }

            if (line.Length + word.Length > rowLength)
            {
                if (maxLines == -1 || lineCount < maxLines - 1)
                {
                    result.AppendLine(line.ToString());
                    lineCount++;
                    line = new StringBuilder(); // Clear() not supported
                }
                else
                {
                    result.Append(line);
                    return result.ToString() + "..";
                }
            }

            line.Append(word + " ");
        }

        result.Append(line);
        return result.ToString();
    }

    static String ReverseString(String str)
    {
        int word_length = 0;
        String result = "";
        for (int i = 0; i < str.Length; i++)
        {
            if (str[i] == ' ')
            {
                result = " " + result;
                word_length = 0;
            }
            else
            {
                result = result.Insert(word_length, str[i].ToString());
                word_length++;
            }
        }
        return result;
    }

    public static string GetNumberlessString(string s)
    {
        return Regex.Replace(s, @"[\d-]", string.Empty);
    }

    public static string HtmlDecode(string s)
    {
        // Didn't get the two following to be recognized, so using this Misc function:
        // System.Net.WebUtility.HtmlDecode()
        // System.Web.HttpUtility.HtmlDecode();
        s = s.Replace("&gt;", ">");
        s = s.Replace("&lt;", "<");
        s = s.Replace("&quot;", "\"");
        s = s.Replace("&#39;", "'");
        s = s.Replace("&amp;", "&");
        return s;
    }

    public static List<string> GetTextsBetween(string source, string start, string end)
    {
        // via http://stackoverflow.com/a/13780976/34170
        var results = new List<string>();
        string pattern = string.Format(
                "{0}({1}){2}",
                Regex.Escape(start),
                ".+?",
                Regex.Escape(end));

        foreach (Match m in Regex.Matches(source, pattern))
        {
            results.Add(m.Groups[1].Value);
        }
        return results;
    }

    public static string GetTextBetween(string source, string start, string end)
    {
        string pattern = string.Format(
                "{0}({1}){2}",
                Regex.Escape(start),
                ".+?",
                Regex.Escape(end));

        foreach (Match m in Regex.Matches(source, pattern))
        {
            return m.Groups[1].Value;
        }
        return null;
    }

    public static int GetPercentInt(int max, int value)
    {
        return Mathf.RoundToInt(((float)value / (float)max) * 100);
    }

    public static Color GetGray(float brightness)
    {
        return new Color(brightness, brightness, brightness);
    }

    public static Vector3 GetUniformVector3(float value)
    {
        return new Vector3(value, value, value);
    }

    public static float GetLargestValueOfVector(Vector3 vector)
    {
        float largest = vector.x;
        if (vector.y > largest) { largest = vector.y; }
        if (vector.z > largest) { largest = vector.z; }
        return largest;
    }

    public static float GetSmallestValueOfVector(Vector3 vector)
    {
        float smallest = vector.x;
        if (vector.y < smallest) { smallest = vector.y; }
        if (vector.z < smallest) { smallest = vector.z; }
        return smallest;
    }

    public static string[] GetStringInParts(this string text, int partLength, int maxParts = -1)
    {
        var charCount = 0;
        var lines = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string[] parts = lines.GroupBy(w => (charCount += (((charCount % partLength) + w.Length + 1 >= partLength)
                        ? partLength - (charCount % partLength) : 0) + w.Length + 1) / partLength)
                    .Select(g => string.Join(" ", g.ToArray()))
                    .ToArray();
        if (maxParts != -1)
        {
            if (parts.Length > maxParts)
            {
                parts[maxParts - 1] = Misc.Truncate(parts[maxParts - 1] + "..", partLength);
            }
            Array.Resize(ref parts, maxParts);
        }
        return parts;
    }

    public static Hand GetAHandOfOurs()
    {
        return Managers.personManager != null && Managers.personManager.ourPerson != null ?
                Managers.personManager.ourPerson.GetAHand() : null;
    }

    public static string GetRandomId()
    {
        return ObjectId.GenerateNewId().ToString();
    }

    public static bool Chance(float percent = 50)
    {
        return UnityEngine.Random.Range(0, 100) <= percent;
    }

    public static GameObject[] GetChildrenAsArray(Transform transform)
    {
        GameObject[] children = new GameObject[transform.childCount];
        int i = 0;
        foreach (Transform child in transform)
        {
            children[i++] = child.gameObject;
        }
        return children;
    }

    public static string Truncate(string text, int maxLength = 20, bool addDots = true)
    {
        const int dotsLength = 2;
        if (!string.IsNullOrEmpty(text) && text.Length > maxLength)
        {
            if (addDots)
            {
                text = text.Substring(0, maxLength - dotsLength) + "..";
            }
            else
            {
                text = text.Substring(0, maxLength);
            }
        }
        return text;
    }

    public static string TruncateRightAligned(string text, int maxLength = 28, string prefix = "..")
    {
        if (text.Length > maxLength)
        {
            int startCharsToTrim = text.Length - maxLength;
            text = prefix + text.Substring(startCharsToTrim, text.Length - startCharsToTrim);
        }
        return text;
    }

    public static string ReplaceAll(string s, string sFind, string sReplace)
    {
        string sOld = null;
        while (sOld != s)
        {
            sOld = s;
            s = s.Replace(sFind, sReplace);
        }
        return s;
    }

    public static GameObject FindObject(GameObject parentObject, string name)
    {
        if (parentObject == null)
        {
            GameObject[] gameObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject thisObject in gameObjects)
            {
                if (thisObject.name == name)
                {
                    return thisObject;
                }
            }
        }
        else
        {
            Component[] components = parentObject.GetComponentsInChildren(typeof(Transform), true);
            foreach (Transform thisTransform in components)
            {
                if (thisTransform.name == name)
                {
                    return thisTransform.gameObject;
                }
            }
        }
        return null;
    }

    public static bool HasChildWithTag(Transform transform, string tag)
    {
        bool hasIt = false;
        foreach (Transform child in transform)
        {
            if (child.CompareTag(tag))
            {
                hasIt = true;
                break;
            }
        }
        return hasIt;
    }


    public static GameObject GetChildWithTag(Transform transform, string tag)
    {
        GameObject child = null;
        foreach (Transform childTransform in transform)
        {
            if (childTransform.CompareTag(tag))
            {
                child = childTransform.gameObject;
                break;
            }
        }
        return child;
    }

    public static string BoolToYesNo(bool value)
    {
        return value ? "Yes" : "No";
    }

    public static bool CommandLineArgsContain(string value)
    {
        var args = Environment.GetCommandLineArgs();
        return Array.IndexOf(args, value) > -1;
    }

    public static bool ShouldBypassAuth()
    {
        return CommandLineArgsContain("bypass");
    }

    public static bool ShouldDisableVR()
    {
        return CommandLineArgsContain("novr");
    }

    public static void AssertAllNotNull(System.Object[] objects, string message)
    {
        for (int i = 0; i < objects.Length; i++)
        {
            Assert.IsNotNull<System.Object>(objects[i], message);
        }
    }

    public static bool IsValidObjectIdString(string objectIdString)
    {
        return (objectIdString != "" && objectIdString.Length == 24);
    }


    //HTTP status code helpers

    public static int getResponseCode(WWW request)
    {
        int ret = 0;
        if (request.responseHeaders == null)
        {
            Log.Error("getResponseCode - no response headers.");
        }
        else
        {
            if (!request.responseHeaders.ContainsKey("STATUS"))
            {
                Log.Error("getResponseCode - response headers has no STATUS."); //NOREMOTE
            }
            else
            {
                ret = parseResponseCode(request.responseHeaders["STATUS"]);
            }
        }

        return ret;
    }


    public static bool isServerDown(int httpResponseCode)
    {
        bool isDown = false;

        //503; Temp using 302, as rproxy will do a 303->503 in mataintenance situations, and unity not support redirects.
        if (httpResponseCode == 302 || httpResponseCode == 303)
        {
            isDown = true;
        }

        //5xx are server failures
        if ((httpResponseCode >= 500) && (httpResponseCode < 600))
        {
            isDown = true;
        }

        return isDown;
    }

    public static bool wwwObjectHasStatusHeader(WWW req)
    {
        return req.responseHeaders != null && req.responseHeaders.ContainsKey("STATUS");
    }

    public static int parseResponseCode(string statusLine)
    {
        int ret = 0;

        string[] components = statusLine.Split(' ');
        if (components.Length < 3)
        {
            Log.Error("invalid response status: " + statusLine);
        }
        else
        {
            if (!int.TryParse(components[1], out ret))
            {
                Log.Error("invalid response code: " + components[1]);
            }
        }

        return ret;
    }

    public static void LogOff()
    {
        Managers.serverManager.CancelAuthentication();
        Managers.thingManager.UnloadPlacements();
        Managers.broadcastNetworkManager.Disconnect();
    }

    public static bool ForceUpdateIsNeeded(string versionMajorToEnforceUpdates_serverString)
    {
        bool isNeeded = false;
        int versionMajorToEnforceUpdates_server;
        if (!string.IsNullOrEmpty(versionMajorToEnforceUpdates_serverString) &&
                Int32.TryParse(versionMajorToEnforceUpdates_serverString, out versionMajorToEnforceUpdates_server))
        {
            Universe.versionMajorAsToldByServer = versionMajorToEnforceUpdates_serverString;
            isNeeded = versionMajorToEnforceUpdates_server > Universe.versionMajorToEnforceUpdates;
        }
        else
        {
            Log.Error("Server's response.currentAppVersion seems to send wrong data, value is: " +
                    versionMajorToEnforceUpdates_serverString);
        }
        return isNeeded;
    }

    public static void ForceUpdate()
    {
        string text = "Please update to the latest version of Anyland and start again " +
                "(there were some changes requiring this). Thanks! " +
                "If there's any questions, please check out the Steam forum or email us at we@manyland.com " +
                "\n\n[Your version: " + Universe.GetClientVersionDisplay() + " | " +
                "Server version: " + Universe.versionMajorAsToldByServer + "]";

        Managers.errorManager.ShowCriticalHaltError(
            text, showBoilerplate: false, haveErrorSceneReturnToUniverse: false,
            goStraightToErrorScene: true);
    }

    public static string CreateJsonSuperDocument(Dictionary<string, string> jsonNameValuePairs)
    {
        var jsonDoc = "{";
        var isFirstItem = true;
        foreach (KeyValuePair<string, string> item in jsonNameValuePairs)
        {
            // do something with entry.Value or entry.Key
            if (!isFirstItem)
            {
                jsonDoc += ",";
                isFirstItem = false;
            }
            jsonDoc += "\"" + item.Key + "\":" + item.Value;
        }
        jsonDoc += "}";

        return jsonDoc;
    }

    public static void RestartApp()
    {
        #if UNITY_EDITOR
            Log.Debug("If this was the exe running, it would trigger a restart now.");
        #else
            string appPath = Application.dataPath.Replace("_Data", ".exe");
            System.Diagnostics.Process.Start(appPath);
            Application.Quit();
        #endif
    }

    public static void ExitApp()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    public static string SerializeStringList(List<string> list)
    {
        return "[" + string.Join(",", list.ToArray()) + "]";
    }

}
