using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// RELAY: works around a bug in com.unity.ai.assistant 2.20.0-pre.2 (the latest at the time of writing).
///
/// The AI Assistant talks to Unity through a background "relay" process. After a domain reload (entering Play
/// mode, a script change) the editor sometimes starts a second relay while the old one is still alive, but keeps
/// watching the old one. The old relay has lost its client, so 180 s later it shuts itself down (Logs/relay.txt:
/// "Auto-shutdown timer fired after 180s inactivity"). The editor then thinks its relay crashed, logs
///   connection.state_change oldState=Running newState=Failed error=Process exited unexpectedly. code=-1
/// and tears down the working connection too. Nothing in the package restarts a Failed relay, so the Assistant
/// stays disconnected until Unity is restarted.
///
/// This watches the relay state and, a few seconds after it goes Failed, starts a fresh relay (RelayService
/// .StartAsync is allowed from Failed). It is capped so a relay that really can't start doesn't loop forever.
/// RelayService is internal to the package, so it is reached by reflection; if the package changes shape or is
/// removed, this quietly does nothing. Delete this file once Unity ships a fix.
/// </summary>
[InitializeOnLoad]
static class RelayWatchdog
{
    private const string TypeName = "Unity.Relay.Editor.RelayService, Unity.AI.Assistant.Editor";
    private const double CheckInterval = 2.0;   // seconds between state checks
    private const double RestartDelay = 3.0;    // seconds to wait after a failure before restarting
    private const int MaxRestarts = 3;          // restarts allowed ...
    private const double RestartWindow = 300.0; // ... within this many seconds

    private static readonly FieldInfo InstanceField;
    private static readonly PropertyInfo StateProperty;
    private static readonly MethodInfo StartMethod;
    private static PropertyInfo statusProperty;

    private static double nextCheck;
    private static double failedSince = -1.0;
    private static double windowStart;
    private static int restartsInWindow;

    static RelayWatchdog()
    {
        Type service = Type.GetType(TypeName);
        if (service == null) return; // package not installed, or renamed
        InstanceField = service.GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
        StateProperty = service.GetProperty("State", BindingFlags.Instance | BindingFlags.Public);
        StartMethod = service.GetMethod("StartAsync", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (InstanceField == null || StateProperty == null || StartMethod == null) return;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < nextCheck) return;
        nextCheck = now + CheckInterval;

        // only look at a relay the package has already created; never create one ourselves
        object relay = InstanceField.GetValue(null);
        if (relay == null) return;

        if (Status(relay) != "Failed")
        {
            failedSince = -1.0;
            return;
        }
        if (failedSince < 0.0) failedSince = now;
        if (now - failedSince < RestartDelay) return;

        if (now - windowStart > RestartWindow)
        {
            windowStart = now;
            restartsInWindow = 0;
        }
        if (restartsInWindow >= MaxRestarts) return; // give up until the window passes; something else is wrong

        restartsInWindow++;
        failedSince = -1.0;
        Debug.Log($"[RelayWatchdog] The AI Assistant relay stopped (a known package bug after domain reloads). Restarting it ({restartsInWindow}/{MaxRestarts}).");
        try
        {
            StartMethod.Invoke(relay, null);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RelayWatchdog] Could not restart the AI Assistant relay: {e.GetBaseException().Message}");
        }
    }

    private static string Status(object relay)
    {
        object state = StateProperty.GetValue(relay);
        if (state == null) return null;
        if (statusProperty == null) statusProperty = state.GetType().GetProperty("Status");
        return statusProperty?.GetValue(state)?.ToString();
    }
}
