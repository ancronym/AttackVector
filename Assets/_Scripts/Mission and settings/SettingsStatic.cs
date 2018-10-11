using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SettingsStatic {

    public enum Difficulty { easy, medium, hard };
    public enum CamSettings { north, player };

    public static Difficulty difficulty;
    public static CamSettings camSettings;

    public static bool debug;    
}
