using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace PlusAPIToolset {
    public static class HarmonyBuilder {
     public static Harmony Prepatch(string GUID) {
            Harmony harmony = new Harmony(GUID);
            harmony.PatchAll();
            return harmony;
        }
    }
}