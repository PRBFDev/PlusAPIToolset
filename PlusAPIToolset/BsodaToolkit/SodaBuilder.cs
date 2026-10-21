using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using PlusAPIToolset.BsodaToolkit;
using PlusAPIToolset.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace PlusAPIToolset {
    public static class SodaBuilder {
        private static ITM_BSODA? s_originalBsoda;
        private static ItemObject? s_originalItemObject;
        private static readonly Dictionary<Type, GameObject> s_registeredPrefabs = new Dictionary<Type, GameObject>();
        private static readonly Dictionary<Type, ItemObject> s_registeredItems = new Dictionary<Type, ItemObject>();
        private static readonly List<Type> s_pendingTypes = new List<Type>();
        private static readonly Dictionary<string, Sprite> s_spriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, SoundObject> s_soundCache = new Dictionary<string, SoundObject>();
        private static MethodInfo? s_assetLoaderAudioClipFromFile;
        private static bool s_assetLoaderChecked;
        private static MethodInfo? s_getCustomLevelObjectsMethod;
        private static bool s_customLevelObjectsChecked;
        private static bool s_generatorManagementRegistered;

        public static IReadOnlyDictionary<Type, GameObject> RegisteredPrefabs {
            get {
                return s_registeredPrefabs;
            }
        }

        public static IReadOnlyDictionary<Type, ItemObject> RegisteredItems {
            get {
                return s_registeredItems;
            }
        }

        public static string GetCorePath(string guid) {
            return Path.Combine(Application.streamingAssetsPath, "Modded", guid, "Assets", "CustomSodas");
        }

        public static string GetImagesPath(string guid) {
            return Path.Combine(GetCorePath(guid), "Images");
        }

        public static string GetSoundsPath(string guid) {
            return Path.Combine(GetCorePath(guid), "Sounds");
        }

        public static void EnsureDirectoriesExist(string guid) {
            try {
                string imagesPath = GetImagesPath(guid);
                string soundsPath = GetSoundsPath(guid);

                if (!Directory.Exists(imagesPath)) {
                    Directory.CreateDirectory(imagesPath);
                }
                if (!Directory.Exists(soundsPath)) {
                    Directory.CreateDirectory(soundsPath);
                }
            } catch (Exception ex) {
                Debug.LogError($"[PlusAPIToolset] Failed to create asset directories for GUID '{guid}': {ex.Message}");
            }
        }

        public static void Init(BaseUnityPlugin plugin) {
            TryRegisterWithGeneratorManagement(plugin);
        }

        private static void TryRegisterWithGeneratorManagement(BaseUnityPlugin plugin) {
            if (s_generatorManagementRegistered) {
                return;
            }

            try {
                Type? genMgmtType = Type.GetType("MTM101BaldAPI.Registers.GeneratorManagement, MTM101BaldAPI");
                if (genMgmtType == null) {
                    Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                    for (int i = 0; i < assemblies.Length; i++) {
                        if (assemblies[i].GetName().Name == "MTM101BaldAPI") {
                            genMgmtType = assemblies[i].GetType("MTM101BaldAPI.Registers.GeneratorManagement");
                            break;
                        }
                    }
                }

                if (genMgmtType != null) {
                    MethodInfo? registerMethod = genMgmtType.GetMethod("Register");
                    if (registerMethod != null) {
                        Action<string, int, SceneObject> callback = (floorName, floorNumber, sceneObj) => {
                            InjectIntoSceneObject(sceneObj, floorName, floorNumber);
                        };
                        registerMethod.Invoke(null, new object[] { plugin, 3, callback });
                        s_generatorManagementRegistered = true;
                        Debug.Log("[PlusAPIToolset] Hooked into MTM101 GeneratorManagement pipeline.");
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning($"[PlusAPIToolset] Failed to register with MTM101 GeneratorManagement: {ex}");
            }
        }

        public static ITM_BSODA? GetOriginalBsoda() {
            if (s_originalBsoda != null) {
                return s_originalBsoda;
            }

            try {
                Type? metaStorageType = Type.GetType("MTM101BaldAPI.Registers.ItemMetaStorage, MTM101BaldAPI");
                if (metaStorageType != null) {
                    PropertyInfo? instProp = metaStorageType.GetProperty("Instance");
                    object? storage = instProp?.GetValue(null);
                    if (storage != null) {
                        MethodInfo? findMethod = metaStorageType.GetMethod("FindByEnum", new Type[] { typeof(Items) });
                        object? meta = findMethod?.Invoke(storage, new object[] { Items.Bsoda });
                        if (meta != null) {
                            PropertyInfo? valProp = meta.GetType().GetProperty("value");
                            if (valProp?.GetValue(meta) is ItemObject bsodaItem) {
                                s_originalItemObject = bsodaItem;
                                if (bsodaItem.item is ITM_BSODA bsodaComp) {
                                    s_originalBsoda = bsodaComp;
                                    return s_originalBsoda;
                                }
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning($"[PlusAPIToolset] Failed to resolve the original BSODA through MTM101: {ex}");
            }

            ItemObject[] items = Resources.FindObjectsOfTypeAll<ItemObject>();
            for (int i = 0; i < items.Length; i++) {
                ItemObject current = items[i];
                if (current != null && current.itemType == Items.Bsoda && current.item is ITM_BSODA bsoda) {
                    s_originalBsoda = bsoda;
                    s_originalItemObject = current;
                    return s_originalBsoda;
                }
            }

            ITM_BSODA[] bsodas = Resources.FindObjectsOfTypeAll<ITM_BSODA>();
            for (int i = 0; i < bsodas.Length; i++) {
                if (bsodas[i] != null) {
                    s_originalBsoda = bsodas[i];
                    return s_originalBsoda;
                }
            }

            return null;
        }

        public static ItemObject? GetOriginalItemObject() {
            if (s_originalItemObject != null) {
                return s_originalItemObject;
            }

            GetOriginalBsoda();
            if (s_originalItemObject != null) {
                return s_originalItemObject;
            }

            ItemObject[] items = Resources.FindObjectsOfTypeAll<ItemObject>();
            for (int i = 0; i < items.Length; i++) {
                ItemObject current = items[i];
                if (current != null && current.itemType == Items.Bsoda) {
                    s_originalItemObject = current;
                    return s_originalItemObject;
                }
            }

            return null;
        }

        public static GameObject? CreateSodaPrefab(Type sodaType, string prefabName, out SodaObject? sodaComponent) {
            if (!typeof(SodaObject).IsAssignableFrom(sodaType) || sodaType.IsAbstract) {
                sodaComponent = null;
                return null;
            }

            ITM_BSODA? original = GetOriginalBsoda();
            if (original == null) {
                sodaComponent = null;
                return null;
            }

            GameObject prefab = PrefabBuilder.Clone(original.gameObject, prefabName);
            ITM_BSODA oldComp = prefab.GetComponent<ITM_BSODA>();
            SodaObject newComp = (SodaObject)prefab.AddComponent(sodaType);

            CopyBsodaProperties(original, newComp);

            if (oldComp != null) {
                UnityEngine.Object.DestroyImmediate(oldComp);
            }

            EnsureDirectoriesExist(newComp.ModGuid);

            List<string> missingAssets = new List<string>();
            if (newComp.Spray == null) {
                missingAssets.Add("Spray");
            }
            if (newComp.Soda_Small == null) {
                missingAssets.Add("Soda_Small");
            }
            if (newComp.Soda_Large == null) {
                missingAssets.Add("Soda_Large");
            }
            if (newComp.CustomSound == null) {
                missingAssets.Add("CustomSound");
            }

            if (missingAssets.Count > 0) {
                Debug.LogError($"[PlusAPIToolset] Cannot register '{sodaType.Name}' as custom BSODA. Missing required assets: {string.Join(", ", missingAssets)}. Expected location: '{GetCorePath(newComp.ModGuid)}'.");
                UnityEngine.Object.DestroyImmediate(prefab);
                sodaComponent = null;
                return null;
            }

            Entity? entityComp = prefab.GetComponent<Entity>();
            if (entityComp != null) {
                entityComp.enabled = false;
            }

            newComp.SetSprite(newComp.Spray!);
            newComp.Sound = newComp.CustomSound;

            ItemObject itemObj = CreateItemObject(newComp);
            s_registeredItems[sodaType] = itemObj;

            sodaComponent = newComp;
            Debug.Log($"[PlusAPIToolset] Successfully registered '{sodaType.Name}' as custom BSODA.");
            return prefab;
        }

        public static GameObject? CreateSodaPrefab<T>(string prefabName, out T? sodaComponent) where T : SodaObject {
            GameObject? prefab = CreateSodaPrefab(typeof(T), prefabName, out SodaObject? comp);
            sodaComponent = comp as T;
            return prefab;
        }

        public static ItemObject CreateItemObject(SodaObject soda) {
            ItemObject itemObj;
            ItemObject? original = GetOriginalItemObject();
            if (original != null) {
                itemObj = UnityEngine.Object.Instantiate(original);
            } else {
                itemObj = ScriptableObject.CreateInstance<ItemObject>();
            }

            itemObj.name = "Item_" + soda.gameObject.name;
            itemObj.item = soda;
            itemObj.itemType = soda.ItemType;
            itemObj.hideFlags = HideFlags.HideAndDontSave;
            itemObj.addToInventory = true;
            itemObj.overrideDisabled = false;

            if (soda.Soda_Small != null) {
                itemObj.itemSpriteSmall = soda.Soda_Small;
            }
            if (soda.Soda_Large != null) {
                itemObj.itemSpriteLarge = soda.Soda_Large;
            }
            if (!string.IsNullOrEmpty(soda.ItemNameKey)) {
                itemObj.nameKey = soda.ItemNameKey;
            }
            if (!string.IsNullOrEmpty(soda.ItemDescKey)) {
                itemObj.descKey = soda.ItemDescKey;
            }

            itemObj.price = soda.ItemPrice;
            itemObj.value = soda.ItemValue;

            soda.AssociatedItem = itemObj;

            try {
                PlayerFileManager pfm = Singleton<PlayerFileManager>.Instance;
                if (pfm != null && pfm.itemObjects != null && !pfm.itemObjects.Contains(itemObj)) {
                    pfm.itemObjects.Add(itemObj);
                }
            } catch (Exception ex) {
                Debug.LogWarning($"[PlusAPIToolset] Failed to register the item with PlayerFileManager: {ex}");
            }

            TryRegisterItemMeta(itemObj, soda);

            Debug.Log($"[PlusAPIToolset] Registered ItemObject '{itemObj.name}' with ItemType '{itemObj.itemType}'.");
            return itemObj;
        }

        private static void TryRegisterItemMeta(ItemObject itemObj, SodaObject soda) {
            try {
                Type? metaStorageType = Type.GetType("MTM101BaldAPI.Registers.ItemMetaStorage, MTM101BaldAPI");
                Type? itemMetaType = Type.GetType("MTM101BaldAPI.Registers.ItemMetaData, MTM101BaldAPI");
                if (metaStorageType != null && itemMetaType != null) {
                    PropertyInfo? instanceProp = metaStorageType.GetProperty("Instance");
                    object? storageInstance = instanceProp?.GetValue(null);
                    if (storageInstance != null) {
                        PluginInfo? info = null;
                        if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(soda.ModGuid)) {
                            info = BepInEx.Bootstrap.Chainloader.PluginInfos[soda.ModGuid];
                        } else if (PluginInitializer.Instance != null) {
                            info = PluginInitializer.Instance.Info;
                        }

                        if (info != null) {
                            object meta = Activator.CreateInstance(itemMetaType, new object[] { info, itemObj });
                            MethodInfo? addMethod = metaStorageType.GetMethod("Add", new Type[] { itemMetaType });
                            addMethod?.Invoke(storageInstance, new object[] { meta });
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning($"[PlusAPIToolset] Failed to register MTM101 item metadata: {ex}");
            }
        }

        public static BsodaFloor GetFloorFromLevelName(string? name) {
            if (string.IsNullOrEmpty(name)) {
                return BsodaFloor.None;
            }

            string upper = name.ToUpperInvariant();
            if (upper.Contains("ENDLESS") || upper.Contains("_END") || upper.Contains("END_") || upper.StartsWith("END")) {
                return BsodaFloor.END;
            }
            if (upper.Contains("F1") || upper.Contains("LEVEL1") || upper.Contains("FLOOR1")) {
                return BsodaFloor.F1;
            }
            if (upper.Contains("F2") || upper.Contains("LEVEL2") || upper.Contains("FLOOR2")) {
                return BsodaFloor.F2;
            }
            if (upper.Contains("F3") || upper.Contains("LEVEL3") || upper.Contains("FLOOR3")) {
                return BsodaFloor.F3;
            }
            if (upper.Contains("F4") || upper.Contains("LEVEL4") || upper.Contains("FLOOR4")) {
                return BsodaFloor.F4;
            }
            if (upper.Contains("F5") || upper.Contains("LEVEL5") || upper.Contains("FLOOR5")) {
                return BsodaFloor.F5;
            }
            return BsodaFloor.None;
        }

        public static BsodaFloor GetFloorFromScene(SceneObject scene) {
            if (scene == null) {
                return BsodaFloor.None;
            }

            switch (scene.levelNo) {
                case 1:
                    return BsodaFloor.F1;
                case 2:
                    return BsodaFloor.F2;
                case 3:
                    return BsodaFloor.F3;
                case 4:
                    return BsodaFloor.F4;
                case 5:
                    return BsodaFloor.F5;
            }

            if (!string.IsNullOrEmpty(scene.levelTitle)) {
                BsodaFloor titleFloor = GetFloorFromLevelName(scene.levelTitle);
                if (titleFloor != BsodaFloor.None) {
                    return titleFloor;
                }
            }

            return GetFloorFromLevelName(scene.name);
        }

        public static void InjectIntoSceneObject(SceneObject sceneObject, string floorName = "", int floorNumber = -1) {
            if (sceneObject == null) {
                return;
            }

            ProcessPendingRegistrations();

            BsodaFloor currentFloor = GetFloorFromScene(sceneObject);
            if (!string.IsNullOrEmpty(floorName)) {
                BsodaFloor nameFloor = GetFloorFromLevelName(floorName);
                if (nameFloor != BsodaFloor.None) {
                    currentFloor = nameFloor;
                }
            }

            foreach (KeyValuePair<Type, ItemObject> pair in s_registeredItems) {
                GameObject? prefab = GetPrefab(pair.Key);
                if (prefab == null) {
                    continue;
                }

                SodaObject soda = prefab.GetComponent<SodaObject>();
                if (soda == null) {
                    continue;
                }

                bool matchesFloor = currentFloor == BsodaFloor.None || (soda.TargetFloors & currentFloor) != 0;
                if (!matchesFloor) {
                    continue;
                }

                WeightedItemObject weightedItem = new WeightedItemObject {
                    selection = pair.Value,
                    weight = soda.SpawnWeight
                };

                if (soda.AddToLevelGeneration) {
                    if (sceneObject.levelObject != null) {
                        sceneObject.levelObject.potentialItems = AppendToArray(sceneObject.levelObject.potentialItems, weightedItem);
                        sceneObject.levelObject.items = AppendToArray(sceneObject.levelObject.items, weightedItem);
                    }

                    if (sceneObject.randomizedLevelObject != null) {
                        for (int i = 0; i < sceneObject.randomizedLevelObject.Length; i++) {
                            LevelObject? rLevel = sceneObject.randomizedLevelObject[i].selection;
                            if (rLevel != null) {
                                rLevel.potentialItems = AppendToArray(rLevel.potentialItems, weightedItem);
                                rLevel.items = AppendToArray(rLevel.items, weightedItem);
                            }
                        }
                    }

                    InjectIntoCustomLevelObjects(sceneObject, weightedItem);
                }

                if (soda.AddToShopGeneration && (soda.TargetFloors & BsodaFloor.PIT) != 0) {
                    sceneObject.shopItems = AppendToArray(sceneObject.shopItems, new WeightedItemObject {
                        selection = pair.Value,
                        weight = soda.ShopWeight
                    });
                }

                Debug.Log($"[PlusAPIToolset] Injected '{pair.Value.name}' (weight: {soda.SpawnWeight}) into SceneObject '{sceneObject.name}' (floor: {currentFloor}).");
            }
        }

        public static void InjectIntoLevelParameters(LevelGenerationParameters ld, SceneObject? scene) {
            if (ld == null) {
                return;
            }

            ProcessPendingRegistrations();

            BsodaFloor currentFloor = scene != null ? GetFloorFromScene(scene) : GetFloorFromLevelName(ld.name);

            foreach (KeyValuePair<Type, ItemObject> pair in s_registeredItems) {
                GameObject? prefab = GetPrefab(pair.Key);
                if (prefab == null) {
                    continue;
                }

                SodaObject soda = prefab.GetComponent<SodaObject>();
                if (soda == null) {
                    continue;
                }

                bool matchesFloor = currentFloor == BsodaFloor.None || (soda.TargetFloors & currentFloor) != 0;
                if (!matchesFloor) {
                    continue;
                }

                if (soda.AddToLevelGeneration) {
                    WeightedItemObject weightedItem = new WeightedItemObject {
                        selection = pair.Value,
                        weight = soda.SpawnWeight
                    };
                    ld.potentialItems = AppendToArray(ld.potentialItems, weightedItem);
                    ld.items = AppendToArray(ld.items, weightedItem);
                }

                if (soda.AddToShopGeneration && (soda.TargetFloors & BsodaFloor.PIT) != 0) {
                    ld.shopItems = AppendToArray(ld.shopItems, new WeightedItemObject {
                        selection = pair.Value,
                        weight = soda.ShopWeight
                    });
                }

                Debug.Log($"[PlusAPIToolset] Injected '{pair.Value.name}' into LevelGenerationParameters (floor: {currentFloor}).");
            }
        }

        public static void InjectIntoShopScreen(StoreScreen store) {
            if (store == null) {
                return;
            }

            ProcessPendingRegistrations();

            BaseGameManager bgm = Singleton<BaseGameManager>.Instance;
            if (bgm != null && bgm.levelObject != null) {
                foreach (KeyValuePair<Type, ItemObject> pair in s_registeredItems) {
                    GameObject? prefab = GetPrefab(pair.Key);
                    if (prefab == null) {
                        continue;
                    }

                    SodaObject soda = prefab.GetComponent<SodaObject>();
                    if (soda != null && soda.AddToShopGeneration && (soda.TargetFloors & BsodaFloor.PIT) != 0) {
                        bgm.levelObject.shopItems = AppendToArray(bgm.levelObject.shopItems, new WeightedItemObject {
                            selection = pair.Value,
                            weight = soda.ShopWeight
                        });
                        Debug.Log($"[PlusAPIToolset] Injected '{pair.Value.name}' into active Johnny's Store.");
                    }
                }
            }
        }

        private static void InjectIntoCustomLevelObjects(SceneObject sceneObject, WeightedItemObject item) {
            if (!s_customLevelObjectsChecked) {
                s_customLevelObjectsChecked = true;
                try {
                    Type? extType = Type.GetType("MTM101BaldAPI.CustomLevelObjectExtensions, MTM101BaldAPI");
                    if (extType != null) {
                        s_getCustomLevelObjectsMethod = extType.GetMethod("GetCustomLevelObjects", new Type[] { typeof(SceneObject) });
                    }
                } catch (Exception ex) {
                    Debug.LogWarning($"[PlusAPIToolset] Failed to find MTM101 custom level integration: {ex}");
                }
            }

            if (s_getCustomLevelObjectsMethod != null) {
                try {
                    Array? clos = s_getCustomLevelObjectsMethod.Invoke(null, new object[] { sceneObject }) as Array;
                    if (clos != null) {
                        for (int i = 0; i < clos.Length; i++) {
                            object? clo = clos.GetValue(i);
                            if (clo is LevelObject lo) {
                                lo.potentialItems = AppendToArray(lo.potentialItems, item);
                                lo.items = AppendToArray(lo.items, item);
                            }
                        }
                    }
                } catch (Exception ex) {
                    Debug.LogWarning($"[PlusAPIToolset] Failed to inject into MTM101 custom levels: {ex}");
                }
            }
        }

        public static void AddItemToLevelGeneration(ItemObject item, BsodaFloor targetFloors = BsodaFloor.All, int weight = 50, bool addToHallways = true, bool addToShop = true) {
            WeightedItemObject weightedItem = new WeightedItemObject {
                selection = item,
                weight = weight
            };

            SceneObject[] scenes = Resources.FindObjectsOfTypeAll<SceneObject>();
            for (int i = 0; i < scenes.Length; i++) {
                SceneObject scene = scenes[i];
                if (scene != null) {
                    InjectIntoSceneObject(scene);
                }
            }

            LevelObject[] levels = Resources.FindObjectsOfTypeAll<LevelObject>();
            for (int i = 0; i < levels.Length; i++) {
                LevelObject level = levels[i];
                if (level == null) {
                    continue;
                }

                BsodaFloor floor = GetFloorFromLevelName(level.name);
                bool matchesFloor = floor == BsodaFloor.None || (floor & targetFloors) != 0;

                if (addToHallways && matchesFloor) {
                    if (level.items != null) {
                        level.items = AppendToArray(level.items, weightedItem);
                    }
                    if (level.potentialItems != null) {
                        level.potentialItems = AppendToArray(level.potentialItems, weightedItem);
                    }
                }

                if (addToShop && (targetFloors & BsodaFloor.PIT) != 0 && level.shopItems != null) {
                    level.shopItems = AppendToArray(level.shopItems, weightedItem);
                }
            }
        }

        public static void ProcessPendingRegistrations() {
            if (GetOriginalBsoda() == null) {
                return;
            }

            for (int i = s_pendingTypes.Count - 1; i >= 0; i--) {
                Type type = s_pendingTypes[i];
                if (s_registeredPrefabs.ContainsKey(type)) {
                    s_pendingTypes.RemoveAt(i);
                    continue;
                }

                SodaAttribute? attr = type.GetCustomAttribute<SodaAttribute>();
                string prefabName = attr != null ? attr.Name : type.Name;

                GameObject? prefab = CreateSodaPrefab(type, prefabName, out _);
                if (prefab != null) {
                    s_registeredPrefabs[type] = prefab;
                }
                s_pendingTypes.RemoveAt(i);
            }
        }

        public static void ApplyGenerationToLoadedScenes() {
            ProcessPendingRegistrations();

            SceneObject[] scenes = Resources.FindObjectsOfTypeAll<SceneObject>();
            for (int i = 0; i < scenes.Length; i++) {
                if (scenes[i] != null) {
                    InjectIntoSceneObject(scenes[i]);
                }
            }
        }

        private static WeightedItemObject[] AppendToArray(WeightedItemObject[] original, WeightedItemObject newItem) {
            if (original == null) {
                return new WeightedItemObject[] { newItem };
            }

            for (int i = 0; i < original.Length; i++) {
                if (original[i] != null && original[i].selection == newItem.selection) {
                    return original;
                }
            }

            WeightedItemObject[] updated = new WeightedItemObject[original.Length + 1];
            Array.Copy(original, updated, original.Length);
            updated[original.Length] = newItem;
            return updated;
        }

        public static void AutoRegisterAll(Assembly? assembly = null) {
            Assembly targetAssembly = assembly ?? Assembly.GetCallingAssembly();
            Type[] types = targetAssembly.GetTypes();

            for (int i = 0; i < types.Length; i++) {
                Type type = types[i];
                if (type.IsAbstract || !typeof(SodaObject).IsAssignableFrom(type)) {
                    continue;
                }

                BepInPlugin? pluginAttr = type.Assembly.GetCustomAttribute<BepInPlugin>();
                string guid = pluginAttr != null ? pluginAttr.GUID : PlusModInfo.GUID;
                EnsureDirectoriesExist(guid);

                if (!s_pendingTypes.Contains(type)) {
                    s_pendingTypes.Add(type);
                }
            }

            ProcessPendingRegistrations();
        }

        public static GameObject? GetPrefab<T>() where T : SodaObject {
            return GetPrefab(typeof(T));
        }

        public static GameObject? GetPrefab(Type type) {
            if (s_registeredPrefabs.TryGetValue(type, out GameObject? prefab)) {
                return prefab;
            }
            return null;
        }

        public static ItemObject? GetItem<T>() where T : SodaObject {
            return GetItem(typeof(T));
        }

        public static ItemObject? GetItem(Type type) {
            if (s_registeredItems.TryGetValue(type, out ItemObject? item)) {
                return item;
            }
            return null;
        }

        public static T? Spawn<T>(Vector3 position = default, Quaternion rotation = default, Transform? parent = null) where T : SodaObject {
            GameObject? prefab = GetPrefab<T>();
            if (prefab == null) {
                return null;
            }
            return PrefabBuilder.Spawn<T>(prefab, position, rotation, parent);
        }

        public static void CopyBsodaProperties(ITM_BSODA source, SodaObject destination) {
            destination.SpriteRenderer = destination.GetComponentInChildren<SpriteRenderer>(true);
            destination.Entity = destination.GetComponent<Entity>();

            Type sourceType = typeof(ITM_BSODA);
            FieldInfo? moveModField = sourceType.GetField("moveMod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (moveModField != null && moveModField.GetValue(source) is MovementModifier origMod) {
                destination.MovementModifier = new MovementModifier(origMod.movementAddend, origMod.movementMultiplier, origMod.priority);
            } else {
                destination.MovementModifier = new MovementModifier(Vector3.zero, 1f, 1);
            }

            FieldInfo? soundField = sourceType.GetField("sound", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo? speedField = sourceType.GetField("speed", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo? timeField = sourceType.GetField("time", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (soundField != null) {
                destination.Sound = soundField.GetValue(source) as SoundObject;
            }
            if (speedField != null) {
                destination.Speed = (float)speedField.GetValue(source);
            }
            if (timeField != null) {
                destination.TimeRemaining = (float)timeField.GetValue(source);
            }
        }

        public static Sprite? TryResolveSprite(Type type, string suffix, string guid, float pixelsPerUnit = 25f) {
            string cacheKey = $"{guid}|{type.AssemblyQualifiedName}|{suffix}|{pixelsPerUnit.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            if (s_spriteCache.TryGetValue(cacheKey, out Sprite cached)) {
                return cached;
            }

            string targetFileName = $"{type.Name}_{suffix}.png";

            string imagesDir = GetImagesPath(guid);
            string primaryPath = Path.Combine(imagesDir, targetFileName);
            if (File.Exists(primaryPath)) {
                Sprite? sprite = CreateSpriteFromFile(primaryPath, pixelsPerUnit);
                if (sprite != null) {
                    s_spriteCache[cacheKey] = sprite;
                    return sprite;
                }
            }

            Assembly assembly = type.Assembly;
            string[] resourceNames = assembly.GetManifestResourceNames();
            for (int i = 0; i < resourceNames.Length; i++) {
                if (resourceNames[i].EndsWith(targetFileName, StringComparison.OrdinalIgnoreCase)) {
                    using (Stream? stream = assembly.GetManifestResourceStream(resourceNames[i])) {
                        if (stream != null) {
                            using MemoryStream buffer = new MemoryStream();
                            stream.CopyTo(buffer);
                            Sprite sprite = CreateSpriteFromBytes(buffer.ToArray(), pixelsPerUnit);
                            s_spriteCache[cacheKey] = sprite;
                            return sprite;
                        }
                    }
                }
            }

            string? assemblyLocation = assembly.Location;
            if (!string.IsNullOrEmpty(assemblyLocation)) {
                string? assemblyDir = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDir)) {
                    string[] searchPaths = new string[] {
                        Path.Combine(assemblyDir, targetFileName),
                        Path.Combine(assemblyDir, "Textures", targetFileName),
                        Path.Combine(assemblyDir, "Assets", targetFileName),
                        Path.Combine(assemblyDir, "Sprites", targetFileName),
                        Path.Combine(assemblyDir, "Resources", targetFileName)
                    };

                    for (int i = 0; i < searchPaths.Length; i++) {
                        if (File.Exists(searchPaths[i])) {
                            Sprite? sprite = CreateSpriteFromFile(searchPaths[i], pixelsPerUnit);
                            if (sprite != null) {
                                s_spriteCache[cacheKey] = sprite;
                                return sprite;
                            }
                        }
                    }
                }
            }

            return null;
        }

        public static SoundObject? TryResolveSound(Type type, string guid) {
            string cacheKey = $"{guid}|{type.AssemblyQualifiedName}|Sound";
            if (s_soundCache.TryGetValue(cacheKey, out SoundObject cached)) {
                return cached;
            }

            string soundsDir = GetSoundsPath(guid);
            string[] candidateFiles = new string[] {
                $"{type.Name}.wav",
                $"{type.Name}.ogg",
                $"{type.Name}.mp3",
                $"{type.Name}_Sound.wav",
                $"{type.Name}_Sound.ogg",
                $"{type.Name}_Sound.mp3"
            };

            for (int i = 0; i < candidateFiles.Length; i++) {
                string soundPath = Path.Combine(soundsDir, candidateFiles[i]);
                if (File.Exists(soundPath)) {
                    AudioClip? clip = LoadAudio(soundPath);
                    if (clip != null) {
                        SoundObject soundObj = CreateSoundObject(clip, type.Name, SoundType.Effect);
                        s_soundCache[cacheKey] = soundObj;
                        return soundObj;
                    }
                }
            }

            return null;
        }

        public static SoundObject CreateSoundObject(AudioClip clip, string soundKey = "", SoundType type = SoundType.Effect, Color color = default) {
            SoundObject sound = ScriptableObject.CreateInstance<SoundObject>();
            sound.soundClip = clip;
            sound.soundType = type;
            sound.soundKey = soundKey;
            sound.color = color == default ? Color.white : color;
            sound.volumeMultiplier = 1f;
            sound.hideFlags = HideFlags.HideAndDontSave;
            return sound;
        }

        public static AudioClip? LoadAudio(string filePath) {
            if (!File.Exists(filePath)) {
                return null;
            }

            if (!s_assetLoaderChecked) {
                s_assetLoaderChecked = true;
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++) {
                    if (assemblies[i].GetName().Name == "MTM101BaldAPI") {
                        Type? assetLoaderType = assemblies[i].GetType("MTM101BaldAPI.AssetTools.AssetLoader");
                        if (assetLoaderType != null) {
                            s_assetLoaderAudioClipFromFile = assetLoaderType.GetMethod("AudioClipFromFile", new Type[] { typeof(string) });
                        }
                        break;
                    }
                }
            }

            if (s_assetLoaderAudioClipFromFile != null) {
                try {
                    AudioClip? clip = s_assetLoaderAudioClipFromFile.Invoke(null, new object[] { filePath }) as AudioClip;
                    if (clip != null && clip.loadState != AudioDataLoadState.Failed && clip.samples > 0 && clip.length > 0f) {
                        return clip;
                    }
                } catch (Exception ex) {
                    Debug.LogWarning($"[PlusAPIToolset] Failed to load audio through MTM101: {ex}");
                }
            }

            try {
                string fullPath = Path.GetFullPath(filePath);
                string[] fallbacks = new string[] { "file:///", "file://", "", "File:///", "File://" };
                AudioType audioType = AudioType.UNKNOWN;
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".wav") {
                    audioType = AudioType.WAV;
                } else if (ext == ".ogg") {
                    audioType = AudioType.OGGVORBIS;
                } else if (ext == ".mp3") {
                    audioType = AudioType.MPEG;
                } else if (ext == ".aif" || ext == ".aiff") {
                    audioType = AudioType.AIFF;
                }

                for (int i = 0; i < fallbacks.Length; i++) {
                    using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(fallbacks[i] + fullPath, audioType)) {
                        req.SendWebRequest();
                        while (!req.isDone) {
                        }
                        if (req.result == UnityWebRequest.Result.Success) {
                            AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                            if (clip != null) {
                                if (clip.loadState == AudioDataLoadState.Failed || clip.samples <= 0 || clip.length <= 0f) {
                                    UnityEngine.Object.DestroyImmediate(clip);
                                    continue;
                                }
                                clip.name = Path.GetFileNameWithoutExtension(filePath);
                                return clip;
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.LogWarning($"[PlusAPIToolset] Failed to load audio through UnityWebRequest: {ex}");
            }

            return LoadPcmWav(filePath);
        }

        public static AudioClip? LoadWav(string filePath) {
            return LoadAudio(filePath);
        }

        public static AudioClip? LoadPcmWav(string filePath) {
            if (!File.Exists(filePath)) {
                return null;
            }

            try {
                byte[] fileBytes = File.ReadAllBytes(filePath);
                if (fileBytes.Length < 44) {
                    return null;
                }

                string riff = System.Text.Encoding.ASCII.GetString(fileBytes, 0, 4);
                string wave = System.Text.Encoding.ASCII.GetString(fileBytes, 8, 4);
                if (riff != "RIFF" || wave != "WAVE") {
                    return null;
                }

                int pos = 12;
                int channels = 0;
                int sampleRate = 0;
                int bitsPerSample = 0;
                int audioFormat = 0;
                int dataPos = -1;
                int dataSize = 0;

                while (pos + 8 <= fileBytes.Length) {
                    string chunkId = System.Text.Encoding.ASCII.GetString(fileBytes, pos, 4);
                    int chunkSize = BitConverter.ToInt32(fileBytes, pos + 4);
                    pos += 8;

                    if (chunkSize < 0 || pos + chunkSize > fileBytes.Length) {
                        break;
                    }

                    if (chunkId == "fmt " && chunkSize >= 16) {
                        audioFormat = BitConverter.ToInt16(fileBytes, pos);
                        channels = BitConverter.ToInt16(fileBytes, pos + 2);
                        sampleRate = BitConverter.ToInt32(fileBytes, pos + 4);
                        bitsPerSample = BitConverter.ToInt16(fileBytes, pos + 14);
                    } else if (chunkId == "data") {
                        dataPos = pos;
                        dataSize = chunkSize;
                        break;
                    }

                    pos += chunkSize;
                }

                if (channels <= 0 || sampleRate <= 0 || bitsPerSample <= 0 || dataPos < 0 || dataSize <= 0) {
                    return null;
                }

                if (audioFormat != 1) {
                    return null;
                }

                int bytesPerSample = bitsPerSample / 8;
                if (bytesPerSample <= 0) {
                    return null;
                }

                int totalSamples = dataSize / bytesPerSample;
                if (totalSamples <= 0 || totalSamples < channels) {
                    return null;
                }

                float[] audioData = new float[totalSamples];
                if (bitsPerSample == 16) {
                    for (int i = 0; i < totalSamples; i++) {
                        int sampleIndex = dataPos + i * 2;
                        if (sampleIndex + 1 < fileBytes.Length) {
                            short sample = BitConverter.ToInt16(fileBytes, sampleIndex);
                            audioData[i] = sample / 32768f;
                        }
                    }
                } else if (bitsPerSample == 8) {
                    for (int i = 0; i < totalSamples; i++) {
                        int sampleIndex = dataPos + i;
                        if (sampleIndex < fileBytes.Length) {
                            byte sample = fileBytes[sampleIndex];
                            audioData[i] = (sample - 128) / 128f;
                        }
                    }
                } else {
                    return null;
                }

                int sampleCountPerChannel = totalSamples / channels;
                if (sampleCountPerChannel <= 0) {
                    return null;
                }

                AudioClip clip = AudioClip.Create(Path.GetFileNameWithoutExtension(filePath), sampleCountPerChannel, channels, sampleRate, false);
                clip.SetData(audioData, 0);
                return clip;
            } catch (Exception ex) {
                Debug.LogWarning($"[PlusAPIToolset] Failed to decode PCM WAV '{filePath}': {ex}");
                return null;
            }
        }

        public static Sprite CreateSprite(Texture2D texture, float pixelsPerUnit = 1f) {
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        public static Sprite CreateSpriteFromBytes(byte[] data, float pixelsPerUnit = 25f) {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(data);
            texture.filterMode = FilterMode.Point;
            return CreateSprite(texture, pixelsPerUnit);
        }

        public static Sprite? CreateSpriteFromFile(string filePath, float pixelsPerUnit = 25f) {
            if (!File.Exists(filePath)) {
                return null;
            }

            byte[] data = File.ReadAllBytes(filePath);
            return CreateSpriteFromBytes(data, pixelsPerUnit);
        }
    }
}
