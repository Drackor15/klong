using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples {
    public class PrefabPool : MonoBehaviour {
        // singleton for easier access from other scripts
        public static PrefabPool singleton;

        [Header("Settings")]
        [SerializeField]
        private List<GameObject> prefabs;

        [Header("Debug")]
        private Dictionary<string, Pool<GameObject>> pools = new Dictionary<string, Pool<GameObject>>(); // Prefab names must be unique for this to work properly
        private Dictionary<string, GameObject> prefabLookup = new Dictionary<string, GameObject>();

        public GameObject GetPooledPrefab(string prefabName) {
            return prefabLookup.GetValueOrDefault(prefabName);
        }

        void Start() {
            singleton = this;

            foreach (var prefab in prefabs) {
                InitializePool(prefab);
                NetworkClient.RegisterPrefab(prefab, msg => SpawnHandler(prefab, msg), spawned => UnspawnHandler(prefab, spawned));
            }
        }

        void OnDestroy() {
            foreach (var prefab in prefabs) {
                NetworkClient.UnregisterPrefab(prefab);
            }
        }

        void InitializePool(GameObject prefab) {
            if (!pools.ContainsKey(prefab.name)) {
                pools[prefab.name] = new Pool<GameObject>(() => CreateNew(prefab), 6);
                prefabLookup[prefab.name] = prefab;
            }
        }

        GameObject CreateNew(GameObject prefab) {
            GameObject next = Instantiate(prefab, transform);
            next.name = $"{prefab.name}_pooled";
            next.SetActive(false);
            return next;
        }

        // Wrapper Used by NetworkClient.RegisterPrefab
        GameObject SpawnHandler(GameObject prefab, SpawnMessage msg) {
            return Get(prefab, msg.position, msg.rotation);
        }

        // Wrapper Used by NetworkClient.RegisterPrefab
        void UnspawnHandler(GameObject prefab, GameObject spawned) {
            Return(prefab, spawned);
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, uint netID = 0) {
            if (pools.TryGetValue(prefab.name, out var pool)) {
                // Attempt to find an object with a matching playerOwnerNetID
                if (netID != 0) {
                    List<GameObject> tempList = new List<GameObject>();

                    for (uint i = 0; i < pool.Count; i++) {
                        var obj = pool.Get();

                        var playerBall = obj.GetComponent<PlayerBall>();
                        if (playerBall != null && playerBall.playerOwnerNetID == netID) {
                            obj.transform.position = position;
                            obj.transform.rotation = rotation;
                            obj.SetActive(true);

                            // Return the other objects to the pool
                            foreach (var returned in tempList) {
                                pool.Return(returned);
                            }

                            return obj;
                        }

                        // Temporarily store the object to return it later
                        tempList.Add(obj);
                    }

                    // Return all temporarily retrieved objects to the pool
                    foreach (var obj in tempList) {
                        pool.Return(obj);
                    }
                }

                // No matching object found, fall back to getting the next available one
                GameObject next = pool.Get();
                next.transform.position = position;
                next.transform.rotation = rotation;
                next.SetActive(true);
                return next;
            }
            else {
                Debug.LogError($"Prefab {prefab.name} does not have a pool initialized.");
                return null;
            }
        }


        public void Return(GameObject prefab, GameObject spawned) {
            if (pools.TryGetValue(prefab.name, out var pool)) {
                spawned.SetActive(false);
                pool.Return(spawned);
            }
            else {
                Debug.LogError($"Prefab {prefab.name} does not have a pool initialized.");
            }
        }

        public void AddPrefab(GameObject newPrefab, int initialPoolSize = 6) {
            if (!pools.ContainsKey(newPrefab.name)) {
                prefabs.Add(newPrefab);
                InitializePool(newPrefab);
                NetworkClient.RegisterPrefab(newPrefab, msg => SpawnHandler(newPrefab, msg), spawned => UnspawnHandler(newPrefab, spawned));
            }
        }
    }
}