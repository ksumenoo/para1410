using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Echo.EditorTools
{
    // Пульт для отладки снаружи редактора: файл _incoming/cmd/command.txt с командами по строке.
    // build — собрать сцену; rig — пересобрать героя художника; play — Play + «Один ПК»;
    // stop — выйти из Play; shot — скриншот в _incoming/cmd/shot.png; refresh — обновить ассеты.
    // Результат пишется в _incoming/cmd/result.txt.
    [InitializeOnLoad]
    public static class EchoRemote
    {
        const string Dir = "_incoming/cmd";
        const string CommandFile = Dir + "/command.txt";
        const string ResultFile = Dir + "/result.txt";
        static double _next;

        static EchoRemote()
        {
            // Несколько чатов пишут код параллельно: не перекомпилировать во время Play,
            // иначе сеть сбрасывается посреди теста. 1 = Recompile After Finished Playing.
            EditorPrefs.SetInt("ScriptCompilationDuringPlay", 1);
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 0.5;
            if (!File.Exists(CommandFile)) return;
            // Во время Play компиляция откладывается до выхода — команды всё равно выполняем.
            if (!EditorApplication.isPlaying && (EditorApplication.isCompiling || EditorApplication.isUpdating)) return;

            string[] commands;
            try
            {
                commands = File.ReadAllLines(CommandFile);
                File.Delete(CommandFile);
            }
            catch (IOException) { return; }

            var log = new StringBuilder();
            foreach (var raw in commands)
            {
                var cmd = raw.Trim().ToLowerInvariant();
                if (cmd.Length == 0) continue;
                try
                {
                    Run(cmd, log);
                }
                catch (Exception e)
                {
                    log.AppendLine(cmd + ": ОШИБКА " + e);
                    Debug.LogException(e);
                }
            }
            Directory.CreateDirectory(Dir);
            File.WriteAllText(ResultFile, DateTime.Now.ToString("HH:mm:ss") + "\n" + log);
        }

        static void Run(string cmd, StringBuilder log)
        {
            var parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts[0] == "tp" && parts.Length == 4)
            {
                // tp <герой> <x> <z> — локальные координаты на текущей линии героя
                var h = Echo.Hero.Get(int.Parse(parts[1]));
                if (h == null) { log.AppendLine("tp: нет героя"); return; }
                float x = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                float z = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
                h.TeleportRpc(Echo.TimeLines.I.ToWorld(new Vector3(x, 0.1f, z), h.Line.Value));
                log.AppendLine("tp: ok");
                return;
            }
            if (parts[0] == "state")
            {
                foreach (var h in Echo.Hero.All)
                    log.AppendLine($"hero{h.HeroId} line={h.Line.Value} local={h.LocalPos} hiding={h.HidingIn.Value} inv={h.Inventory().Count}");
                foreach (var g in Echo.Guard.All)
                    log.AppendLine($"guard soldier={g.Soldier.Value} state={g.Current} aw={g.Awareness.Value:0.00} stun={g.Stunned.Value} pos={g.transform.position}");
                log.AppendLine($"items={Echo.Item.All.Count} doors={Echo.Door.All.Count} swapped={Echo.GameDirector.I?.Swapped} restarting={Echo.GameDirector.I?.Restarting}");
                return;
            }
            switch (cmd)
            {
                case "build":
                    if (EditorApplication.isPlaying) { log.AppendLine("build: сначала stop"); return; }
                    EchoSceneBuilder.BuildSilently();
                    log.AppendLine("build: ok");
                    break;
                case "rig":
                    if (EditorApplication.isPlaying) { log.AppendLine("rig: сначала stop"); return; }
                    HeroRigBuilder.Build();
                    if (Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<HeroAppearance>() != null)
                        UnityEngine.Object.DestroyImmediate(Selection.activeGameObject);
                    log.AppendLine("rig: ok");
                    break;
                case "play":
                    if (EditorApplication.isPlaying) { log.AppendLine("play: уже"); return; }
                    if (EditorSceneManager.GetActiveScene().path != EchoSceneBuilder.ScenePath)
                        EditorSceneManager.OpenScene(EchoSceneBuilder.ScenePath);
                    SessionState.SetBool("Echo.AutoSinglePc", true);
                    EditorApplication.isPlaying = true;
                    log.AppendLine("play: ok");
                    break;
                case "stop":
                    EditorApplication.isPlaying = false;
                    log.AppendLine("stop: ok");
                    break;
                case "shot":
                    if (!EditorApplication.isPlaying) { log.AppendLine("shot: не в Play"); return; }
                    Directory.CreateDirectory(Dir);
                    ScreenCapture.CaptureScreenshot(Dir + "/shot.png", 2);
                    log.AppendLine("shot: ok");
                    break;
                case "buildwin":
                {
                    if (EditorApplication.isPlaying) { log.AppendLine("buildwin: сначала stop"); return; }
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = new[] { EchoSceneBuilder.ScenePath },
                        locationPathName = "Builds/Win/Naoborot.exe",
                        target = BuildTarget.StandaloneWindows64,
                        options = BuildOptions.None
                    });
                    var sum = report.summary;
                    log.AppendLine($"buildwin: {sum.result} ошибок={sum.totalErrors} размер={sum.totalSize / (1024 * 1024)}МБ время={sum.totalTime}");
                    foreach (var step in report.steps)
                        foreach (var msg in step.messages)
                            if (msg.type == LogType.Error || msg.type == LogType.Exception) log.AppendLine("  " + msg.content);
                    break;
                }
                case "refresh":
                    AssetDatabase.Refresh();
                    log.AppendLine("refresh: ok");
                    break;
                default:
                    log.AppendLine(cmd + ": неизвестная команда");
                    break;
            }
        }
    }
}
