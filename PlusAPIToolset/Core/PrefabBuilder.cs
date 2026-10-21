using System;
using UnityEngine;

namespace PlusAPIToolset.Core {
    public static class PrefabBuilder {
        public static GameObject Create(string name) {
            GameObject template = new GameObject(name);
            template.SetActive(false);
            template.hideFlags = HideFlags.HideAndDontSave;
            return template;
        }

        public static GameObject Create<T>(string name, out T component) where T : Component {
            GameObject template = Create(name);
            component = template.AddComponent<T>();
            return template;
        }

        public static GameObject Clone(GameObject source, string? newName = null) {
            GameObject clone = UnityEngine.Object.Instantiate(source);
            if (!string.IsNullOrEmpty(newName)) {
                clone.name = newName;
            }
            clone.SetActive(false);
            clone.hideFlags = HideFlags.HideAndDontSave;
            Entity? entity = clone.GetComponent<Entity>();
            if (entity != null) {
                entity.enabled = false;
            }
            return clone;
        }

        public static T Clone<T>(this T sourceComponent, string? newName = null) where T : Component {
            GameObject clone = Clone(sourceComponent.gameObject, newName);
            return clone.GetComponent<T>();
        }

        public static GameObject? Spawn(GameObject? template, Vector3 position = default, Quaternion rotation = default, Transform? parent = null) {
            if (template == null) {
                return null;
            }

            GameObject instance = UnityEngine.Object.Instantiate(template, position, rotation, parent);
            instance.hideFlags = HideFlags.None;
            instance.SetActive(true);
            return instance;
        }

        public static T? Spawn<T>(GameObject? template, Vector3 position = default, Quaternion rotation = default, Transform? parent = null) where T : Component {
            GameObject? instance = Spawn(template, position, rotation, parent);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        public static T? Spawn<T>(T? templateComponent, Vector3 position = default, Quaternion rotation = default, Transform? parent = null) where T : Component {
            if (templateComponent == null) {
                return null;
            }
            return Spawn<T>(templateComponent.gameObject, position, rotation, parent);
        }
    }
}
