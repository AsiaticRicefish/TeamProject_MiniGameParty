using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace LDH_Util
{
    public static class JsonStore
    {
        private static string GetPath(string fileName)
            => Path.Combine(Application.persistentDataPath, fileName);

        public static async Task SaveAsync<T>(string fileName, T data)
        {
            var path     = GetPath(fileName);
            var dir      = Path.GetDirectoryName(path);
            var tmpPath  = path + ".tmp";
            var bakPath  = path + ".bak";
            
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            
            // 1) temp에 먼저 씀
            await File.WriteAllTextAsync(tmpPath, json);

            // 2) 기존 파일 백업
            if (File.Exists(path))
            {
                try
                {
                    if (File.Exists(bakPath)) File.Delete(bakPath);
                    File.Move(path, bakPath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LocalJsonStore] Backup failed: {e.Message}");
                }
            }
            
            // 3) temp → 정식 파일로 교체
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmpPath, path);

        }

        public static bool TryLoad<T>(string fileName, out T data)
        {
            var path = GetPath(fileName);
            if (!File.Exists(path))
            {
                data = default;
                return false;
            }

            try
            {
                var json = File.ReadAllText(path);
                data = JsonConvert.DeserializeObject<T>(json);
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalJsonStore] Load failed: {e.Message}");
                data = default;
                return false;
            }
        }
        
        public static void Delete(string fileName)
        {
            var path = GetPath(fileName);
            if (File.Exists(path)) File.Delete(path);
            var bak = path + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
        }
    }
}