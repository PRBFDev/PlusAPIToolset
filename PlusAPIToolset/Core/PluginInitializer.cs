using BepInEx;

namespace PlusAPIToolset.Core {
    [BepInPlugin(PlusModInfo.GUID, PlusModInfo.Name, PlusModInfo.Version)]
    public class PluginInitializer : BaseUnityPlugin {
        public static PluginInitializer? Instance { get; private set; }

        private void Awake() {
            Instance = this;
            HarmonyBuilder.Prepatch(PlusModInfo.GUID);
            var plusAssets = gameObject.AddComponent<PlusAssetsManager>();
            ObjectToolkit.RegisterInstance(plusAssets);
            SodaBuilder.Init(this);
            SodaBuilder.EnsureDirectoriesExist(PlusModInfo.GUID);
            SodaBuilder.AutoRegisterAll();
        }
    }
}
