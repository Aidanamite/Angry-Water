using UnityEngine;
using HarmonyLib;
using System;
using UltimateWater;
using System.Collections.Generic;
using System.IO;
using RaftModLoader;
using HMLLibrary;

public class AngryWater : Mod
{
    Harmony harmony;
    public static Dictionary<WaterWavesSpectrum, (float, float)> edited = new Dictionary<WaterWavesSpectrum, (float, float)>();
    static string configPath = Path.Combine(SaveAndLoad.WorldPath, "AngryWater.json");
    public static JSONObject Config = getSaveJson();
    public static float wavePower
    {
        get
        {
            if (Config.IsNull || !Config.HasField("wavePower"))
                return 1.7f;
            return Config.GetField("wavePower").n;
        }
        set
        {
            if (!Config.IsNull && Config.HasField("wavePower"))
                Config.SetField("wavePower", value);
            else
                Config.AddField("wavePower", value);
        }
    }
    public static float waveMulti
    {
        get
        {
            if (Config.IsNull || !Config.HasField("waveMulti"))
                return 1;
            return Config.GetField("waveMulti").n;
        }
        set
        {
            if (!Config.IsNull && Config.HasField("waveMulti"))
                Config.SetField("waveMulti", value);
            else
                Config.AddField("waveMulti", value);
        }
    }
    public void Start()
    {
        harmony = new Harmony("com.aidanamite.AngryWater");
        harmony.PatchAll();
        foreach (var p in Resources.FindObjectsOfTypeAll<WaterProfile>())
        {
            Traverse obj = Traverse.Create(p.Data.Spectrum);
            edited.Add(p.Data.Spectrum, (obj.Field("_Amplitude").GetValue<float>(), obj.Field("_WindSpeed").GetValue<float>()));
        }
        RemodifySpectrums();
        Log("Mod has been loaded!");
    }

    public void OnModUnload()
    {
        harmony.UnpatchAll();
        foreach (var s in edited)
        {
            Traverse obj = Traverse.Create(s.Key);
            obj.Field("_Amplitude").SetValue(s.Value.Item1);
            obj.Field("_WindSpeed").SetValue(s.Value.Item2);
        }
        edited.Clear();
        MarkDirty();
        Log("Mod has been unloaded!");
    }

    public static void Log(object message)
    {
        Debug.Log("[Angry Water]: " + message.ToString());
    }

    static void RemodifySpectrums()
    {
        foreach (var s in edited)
        {
            Traverse obj = Traverse.Create(s.Key);
            obj.Field("_Amplitude").SetValue(EditValue(s.Value.Item1));
            obj.Field("_WindSpeed").SetValue(EditValue(s.Value.Item2));
        }
        MarkDirty();
    }

    static void MarkDirty()
    {
        foreach (var w in Resources.FindObjectsOfTypeAll<Water>())
        {
            foreach (var p in w.ProfilesManager.Profiles)
                p.Profile.Dirty = true;
            w.WindWaves.SpectrumResolver.GetCachedSpectraDirect().Clear();
        }
    }

    [ConsoleCommand(name: "wavePower", docs: "Syntax: 'wavePower <primary> <secondary>'  Changes the strength of waves")]
    public static string MyCommand(string[] args)
    {
        if (args.Length < 2)
            return "Not enough arguments";
        if (args.Length > 2)
            return "Too many arguments";
        try
        {
            float multi = float.Parse(args[1]);
            wavePower = float.Parse(args[0]);
            waveMulti = multi;
            saveJson(Config);
            RemodifySpectrums();
            return $"Wave power changed to [ wave ^ {wavePower} x {waveMulti} ]";
        }
        catch
        {
            return "Failed to parse either " + args[0] + " or " + args[1] + " as a number";
        }
    }

    public static float EditValue(float originalValue)
    {
        //Log("Data-- " + originalValue.ToString() + ", " + wavePower.ToString() + ", " + waveMulti.ToString() + ", " + ((float)Math.Pow(originalValue, wavePower)).ToString());
        return (float)Math.Pow(originalValue, wavePower) * waveMulti;
    }

    private static JSONObject getSaveJson()
    {
        JSONObject data;
        try
        {
            data = new JSONObject(File.ReadAllText(configPath));
        }
        catch
        {
            data = JSONObject.Create();
            saveJson(data);
        }
        return data;
    }

    private static void saveJson(JSONObject data)
    {
        try
        {
            File.WriteAllText(configPath, data.ToString());
        }
        catch (Exception err)
        {
            Log("An error occured while trying to save settings: " + err.Message);
        }
    }
}

[HarmonyPatch(typeof(WaterWavesSpectrum), MethodType.Constructor, new Type[] { typeof(float), typeof(float), typeof(float), typeof(float) })]
public class Patch_WaveCalculatorCreate
{
    static void Prefix(WaterWavesSpectrum __instance, ref float amplitude, ref float windSpeed)
    {
        AngryWater.edited.Add(__instance, (amplitude, windSpeed));
        amplitude = AngryWater.EditValue(amplitude);
        windSpeed = AngryWater.EditValue(windSpeed);
    }
}