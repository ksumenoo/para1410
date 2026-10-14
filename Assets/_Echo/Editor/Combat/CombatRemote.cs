using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Echo.Combat.EditorBuild
{
    // Пульт боя (как пульт ядра, но свой файл): _incoming/cmd/combat.txt → _incoming/cmd/combat_result.txt.
    // Только в Play на хосте. Команды:
    //   cstate                       — враги, обоймы, пули
    //   pistol <герой>               — выдать пистолет
    //   ammo <герой>                 — выдать пачку патронов
    //   shoot <герой> <dx> <dz>      — выстрел по направлению в МИРОВЫХ осях (линия 2 отзеркалена по X)
    //   dormant <n>                  — n-й мутант (по локальному X) становится «мёртвым до нас»
    //   hurt <герой>                 — удар по герою
    [InitializeOnLoad]
    public static class CombatRemote
    {
        const string Dir = "_incoming/cmd";
        const string CommandFile = Dir + "/combat.txt";
        const string ResultFile = Dir + "/combat_result.txt";
        static double _next;

        static CombatRemote() => EditorApplication.update += Poll;

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 0.5;
            if (!File.Exists(CommandFile)) return;
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
                try { Run(cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), log); }
                catch (Exception e) { log.AppendLine(cmd + ": ОШИБКА " + e.Message); Debug.LogException(e); }
            }
            Directory.CreateDirectory(Dir);
            File.WriteAllText(ResultFile, DateTime.Now.ToString("HH:mm:ss") + "\n" + log);
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static void Run(string[] p, StringBuilder log)
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (!EditorApplication.isPlaying || nm == null || !nm.IsServer) { log.AppendLine(p[0] + ": нужен Play (хост)"); return; }

            switch (p[0])
            {
                case "cstate":
                    foreach (var h in Hero.All)
                    {
                        var hp = h.GetComponent<HeroHealth>();
                        var gun = h.GetComponent<HeroGun>();
                        log.AppendLine($"hero{h.HeroId} line={h.Line.Value} local={h.LocalPos} hp={hp?.Hp.Value} pistol={gun?.HasPistol} " +
                                       $"clip={gun?.InverseRounds.Value} loaded={gun?.LoadedRounds.Value} packs={gun?.AmmoPacks} " +
                                       $"bulletsOut={gun?.ServerBulletsOut} inCorpses={gun?.ServerBulletsInCorpses}");
                    }
                    foreach (var e in Enemy.All.OrderBy(e => TimeLines.I.ToLocal(e.transform.position, e.Line).x))
                    {
                        string extra = e is Mutant m ? $" chase={m.Chasing.Value} dormant={m.Dormant} bullet={m.HoldsBullet.Value}" : "";
                        log.AppendLine($"{e.GetType().Name} line={e.Line} dead={e.Dead.Value}{extra} local={TimeLines.I.ToLocal(e.transform.position, e.Line)}");
                    }
                    log.AppendLine($"restarting={GameDirector.I?.Restarting} swapped={GameDirector.I?.Swapped}");
                    break;

                case "pistol":
                case "ammo":
                {
                    var h = Hero.Get(int.Parse(p[1]));
                    var it = ItemSystem.Spawn(p[0] == "pistol" ? ItemType.Pistol : ItemType.Ammo, 0, h.Line.Value, h.LocalPos, false, 0);
                    ItemSystem.ServerPickup(h, it);
                    log.AppendLine(p[0] + ": ok");
                    break;
                }

                case "shoot":
                {
                    var h = Hero.Get(int.Parse(p[1]));
                    h.GetComponent<HeroGun>().DebugShoot(new Vector3(F(p[2]), 0f, F(p[3])));
                    log.AppendLine("shoot: ok");
                    break;
                }

                case "dormant":
                {
                    var list = Enemy.All.OfType<Mutant>().OrderBy(m => TimeLines.I.ToLocal(m.transform.position, m.Line).x).ToList();
                    int n = int.Parse(p[1]);
                    if (n < 0 || n >= list.Count) { log.AppendLine("dormant: нет такого, мутантов " + list.Count); break; }
                    list[n].DebugMakeDormant();
                    log.AppendLine("dormant: ok " + list[n].transform.position);
                    break;
                }

                case "hurt":
                {
                    var h = Hero.Get(int.Parse(p[1]));
                    log.AppendLine("hurt: " + h.GetComponent<HeroHealth>().ServerDamage(1, "Отладка."));
                    break;
                }

                default:
                    log.AppendLine(p[0] + ": неизвестная команда");
                    break;
            }
        }
    }
}
