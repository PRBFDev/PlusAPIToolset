using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlusAPIToolset.Core {
    public static class ObjectToolkit {
        private static readonly Dictionary<Type, object> m_instances = new Dictionary<Type, object>();

        public static void RegisterInstance<T>(T instance) where T : class {
            if (instance != null) {
                m_instances[typeof(T)] = instance;
            }
        }

        public static T? TryGetInstance<T>() where T : MonoBehaviour {
            if (m_instances.TryGetValue(typeof(T), out object instance)) {
                T cached = (T)instance;
                if (cached != null) {
                    return cached;
                }
                m_instances.Remove(typeof(T));
            }

            T found = UnityEngine.Object.FindObjectOfType<T>();
            if (found != null) {
                m_instances[typeof(T)] = found;
                return found;
            }

            return null;
        }

        public static T GetOrCreateInstance<T>(string gameObjectName = "PlusAPIToolset_Manager") where T : MonoBehaviour {
            T? instance = TryGetInstance<T>();
            if (instance != null) {
                return instance;
            }

            GameObject obj = new GameObject(gameObjectName);
            obj.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(obj);
            instance = obj.AddComponent<T>();
            RegisterInstance(instance);
            return instance;
        }
    }
}
