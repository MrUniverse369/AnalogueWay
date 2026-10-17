using System;

/// <summary>
/// Everything that is written to a save. Data only, no logic.
/// To save something new: add a field here (with a sensible default), then read/write it in
/// GameSession.Capture and GameSession.Apply. Old saves without the field load with the default.
/// If a field changes meaning or is removed, bump SaveSystem.CurrentVersion and handle it in SaveSystem.Migrate.
/// </summary>
[Serializable]
public class SaveData
{
    public int version = SaveSystem.CurrentVersion;
    public string savedAtUtc;

    // Scene name, not build index, so reordering Build Settings can't break saves.
    public string sceneName;

    public int coins;
    public int lives = GameSession.StartingLives;
    public float chi = GameSession.StartingChi;
    public int deaths;
    public bool inLivingWorld;

    // ZONEMAP: the furthest level reached, which unlocks the zone select. Older saves leave it empty and use sceneName.
    public string furthestScene;
}
