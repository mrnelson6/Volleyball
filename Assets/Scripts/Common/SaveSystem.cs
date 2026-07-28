using System;
using System.IO;
using UnityEngine;

namespace Volleyball
{
    /// <summary>
    /// Reads and writes the campaign save file as JSON under
    /// <see cref="Application.persistentDataPath"/> (a per-user, per-platform writable location
    /// that also works in WebGL via the browser's IndexedDB-backed virtual filesystem).
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "campaign.json";

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>True if a campaign save file exists on disk.</summary>
        public static bool Exists() => File.Exists(Path);

        /// <summary>Load the campaign save (migrated to the current version), or null if none
        /// exists / it can't be parsed.</summary>
        public static CampaignSave Load()
        {
            if (!Exists()) return null;
            try
            {
                string json = File.ReadAllText(Path);
                var data = JsonUtility.FromJson<CampaignSave>(json);
                return Migrate(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Volleyball] Failed to read save '{Path}': {e.Message}");
                return null;
            }
        }

        /// <summary>Bring an older save up to the current schema in memory (persisted on the
        /// next Save call). v1 predates the world tour — its `stage` was never advanced by any
        /// build, so starting the tour from the first region loses nothing.</summary>
        static CampaignSave Migrate(CampaignSave data)
        {
            if (data == null) return null;
            if (data.saveVersion < 2)
            {
                data.regionIndex = 0;
                data.matchIndex = 0;
                data.attemptsThisMatch = 0;
                data.tourComplete = false;
                data.saveVersion = 2;
            }
            return data;
        }

        /// <summary>Write the campaign save to disk, stamping the last-played time.</summary>
        public static void Save(CampaignSave data)
        {
            if (data == null) return;
            data.lastPlayedIso = NowIso();
            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Volleyball] Failed to write save '{Path}': {e.Message}");
            }
        }

        /// <summary>Create, persist, and return a fresh campaign save (overwrites any existing one).</summary>
        public static CampaignSave NewGame()
        {
            var data = new CampaignSave { createdIso = NowIso() };
            Save(data);
            return data;
        }

        /// <summary>Delete the campaign save file if present.</summary>
        public static void Delete()
        {
            if (Exists()) File.Delete(Path);
        }

        static string NowIso() => DateTime.UtcNow.ToString("o");
    }
}
