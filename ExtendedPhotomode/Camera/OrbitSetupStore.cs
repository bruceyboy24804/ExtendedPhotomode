namespace ExtendedPhotomode.Camera {
    #region Using Statements

    using System;
    using System.Collections.Generic;
    using System.IO;

    using ModsCommon.Utils;

    using Newtonsoft.Json;

    using UnityEngine;

    #endregion

    /// <summary>Persists <see cref="OrbitSetup"/> records keyed by cinematic asset guid.</summary>
    /// <remarks>
    /// A sidecar rather than an extension of the asset itself: <c>CinematicCameraAsset</c> is a
    /// sealed vanilla type saved through the asset database, and there is no supported way to attach
    /// extra data to it. Keying by guid means a shot saved to the cloud still finds its setup
    /// locally, and an orphaned entry costs a few dozen bytes rather than breaking a load.
    /// </remarks>
    public class OrbitSetupStore {
        private const string kFolder = "ModsData";

        private const string kFileName = "ExtendedPhotomode.orbits.json";

        private readonly PrefixedLogger m_Log;
        private readonly string         m_Path;

        private Dictionary<string, OrbitSetup> m_Setups = new Dictionary<string, OrbitSetup>();

        public OrbitSetupStore(PrefixedLogger log) {
            m_Log = log;

            m_Path = Path.Combine(Application.persistentDataPath, kFolder, kFileName);
            Load();
        }

        public bool TryGet(string guid, out OrbitSetup setup) {
            setup = null;
            return !string.IsNullOrEmpty(guid) && m_Setups.TryGetValue(guid, out setup);
        }

        public void Put(string guid, OrbitSetup setup) {
            if (string.IsNullOrEmpty(guid) || setup == null) {
                return;
            }

            m_Setups[guid] = setup;
            Save();
        }

        private void Load() {
            try {
                if (!File.Exists(m_Path)) {
                    return;
                }

                string json = File.ReadAllText(m_Path);
                m_Setups = JsonConvert.DeserializeObject<Dictionary<string, OrbitSetup>>(json)
                           ?? new Dictionary<string, OrbitSetup>();

                m_Log.Debug($"Loaded {m_Setups.Count} orbit setups from {m_Path}");
            } catch (Exception e) {
                m_Log.Warn($"Could not read orbit setups from {m_Path}: {e.Message}");
                m_Setups = new Dictionary<string, OrbitSetup>();
            }
        }

        private void Save() {
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(m_Path));
                File.WriteAllText(m_Path, JsonConvert.SerializeObject(m_Setups, Formatting.Indented));
            } catch (Exception e) {
                m_Log.Warn($"Could not write orbit setups to {m_Path}: {e.Message}");
            }
        }
    }
}
