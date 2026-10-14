# «Наоборот получилось» — общие правила и API ядра

Читают все чаты. Ядро (папка `Assets/_Echo/Scripts/*.cs`, `Assets/_Echo/Editor/EchoSceneBuilder.cs`, `Core/`) ведёт **Чат 1**. Его файлы НЕ редактировать — нужен хук или правка, пишите Виктору, он передаст в Чат 1.

## Проект
- Unity **6000.3.11f1**, URP, **только новая Input System** (`UnityEngine.InputSystem`, старый `Input.*` не работает).
- Netcode for GameObjects **2.13.3**, Multiplayer Services **2.4.0**, Unity Transport 2.7, AI Navigation 2.0. Vivox ставится (может ещё не быть).
- Весь код игры — сборка `Echo.Game` (`Assets/_Echo/Scripts/**`), редакторский — `Echo.Editor` (`Assets/_Echo/Editor/**`). Свои подпапки там компилируются автоматически, свои asmdef не создавать.
- Namespace: `Echo.<Модуль>` (`Echo.Combat`, `Echo.Levels`, `Echo.UI`).
- Сцена `Assets/_Echo/Scenes/Game.unity` **генерируется** меню *Echo → Собрать сцену прототипа*. Руками сцену не править — всё, что нужно в сцене, добавляйте через хуки сборки (ниже).
- Лог редактора для проверки компиляции: `C:\Users\yulia\AppData\Local\Unity\Editor\Editor.log` (ищите `error CS`). **Сломанная компиляция блокирует всех** — после каждой правки проверьте, что ошибок нет.

## Мир
- Две копии одной карты: **линия 1** (прошлое/настоящее, корень в (0,0,0)) и **линия 2** (будущее, корень в (1000,0,0)). «То же место» = та же локальная позиция.
- `TimeLines.I.ToWorld(local, line)`, `ToLocal(world, line)`, `LineOf(world)`.
- Уровень идёт по +X от 0, пол на y = 0, ширина z ≈ ±30. Обе линии — одинаковая геометрия, разный вид.
- **Линия 2 зеркальная** (решение Виктора: Герой 2 идёт по экрану справа налево). Сборщик ядра после всех шагов отражает раскладку линии 2 по X (позиции и повороты, не масштаб). Строить линию 2 нужно в тех же **локальных** координатах, что линию 1 — отражение произойдёт само. Не используйте отрицательный масштаб и не считайте мировые координаты линии 2 руками (`root.position + local` неверно для линии 2) — только `TimeLines.I.ToWorld/ToLocal`. `TimeLines.I.ForwardSign(line)` = +1 для линии 1, −1 для линии 2 (направление «вперёд по уровню» в мире).
- До сопряжения мир меняет Герой 1 (на линии 1), после — Герой 2 (он тогда на линии 1). Линия 1 всегда «прошлое», её изменения отражаются на линии 2.
- Всё сетевое — **авторитетно на сервере (хосте)**. Клиент шлёт ввод через `[Rpc(SendTo.Server)]`.
- Режим «Один ПК»: оба героя принадлежат хосту, `GameSession.SinglePc == true`, Герой 2 управляется стрелками.

## Герой (`Echo.Hero`, NetworkBehaviour)
- `Hero.All`, `Hero.Get(1|2)`, `HeroId`, `Line` (NetworkVariable<int>), `IsOwner`, `IsHiding`, `IsSneaking`, `LocalPos`.
- `Inventory()` → список `Item`, `Capacity` (6, с рюкзаком 14), `SelectedSlot`, `InventoryOpen` (локально у владельца).
- `ToastRpc(string)` — сервер → сообщение на экране владельца. `TeleportRpc(Vector3 world)`.
- Риг художника: `hero.rigAnimator` (параметры `Speed`, `Crouch`, триггеры `Shoot`, `Interact`), `hero.rigAppearance` (`HeroAppearance.Set(hair, eyes, face, skin, hairColor, suit)`, `SetGunVisible`), `hero.rigAura` (`HeroAura.SetAura`).
- `Hero.FlatDistance(a, b)` — расстояние по XZ.

## Предметы (`Echo.Item`, `Echo.ItemSystem`)
- `ItemType { Key, Barrel, Backpack, Ammo, Pistol }`. `Item.All`, `item.ItemType`, `item.Carrier.Value` (NetworkObjectId героя или 0), `item.Line`, `item.Local`, `item.KeyId`.
- Сервер: `ItemSystem.Spawn(type, keyId, line, local, isEcho:false, echoOf:0)`, `ItemSystem.Consume(item)`.
- Положенный на линии 1 предмет автоматически получает «оттиск» с аурой на линии 2.

## Управление партией
- `GameDirector.I.ServerCaught(hero)` / `ServerKilled(hero)` — общий рестарт комнаты для обоих. `Restarting`, `Swapped`.
- События (сервер): `GameEvents.ServerGameStarted` (всё ядро заспавнено — спавните своё), `ServerRoomRestart` (верните своё в исходное), `ServerSwapped`.
- `GameEvents.EnemiesPaused` (сервер, static bool): пока true — враги стоят и никого не замечают, поимка/смерть не перезапускают комнату. Включает интерфейс на время интро/внешности, учитывают охрана и враги боя; сбрасывается при старте партии.
- Шум: `Guard.EmitNoise(line, worldPos, radius)` — охрана линии идёт проверить. Оглушение: `guard.ServerStun(seconds)` (сервер), `guard.Stunned`, `guard.Current` (GuardState).

## Меню и сообщения (для интерфейса)
- `NetworkBootstrap` (на объекте `Game`): public `StartSinglePc()`, `HostIp()`, `JoinIp(ip)`, `CreateRelay()`, `JoinRelay(code)`, `Leave()`, `Status`, `Busy`; `drawLegacyGui = false` выключает старое IMGUI-меню.
- `Hud.ToastShown` (event `Action<int heroId, string text>`), `Hud.TryGetToast(heroId, out text)`; старый HUD выключается `hud.enabled = false`.
- `GameSession.JoinInfo` — код комнаты / IP для показа; `GameSession.SinglePc`.
- `hero.InteractHint` — строка «что сделает E» для своего героя («E — взять: Ключ 1», «Заперто: нужен ключ 2», «E — спрятаться в шкафчик», «Держи E вместе с напарником — сопряжение»), пустая, если рядом ничего. Показывать по центру низа своей половины.

## Метки в сцене (ставит модуль уровней)
| Компонент | Что делает |
|---|---|
| `ItemMarker {type, keyId}` | предмет; линия определяется по позиции |
| `DoorMarker {doorId, keyId}` | дверь 3×3 м по локальной оси X, ставить в проём; keyId 0 — без ключа; открыта на линии 1 → открыта и на линии 2 с тем же doorId |
| `GuardMarker {soldier}` + дочерние пустышки | охранник/военный (линия 1), дочерние объекты — точки патруля |
| `LockerSpot` | точка, где встать, чтобы спрятаться (визуал шкафчика ставьте рядом сами) |
| `CheckpointMarker` | возрождение; ставить на линии 1, работает для обеих; упорядочены по X |
| `ReactorMarker` | реактор сопряжения (на обеих линиях) |
| `MachineMarker {radius}` | аппарат финала (на обеих линиях) |
| слой 2 `Ignore Raycast` | стекло: охрана видит сквозь него |
NavMesh печётся на сервере при старте по коллайдерам детей корня линии — стены и мебель делайте с коллайдерами.

## Хуки сборки (Editor)
```csharp
// шаг сборки сцены: создать префабы, расставить свои объекты
[Echo.EditorTools.EchoBuildStep(order: 10)]
public static void Build(Echo.EditorTools.EchoBuildContext ctx) { ctx.RegisterNetworkPrefab(prefab); ctx.Line1 / ctx.Line2 / ctx.GameSystems }
// построитель уровня (только модуль уровней): заменяет тестовый коридор
[Echo.EditorTools.EchoLevelBuilder]
public static void BuildLine(Echo.EditorTools.EchoBuildContext ctx, Transform lineRoot, int line) { }
```
Runtime-атрибуты: `[Echo.AddToHeroPrefab]` — компонент добавится на сетевой префаб героя; `[Echo.AddToGameSystems]` — на объект `Game` в сцене.
Сетевые префабы: NetworkObject на корне; NavMeshAgent на префабе **выключен**, включать на сервере (см. `Guard.ServerInit`).

## Клавиши (заняты)
Ядро: WASD, E (действие/спрятаться, держать у реактора), G (положить), F (бросить), Tab (инвентарь), 1–0, Shift (красться). Один ПК, Герой 2: стрелки, Enter, Backspace, RCtrl, `\`, `]`, RShift.
Бой: ЛКМ (по сети), Space (Один ПК, Герой 1), Numpad0 (Один ПК, Герой 2). Интерфейс: Esc (пауза), V (рация, удерживать), M (микрофон вкл/выкл).

## Git
Коммитьте только свои файлы вместе с `.meta`. Не коммитьте `Library/`, `Temp/`, сцену `Game.unity` (её пересобирают).

## Пульт для проверки (без участия Виктора)
Unity выполняет команды из файла `_incoming/cmd/command_<чат>.txt` (по одной на строку), ответ — в `_incoming/cmd/result_<чат>.txt`. Свой суффикс: Чат 2 — `_combat`, Чат 3 — `_levels`, Чат 4 — `_ui` (без суффикса — Чат 1).
- Перед записью проверьте, что ваш `command_<чат>.txt` уже удалён Unity (предыдущая команда выполнена).
- Play общий на всех: перед `play`/`stop` посмотрите `state` — если игра уже идёт, работайте в ней, не перезапускайте чужой тест.
- Команды: `state` (герои, охрана, предметы), `play` (Play + «Один ПК»), `stop`, `shot` (скриншот 2× в `_incoming/cmd/shot.png`), `tp <герой> <x> <z>` (телепорт в локальные координаты своей линии), `use <герой>` (E), `hold <герой> 1|0` (держать E), `drop <герой>` / `throw <герой>` (первый предмет инвентаря), `face <герой> <dx> <dz>`, `god 1|0` (поимка и смерть не перезапускают комнату — для тестов), `pause 1|0` (EnemiesPaused), `click <текст кнопки>` (нажать активную uGUI-кнопку по части её текста, регистр не важен), `refresh` (перекомпилировать — только вне Play).
- `build` и `buildwin` (пересборка сцены / Windows-билд) — только Чат 1, чтобы не пересобирать сцену посреди чужого теста.
- Во время Play компиляция откладывается до `stop` — после правок кода делайте `stop`, `refresh`, проверьте Editor.log.
