using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace PlusAPIToolset.Core {
    public static class HarmonyBuilder {
        public static Harmony Prepatch(string GUID) {
            Harmony harmony = new Harmony(GUID);
            harmony.PatchAll();
            return harmony;
        }
    }

    [HarmonyPatch(typeof(Entity), "Update")]
    internal static class EntityUpdatePatch {
        [HarmonyPrefix]
        private static bool Prefix(Entity __instance) {
            if (__instance.Ec == null) {
                return false;
            }
            return true;
        }
    }
}
