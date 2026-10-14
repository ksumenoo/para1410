using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Echo.Bootstrap
{
    // Ставит сетевые пакеты один раз при загрузке редактора, если их ещё нет в манифесте.
    [InitializeOnLoad]
    public static class EchoPackageInstaller
    {
        static readonly string[] Needed =
        {
            "com.unity.netcode.gameobjects",
            "com.unity.services.multiplayer",
        };

        // Vivox тяжёлый (сотни МБ), ставим отдельно, чтобы не держать остальное.
        static readonly string[] Voice = { "com.unity.services.vivox" };

        const string SessionKey = "Echo.PackagesRequested.v2";
        static AddAndRemoveRequest _request;

        static EchoPackageInstaller()
        {
            EditorApplication.delayCall += TryInstall;
        }

        [MenuItem("Echo/Установить сетевые пакеты")]
        public static void InstallFromMenu()
        {
            SessionState.SetBool(SessionKey, false);
            TryInstall();
        }

        [MenuItem("Echo/Установить Vivox (голосовая рация)")]
        public static void InstallVoice() => Install(Voice);

        static void TryInstall()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            Install(Needed);
        }

        static void Install(string[] ids)
        {
            if (_request != null && !_request.IsCompleted) { Debug.Log("[Echo] Установка уже идёт."); return; }
            var manifest = File.ReadAllText("Packages/manifest.json");
            var missing = ids.Where(id => !manifest.Contains("\"" + id + "\"")).ToArray();
            if (missing.Length == 0) return;

            Debug.Log("[Echo] Устанавливаю пакеты: " + string.Join(", ", missing));
            _request = Client.AddAndRemove(missing, null);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null || !_request.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (_request.Status == StatusCode.Success)
            {
                var installed = _request.Result
                    .Where(p => Needed.Contains(p.name) || Voice.Contains(p.name))
                    .Select(p => p.name + "@" + p.version);
                Debug.Log("[Echo] Пакеты установлены: " + string.Join(", ", installed));
            }
            else
            {
                Debug.LogError("[Echo] Не удалось установить пакеты: " + _request.Error?.message);
            }
        }
    }
}
