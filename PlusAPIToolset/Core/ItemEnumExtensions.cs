using System;
using System.Collections.Generic;
using System.Reflection;

namespace PlusAPIToolset.Core {
    public static class ItemEnumExtensions {
        private static int s_currentId = 1000;
        private static readonly Dictionary<string, Items> s_nameToEnum = new Dictionary<string, Items>();
        private static readonly Dictionary<Items, string> s_enumToName = new Dictionary<Items, string>();

        public static Items Register(string name) {
            if (s_nameToEnum.TryGetValue(name, out Items existing)) {
                return existing;
            }

            try {
                Type? enumExtType = Type.GetType("MTM101BaldAPI.EnumExtensions, MTM101BaldAPI");
                if (enumExtType != null) {
                    MethodInfo? existsMethod = enumExtType.GetMethod("EnumWithExtendedNameExists")?.MakeGenericMethod(typeof(Items));
                    MethodInfo? getMethod = enumExtType.GetMethod("GetFromExtendedName")?.MakeGenericMethod(typeof(Items));
                    MethodInfo? extendMethod = enumExtType.GetMethod("ExtendEnum")?.MakeGenericMethod(typeof(Items));

                    if (existsMethod != null && getMethod != null && extendMethod != null) {
                        bool exists = (bool)existsMethod.Invoke(null, new object[] { name });
                        Items item = exists ? (Items)getMethod.Invoke(null, new object[] { name }) : (Items)extendMethod.Invoke(null, new object[] { name });
                        s_nameToEnum[name] = item;
                        s_enumToName[item] = name;
                        return item;
                    }
                }
            } catch (Exception ex) {
                UnityEngine.Debug.LogWarning($"[PlusAPIToolset] MTM101 enum registration failed for '{name}': {ex}");
            }

            Items newItem = (Items)s_currentId++;
            s_nameToEnum[name] = newItem;
            s_enumToName[newItem] = name;
            return newItem;
        }

        public static string? GetName(Items item) {
            return s_enumToName.TryGetValue(item, out string? name) ? name : null;
        }
    }
}
