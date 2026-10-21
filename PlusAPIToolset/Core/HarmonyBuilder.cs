using HarmonyLib;

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

    [HarmonyPatch(typeof(GameInitializer), "Initialize")]
    internal static class GameInitializerPatch {
        [HarmonyPrefix]
        private static void Prefix(GameInitializer __instance) {
            SceneObject? sceneObj = Singleton<CoreGameManager>.Instance != null ? Singleton<CoreGameManager>.Instance.sceneObject : null;
            if (sceneObj != null) {
                SodaBuilder.InjectIntoSceneObject(sceneObj);
            }
        }
    }

    [HarmonyPatch(typeof(LevelBuilder), "StartGenerate")]
    internal static class LevelBuilderPatch {
        [HarmonyPrefix]
        private static void Prefix(LevelBuilder __instance) {
            if (__instance.ld != null) {
                SodaBuilder.InjectIntoLevelParameters(__instance.ld, __instance.scene);
            }
        }
    }

    [HarmonyPatch(typeof(StoreScreen), "Start")]
    internal static class StoreScreenPatch {
        [HarmonyPrefix]
        private static void Prefix(StoreScreen __instance) {
            SodaBuilder.InjectIntoShopScreen(__instance);
        }
    }
}
