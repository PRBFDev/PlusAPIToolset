using System;
using BepInEx;
using UnityEngine;

namespace PlusAPIToolset.Core {
    [BepInPlugin(PlusModInfo.GUID, PlusModInfo.Name, PlusModInfo.Version)]
    public class PluginInitializer : BaseUnityPlugin {
        private void Awake() {
            HarmonyBuilder.Prepatch(PlusModInfo.GUID);
            var plusAssets = gameObject.AddComponent<PlusAssetsManager>();
            ObjectToolkit.RegisterInstance(plusAssets);
            SodaBuilder.EnsureDirectoriesExist(PlusModInfo.GUID);
            SodaBuilder.AutoRegisterAll();
        }
    }
}
