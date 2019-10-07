using System;
using UnityEngine;
using SimpleJSON;
using System.Collections.Generic;

public class JsonHelper
{
    // Various JSON conversion helper methods, e.g. getting
    // a Vector3 as proper string.
    
    public static string GetJson(Vector3 v)
    {
        return "[" + v.x + "," + v.y + "," + v.z + "]";
    }
    
    public static string GetJsonNoBrackets(Vector3 v)
    {
        return v.x + "," + v.y + "," + v.z;
    }
    
    public static string GetJson(string s)
    {
        return "\"" + Escape(s) + "\"";
    }
    
    public static string GetJson(bool b)
    {
        return b ? "true" : "false";
    }

    public static string GetJson(Color v)
    {
        return "[" + v.r + "," + v.g + "," + v.b + "]";
    }
    
    public static string GetJson(BoolVector3 v)
    {
        return "[" + BoolStringNumber(v.x) + "," + BoolStringNumber(v.y) + "," + BoolStringNumber(v.z) + "]";
    }
    
    static string BoolStringNumber(bool b)
    {
        return b ? "1" : "0";
    }

    public static Vector3 GetVector3(JSONNode node)
    {
        return new Vector3(node[0].AsFloat, node[1].AsFloat, node[2].AsFloat);
    }
    
    public static Color GetColor(JSONNode node)
    {
        return new Color(node[0].AsFloat, node[1].AsFloat, node[2].AsFloat);
    }
    
    public static BoolVector3 GetBoolVector3(JSONNode node)
    {
        return new BoolVector3(node[0].AsInt == 1, node[1].AsInt == 1, node[2].AsInt == 1);
    }

    public static string GetStringDictionaryAsArray(Dictionary<string,string> dictionary)
    {
        string s = "";
        foreach (KeyValuePair<string, string> entry in dictionary)
        {
            if (s != "") { s += ","; }
            s += "[" + GetJson(entry.Key) + "," + GetJson(entry.Value) + "]";
        }
        return s;
    }

    static string Escape(string s)
    {
        if (s != null)
        {
            s = s.Replace(Environment.NewLine, "[newline]");
            s = s.Replace("\\", "");
            s = s.Replace("\"", "\\\"");
            s = s.Replace("[newline]", "\\n");
        }
        return s;
    }

}
