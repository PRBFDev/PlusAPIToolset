using BepInEx;
using System;

namespace PlusAPIToolset {

    [BepInPlugin(PlusModInfo.GUID,PlusModInfo.Name,PlusModInfo.Version)]
    public class PluginInitializer  : BaseUnityPlugin
    {
        private void Awake() {
            HarmonyBuilder.Prepatch(PlusModInfo.GUID);
        }
    }
}
