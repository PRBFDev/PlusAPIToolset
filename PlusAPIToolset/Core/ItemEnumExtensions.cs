using System.Collections.Generic;

namespace PlusAPIToolset.Core {
    public static class ItemEnumExtensions {
        private static int s_currentId = 1000;
        private static readonly Dictionary<string, Items> s_nameToEnum = new Dictionary<string, Items>();
        private static readonly Dictionary<Items, string> s_enumToName = new Dictionary<Items, string>();

        public static Items Register(string name) {
            if (s_nameToEnum.TryGetValue(name, out Items existing)) {
                return existing;
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
