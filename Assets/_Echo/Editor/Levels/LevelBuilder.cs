using Echo.EditorTools;
using UnityEditor;
using UnityEngine;

namespace Echo.Levels
{
    // Уровень «Наоборот получилось»: лобби → служебная зона → реактор (−1) → лаборатории → зал аппарата.
    // Идёт по +X от 0. Геометрия на обеих линиях одинаковая, различаются материалы и декор.
    public static class LevelBuilder
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        static Vector2 Gap(float center, float width) => new Vector2(center, width);

        [EchoLevelBuilder]
        public static void BuildLine(EchoBuildContext ctx, Transform lineRoot, int line)
        {
            var k = new LevelKit(lineRoot, line);
            var m = new Mats(k);

            Lobby(k, m);
            ServiceCorridor(k, m);
            ServiceZone(k, m);
            Stairs(k, m);
            ReactorHall(k, m);
            Labs(k, m);
            MachineHall(k, m);

            AssetDatabase.SaveAssets();
        }

        // Все материалы линии в одном месте.
        class Mats
        {
            public Material lobbyFloor, lobbyWall, officeFloor, officeWall, techFloor, techWall, hiFloor, hiWall;
            public Material wood, metal, dark, crate, locker, white, rubble, glass, water;
            public Material glowGreen, glowRed, glowWhite, glowCyan, ledGreen, ledRed;

            public Mats(LevelKit k)
            {
                Color c(float r, float g, float b) => new Color(r, g, b);
                lobbyFloor = k.Surface("lobby_floor", c(.88f, .9f, .93f), c(.36f, .4f, .37f), "lobby", "floor", .6f);
                lobbyWall = k.Surface("lobby_wall", c(.95f, .95f, .97f), c(.42f, .46f, .43f), "lobby", "wall");
                officeFloor = k.Surface("office_floor", c(.42f, .45f, .5f), c(.17f, .22f, .21f), "office", "floor");
                officeWall = k.Surface("office_wall", c(.86f, .84f, .76f), c(.3f, .34f, .31f), "office", "wall");
                techFloor = k.Surface("tech_floor", c(.33f, .35f, .38f), c(.15f, .18f, .17f), "tech", "floor", .3f);
                techWall = k.Surface("tech_wall", c(.58f, .59f, .6f), c(.25f, .27f, .26f), "tech", "wall");
                hiFloor = k.Surface("hitech_floor", c(.9f, .92f, .94f), c(.3f, .34f, .33f), "hitech", "floor", .6f);
                hiWall = k.Surface("hitech_wall", c(.84f, .9f, .95f), c(.28f, .33f, .33f), "hitech", "wall", .4f);

                wood = k.Surface("wood", c(.55f, .38f, .22f), c(.24f, .2f, .15f));
                metal = k.Surface("metal", c(.5f, .52f, .56f), c(.22f, .24f, .24f), null, null, .5f);
                dark = k.Surface("dark", c(.14f, .15f, .18f), c(.07f, .09f, .08f));
                crate = k.Surface("crate", c(.62f, .46f, .25f), c(.28f, .24f, .16f));
                locker = k.Surface("locker", c(.45f, .55f, .6f), c(.2f, .3f, .28f));
                white = k.Surface("white", c(.95f, .95f, .95f), c(.5f, .55f, .5f));
                rubble = k.Surface("rubble", c(.4f, .38f, .35f), c(.2f, .2f, .18f));
                glass = k.Transparent("glass", new Color(.6f, .85f, 1f, .22f), new Color(.35f, .8f, .55f, .3f), false);
                water = k.Transparent("water", new Color(.3f, .6f, .7f, .0f), new Color(.15f, .5f, .45f, .45f), false);
                glowGreen = k.Glow("glow_green", c(.7f, .95f, 1f), c(.3f, 1f, .5f), 1.8f);
                glowRed = k.Glow("glow_red", c(1f, .25f, .2f), c(.5f, 1f, .4f), 1.8f);
                glowWhite = k.Glow("glow_white", c(1f, 1f, 1f), c(.9f, 1f, .9f), 1.6f);
                glowCyan = k.Glow("glow_cyan", c(.5f, .95f, 1f), c(.35f, 1f, .75f), 1.6f);
                ledGreen = k.Glow("led_green", c(.2f, 1f, .3f), c(.2f, 1f, .3f), 2f);
                ledRed = k.Glow("led_red", c(1f, .2f, .15f), c(1f, .2f, .15f), 2f);
            }
        }

        // ---------- 1. Лобби (публичная зона) ----------

        static void Lobby(LevelKit k, Mats m)
        {
            k.Group("1_Lobby");
            k.Floor(-8f, 26f, -9f, 9f, m.lobbyFloor);
            k.WallZ(-8f, -9f, 9f, m.lobbyWall, Zone.Public, +1);
            k.WallX(9f, -8f, 26f, m.lobbyWall, Zone.Public, -1);
            k.WallX(-9f, -8f, 26f, m.lobbyWall, Zone.Public, +1);
            // Восточная стена с дверью в служебный коридор
            k.WallZ(26f, -9f, 9f, m.lobbyWall, Zone.Public, -1, Gap(0f, 3f));
            k.PlaceDoor(1, 1, V(26f, 0f, 0f));
            k.Sign("ДВЕРЬ 1 · НУЖЕН КЛЮЧ 1", V(24.5f, 0f, -3.4f), 0.28f);
            if (!k.L1) k.Sign("ДВЕРЬ ЗАПЕРТА · НАПАРНИК ОТКРОЕТ ЕЁ В ПРОШЛОМ", V(17f, 0f, -2.2f), 0.26f);
            k.Sign("КЛЮЧ-КАРТА НА РЕСЕПШЕНЕ", V(15f, 0f, 4.6f), 0.28f);
            k.Arrow(V(6f, 0f, 2.5f), -35f);
            k.Arrow(V(10f, 0f, 4.5f), -15f);
            k.Arrow(V(19f, 0f, 1.2f), 0f);
            k.Arrow(V(21f, 0f, 0f), 0f);

            // Ресепшен и ключ-карта
            k.Box("Reception", V(15f, 0.55f, 7.4f), V(7f, 1.1f, 1.4f), m.wood);
            k.Box("ReceptionLogo", V(15f, 1.4f, 8.05f), V(3f, 0.5f, 0.05f), m.glowWhite, false);
            k.PlaceItem(ItemType.Key, 1, V(15f, 1.15f, 6.9f));
            k.Sign("NoRIN.Co", V(15f, 0f, 8.5f), 0.7f);

            // Турникет: проход шириной ~1.3 м, ограждение до стен
            k.Box("TurnstileL", V(23f, 0.55f, 0.9f), V(1f, 1.1f, 0.5f), m.metal);
            k.Box("TurnstileR", V(23f, 0.55f, -0.9f), V(1f, 1.1f, 0.5f), m.metal);
            k.Box("TurnstileBarL", V(23f, 0.9f, 0.9f), V(0.1f, 0.1f, 0.5f), m.glowRed, false);
            k.Box("FenceN", V(23f, 0.55f, 5.075f), V(0.2f, 1.1f, 7.85f), m.white);
            k.Box("FenceS", V(23f, 0.55f, -5.075f), V(0.2f, 1.1f, 7.85f), m.white);
            k.Sign("ТОЛЬКО ДЛЯ ПЕРСОНАЛА", V(24.6f, 0f, 3.2f), 0.28f, 90f);

            // Экспозиция
            k.Cyl("ExhibitBase", V(7f, 0.2f, -6f), 1.7f, 0.4f, m.white);
            k.Cyl("ExhibitReactor", V(7f, 1.2f, -6f), 0.7f, 1.6f, m.metal);
            k.Ball("ExhibitGlow", V(7f, 2.2f, -6f), 0.4f, m.glowCyan);
            k.Sign("КАК РАБОТАЕТ ВРЕМЯ", V(7f, 0f, -8.1f), 0.4f);
            k.Box("Hologram", V(2.5f, 1.2f, -5.5f), V(0.1f, 2f, 1.6f), m.glowCyan, false);

            // Скамьи, колонны, гардероб, сувениры
            k.Box("BenchN", V(-4f, 0.25f, 5.5f), V(3.5f, 0.5f, 0.8f), m.wood);
            k.Box("BenchS", V(-4f, 0.25f, -5.5f), V(3.5f, 0.5f, 0.8f), m.wood);
            foreach (float x in new[] { 11f, 19f })
                foreach (float z in new[] { 3.5f, -3.5f })
                    k.Cyl("Column", V(x, 1.5f, z), 0.5f, 3f, m.white);
            k.Box("Cloakroom", V(16f, 1f, -8.2f), V(6f, 2f, 1.2f), m.wood);
            k.Sign("ГАРДЕРОБ", V(16f, 0f, -6.9f), 0.35f);
            k.Box("Souvenirs", V(-3f, 0.6f, -8.3f), V(5f, 1.2f, 1f), m.wood);
            k.Sign("СУВЕНИРЫ", V(-3f, 0f, -7f), 0.35f);
            k.Sign("ЛОББИ · ПУБЛИЧНАЯ ЗОНА", V(0f, 0f, 2.6f), 0.4f);
            k.Sign("ДОБРО ПОЖАЛОВАТЬ В NoRIN.Co", V(0f, 0f, -2.6f), 0.3f);

            // Будущее: завалы с проходами, зелёный свет
            if (!k.L1)
            {
                k.Box("Rubble", V(4f, 0.3f, -1f), V(1.8f, 0.6f, 1.4f), m.rubble, true, 20f);
                k.Box("Rubble", V(13f, 0.4f, -3f), V(2f, 0.8f, 1.4f), m.rubble, true, 35f);
                k.Box("Rubble", V(20f, 0.35f, 3.6f), V(1.6f, 0.7f, 1.6f), m.rubble, true, 10f);
                k.Box("FallenBeam", V(-3f, 0.2f, 1f), V(4f, 0.4f, 0.4f), m.metal, true, 70f);
                k.Box("BrokenCase", V(7.5f, 0.3f, -3.4f), V(1.2f, 0.6f, 0.8f), m.rubble, true, 15f);
                k.PlaceItem(ItemType.Pistol, 0, V(4f, 0f, 3f)); // пистолет Героя 2
                k.Sign("ПИСТОЛЕТ ГЕРОЯ 2 · E — ВЗЯТЬ", V(4f, 0f, 1.3f), 0.26f);
            }
            k.GlowLight(V(2f, 3.5f, 0f));
            k.GlowLight(V(14f, 3.5f, 0f));
            k.GlowLight(V(22f, 3.5f, 0f), 2.5f, 9f);

            k.PlaceMutant(V(10f, 0f, -1f));
            k.PlaceMutant(V(17f, 0f, -5f));
            k.PlaceMutant(V(20f, 0f, 3f));
            k.PlaceMutant(V(7f, 0f, 1.5f), true);
            k.PlaceMutant(V(1f, 0f, -4f), true);
            k.PlaceMutant(V(12f, 0f, 5f), true);
        }

        // ---------- 1б. Служебный коридор ----------

        static void ServiceCorridor(LevelKit k, Mats m)
        {
            k.Group("1b_ServiceCorridor");
            k.Floor(26f, 44f, -3.5f, 3.5f, m.officeFloor);
            k.WallX(3.5f, 26f, 44f, m.officeWall, Zone.Staff, -1);
            k.WallX(-3.5f, 26f, 44f, m.officeWall, Zone.Staff, +1);

            k.Locker(V(33f, 0f, 2.95f), 180f, m.locker);
            k.Locker(V(39f, 0f, -2.95f), 0f, m.locker);
            k.Box("Crates", V(29.5f, 0.5f, -2.8f), V(1f, 1f, 1f), m.crate);
            k.Sign("СЛУЖЕБНЫЙ КОРИДОР", V(35f, 0f, 0.4f), 0.35f);
            k.Sign("ШКАФЧИК · E — СПРЯТАТЬСЯ", V(36f, 0f, 2.2f), 0.24f);
            k.Arrow(V(29f, 0f, 0f), 0f);
            k.Arrow(V(41f, 0f, 0f), 0f);

            k.PlaceGuard(false, V(30f, 0f, -1.5f), V(42f, 0f, -1.5f), V(42f, 0f, 1.5f), V(30f, 0f, 1.5f));

            if (!k.L1)
            {
                k.Box("Rubble", V(36f, 0.3f, 2.4f), V(1.6f, 0.6f, 1.2f), m.rubble, true, 25f);
                k.Box("Water", V(35f, 0.02f, -1f), V(8f, 0.04f, 3f), m.water, false);
            }
            k.GlowLight(V(30f, 3.5f, 0f), 2.5f, 9f);
            k.GlowLight(V(40f, 3.5f, 0f), 2.5f, 9f);
        }

        // ---------- 2. Служебная зона ----------

        static void ServiceZone(LevelKit k, Mats m)
        {
            k.Group("2_ServiceZone");
            k.Floor(44f, 82f, -10f, 10f, m.officeFloor);
            k.WallX(10f, 44f, 82f, m.officeWall, Zone.Staff, -1);
            k.WallX(-10f, 44f, 82f, m.officeWall, Zone.Staff, +1);
            k.WallZ(44f, -10f, 10f, m.officeWall, Zone.Staff, +1, Gap(0f, 4f));
            k.WallZ(82f, -10f, 10f, m.officeWall, Zone.Staff, -1, Gap(0f, 3f));
            k.PlaceDoor(2, 2, V(82f, 0f, 0f)); // на лестницу к реактору (красная карта)

            // Перегородки восточной части: офис (север), раздевалка (юг), проход к двери
            k.WallZ(66f, -10f, 10f, m.officeWall, Zone.None, 0, Gap(0f, 4f));
            k.WallX(2.5f, 66f, 82f, m.officeWall, Zone.None, 0, Gap(72f, 3f));
            k.WallX(-2.5f, 66f, 82f, m.officeWall, Zone.None, 0, Gap(72f, 3f));

            // Опенспейс: столы, ширмы, кулер
            foreach (float x in new[] { 49f, 54f, 59f, 63f })
                foreach (float s in new[] { 1f, -1f })
                {
                    k.Box("Desk", V(x, 0.4f, 5f * s), V(2.4f, 0.8f, 1.2f), m.wood);
                    k.Box("Monitor", V(x, 1f, 5.2f * s), V(0.7f, 0.4f, 0.1f), m.dark, false);
                    k.Box("Chair", V(x, 0.25f, 6.3f * s), V(0.5f, 0.5f, 0.5f), m.dark);
                }
            foreach (float x in new[] { 51.5f, 56.5f, 61f })
                foreach (float s in new[] { 1f, -1f })
                    k.Box("Partition", V(x, 0.8f, 5f * s), V(0.1f, 1.6f, 2.2f), m.white);
            k.Cyl("Cooler", V(46f, 0.6f, -8.5f), 0.3f, 1.2f, m.glowCyan);
            k.Box("Whiteboard", V(62f, 1.6f, 9.74f), V(4f, 1.2f, 0.06f), m.white, false);
            k.Sign("ГРАФИК ОХРАНЫ", V(62f, 0f, 8.7f), 0.3f);

            // Серверные стойки с огоньками
            for (int i = 0; i < 5; i++)
            {
                float x = 47.5f + i * 1.6f;
                k.Box("Rack", V(x, 1.1f, 9.35f), V(1f, 2.2f, 0.9f), m.dark);
                k.Box("Led", V(x, 1.8f, 8.88f), V(0.8f, 0.06f, 0.02f), i % 2 == 0 ? m.ledGreen : m.ledRed, false);
                k.Box("Led", V(x, 1.4f, 8.88f), V(0.8f, 0.06f, 0.02f), i % 2 == 0 ? m.ledRed : m.ledGreen, false);
            }
            k.Sign("СЕРВЕРНАЯ", V(50.7f, 0f, 7.6f), 0.3f);

            // Склад
            k.Box("Crate", V(47.5f, 0.5f, -9.2f), V(1.2f, 1f, 1.2f), m.crate);
            k.Box("Crate", V(48.8f, 0.5f, -9.2f), V(1.2f, 1f, 1.2f), m.crate);
            k.Box("Crate", V(48.1f, 1.5f, -9.2f), V(1f, 1f, 1f), m.crate);
            k.PlaceItem(ItemType.Barrel, 0, V(49f, 0f, -7f));
            k.PlaceItem(ItemType.Barrel, 0, V(60f, 0f, 8f));

            // Кабинет начальника: красная карта на столе
            k.Box("BossDesk", V(76f, 0.45f, 7.4f), V(3.2f, 0.9f, 1.4f), m.wood);
            k.Box("BossChair", V(76f, 0.4f, 8.7f), V(0.7f, 0.8f, 0.7f), m.dark);
            k.Box("Bookshelf", V(70f, 1.1f, 9.55f), V(3f, 2.2f, 0.5f), m.wood);
            k.Cyl("Plant", V(80.5f, 0.5f, 9f), 0.35f, 1f, m.glowGreen, false);
            k.PlaceItem(ItemType.Key, 2, V(76f, 1.0f, 6.9f));
            k.Sign("КАБИНЕТ НАЧАЛЬНИКА", V(74f, 0f, 4f), 0.3f);

            // Раздевалка: шкафчики вдоль стены, рюкзак на скамье
            k.Box("LockerRow", V(74f, 1.1f, -9.45f), V(13f, 2.2f, 0.7f), m.locker);
            foreach (float x in new[] { 69f, 72f, 75f, 78f }) k.LockerSpotAt(V(x, 0f, -8.6f));
            k.Box("Bench", V(74f, 0.25f, -6f), V(4f, 0.5f, 0.7f), m.wood);
            k.PlaceItem(ItemType.Backpack, 0, V(74f, 0.55f, -6f));
            k.Sign("РАЗДЕВАЛКА", V(74f, 0f, -4.2f), 0.3f);
            k.Sign("ЛЕСТНИЦА К РЕАКТОРУ →", V(78.5f, 0f, 0f), 0.28f);
            k.Sign("ДВЕРЬ 2 · НУЖЕН КЛЮЧ 2 (КРАСНАЯ КАРТА)", V(77f, 0f, -1.8f), 0.24f);
            if (!k.L1) k.Sign("ДВЕРЬ ЗАПЕРТА · НАПАРНИК ОТКРОЕТ ЕЁ В ПРОШЛОМ", V(75f, 0f, 1.8f), 0.22f);
            k.Sign("КРАСНАЯ КАРТА В КАБИНЕТЕ", V(72f, 0f, 4.8f), 0.24f);
            k.Sign("ШКАФЧИКИ · E — СПРЯТАТЬСЯ", V(73f, 0f, -7.6f), 0.24f);
            k.Arrow(V(47f, 0f, 0f), 0f);
            k.Arrow(V(56f, 0f, 0f), 0f);
            k.Arrow(V(64f, 0f, 0f), 0f);
            k.Arrow(V(70f, 0f, 0f), 0f);
            k.Arrow(V(72f, 0f, 1.5f), -90f, 0.8f);
            k.Arrow(V(72f, 0f, -1.5f), 90f, 0.8f);
            k.Arrow(V(79f, 0f, 0f), 0f);
            k.Sign("СЛУЖЕБНАЯ ЗОНА · ТОЛЬКО ДЛЯ ПЕРСОНАЛА", V(55f, 0f, 0f), 0.35f);

            k.PlaceGuard(false, V(50f, 0f, -1.5f), V(62f, 0f, -1.5f), V(62f, 0f, 1.5f), V(50f, 0f, 1.5f));
            k.PlaceGuard(false, V(68f, 0f, -1f), V(80f, 0f, -1f), V(80f, 0f, 1f), V(68f, 0f, 1f));
            k.PlaceCheckpoint(V(45.5f, 0f, -6.5f));

            if (!k.L1)
            {
                k.Box("Water", V(56f, 0.02f, -1f), V(12f, 0.04f, 6f), m.water, false);
                k.Box("OverturnedDesk", V(52f, 0.5f, -3f), V(2.4f, 0.8f, 1.2f), m.wood, true, 40f);
                k.Box("OverturnedDesk", V(60f, 0.5f, 2.5f), V(2.4f, 0.8f, 1.2f), m.wood, true, 115f);
                k.Box("Rubble", V(67.5f, 0.4f, 6f), V(1.6f, 0.8f, 1.6f), m.rubble, true, 20f);
                k.Box("Cables", V(55f, 0.05f, 8f), V(8f, 0.1f, 0.1f), m.dark, false);
            }
            k.GlowLight(V(50f, 3.5f, 0f));
            k.GlowLight(V(60f, 3.5f, 0f));
            k.GlowLight(V(74f, 3.5f, 0f));

            k.PlaceMutant(V(54f, 0f, 0f));
            k.PlaceMutant(V(74f, 0f, -6.5f));
            k.PlaceMutant(V(75f, 0f, 5f));
            k.PlaceMutant(V(47f, 0f, 3f), true);
            k.PlaceMutant(V(64f, 0f, -7f), true);
            k.PlaceMutant(V(72f, 0f, 0f), true);
        }

        // ---------- 3а. Лестница вниз ----------

        static void Stairs(LevelKit k, Mats m)
        {
            k.Group("3a_Stairs");
            k.Floor(82f, 94f, -3f, 3f, m.techFloor);
            k.WallX(3f, 82f, 94f, m.techWall, Zone.Closed, -1);
            k.WallX(-3f, 82f, 94f, m.techWall, Zone.Closed, +1);
            for (int i = 0; i < 6; i++)
                k.Box("Step", V(84f + i * 1.6f, 0.03f, 0f), V(0.5f, 0.06f, 5.6f), m.dark, false);
            k.Sign("УРОВЕНЬ -1 · ЗАКРЫТАЯ ЗОНА", V(88f, 0f, 1.7f), 0.28f);
            k.Sign("ВХОД ТОЛЬКО С ДОПУСКОМ", V(88f, 0f, -1.7f), 0.28f);
            k.Arrow(V(85f, 0f, 0f), 0f);
            k.Arrow(V(89f, 0f, 0f), 0f);
            k.PlaceCheckpoint(V(91f, 0f, 0f));
            if (!k.L1) k.Box("Rubble", V(86f, 0.3f, 2f), V(1.4f, 0.6f, 1.2f), m.rubble, true, 30f);
            k.GlowLight(V(88f, 3.2f, 0f), 2.5f, 8f);
        }

        // ---------- 3. Реакторный уровень −1 ----------

        static void ReactorHall(LevelKit k, Mats m)
        {
            k.Group("3_ReactorHall");
            k.Floor(94f, 136f, -12f, 12f, m.techFloor);
            k.WallX(12f, 94f, 136f, m.techWall, Zone.Closed, -1);
            k.WallX(-12f, 94f, 136f, m.techWall, Zone.Closed, +1);
            k.WallZ(94f, -12f, 12f, m.techWall, Zone.Closed, +1, Gap(0f, 4f));
            k.WallZ(136f, -12f, 12f, m.techWall, Zone.Closed, -1, Gap(0f, 4f));

            // Реактор: высокий цилиндр с соплом, трубами и лампами
            k.Cyl("ReactorBase", V(124f, 0.25f, 0f), 4.2f, 0.5f, m.metal, false);
            k.Cyl("ReactorBody", V(124f, 3.5f, 0f), 3f, 6f, m.metal);
            k.Cyl("ReactorRingLow", V(124f, 1.5f, 0f), 3.15f, 0.2f, m.glowRed, false);
            k.Cyl("ReactorRingMid", V(124f, 3.5f, 0f), 3.15f, 0.2f, m.white, false);
            k.Cyl("ReactorNozzle", V(124f, 6.6f, 0f), 1.7f, 1.2f, m.dark, false);
            k.Cyl("ReactorTop", V(124f, 7.6f, 0f), 1f, 0.9f, m.glowGreen, false);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                k.Ball("Lamp", V(124f + Mathf.Cos(a) * 3.1f, 4.6f, Mathf.Sin(a) * 3.1f), 0.2f, i % 2 == 0 ? m.glowRed : m.glowWhite);
            }
            k.Box("PipeA", V(129f, 2f, 2.6f), V(4f, 0.5f, 0.5f), m.metal);
            k.Box("PipeB", V(129f, 3f, -2.6f), V(4f, 0.5f, 0.5f), m.metal);
            k.Box("PipeC", V(131f, 1.5f, 0f), V(0.5f, 3f, 0.5f), m.metal);

            // Звезда сопряжения перед реактором
            k.PlaceReactor(V(110f, 0f, 0f));
            k.Cyl("StarRing", V(110f, 0.03f, 0f), 2.6f, 0.04f, m.glowCyan, false);
            for (int i = 0; i < 4; i++)
                k.Box("StarRay", V(110f, 0.05f, 0f), V(5.6f, 0.04f, 0.4f), m.glowCyan, false, i * 45f);
            k.Sign("ТОЧКА СОПРЯЖЕНИЯ · УДЕРЖИВАЙТЕ E", V(110f, 0f, 3.4f), 0.3f);

            // Пульт управления за стеклом (север)
            k.Box("BoothGlass", V(106f, 1.5f, 8f), V(12f, 3f, 0.15f), m.glass, true, 0f, 2);
            k.Box("BoothWallW", V(100f, 1.5f, 10f), V(0.3f, 3f, 4f), m.techWall);
            k.Box("BoothWallE", V(112f, 1.5f, 10f), V(0.3f, 3f, 4f), m.techWall);
            k.Box("Console", V(106f, 0.5f, 11f), V(8f, 1f, 1f), m.dark);
            k.Box("ConsoleScreen", V(106f, 1.3f, 11.3f), V(6f, 0.5f, 0.05f), m.glowGreen, false);
            k.Sign("ПУЛЬТ УПРАВЛЕНИЯ", V(106f, 0f, 6.8f), 0.3f);

            // Ящики-укрытия
            k.Box("Crate", V(101f, 0.75f, -3.5f), V(1.5f, 1.5f, 1.5f), m.crate, true, 10f);
            k.Box("Crate", V(104f, 0.75f, 3.5f), V(1.5f, 1.5f, 1.5f), m.crate, true, -8f);
            k.Box("Crate", V(104f, 2.0f, 3.5f), V(1f, 1f, 1f), m.crate, true, 20f);
            k.Box("Crate", V(107.5f, 0.75f, -4.5f), V(1.5f, 1.5f, 1.5f), m.crate);
            k.Box("Crate", V(115.5f, 0.75f, 5.5f), V(1.5f, 1.5f, 1.5f), m.crate, true, 25f);
            k.Box("Crate", V(116f, 0.75f, -8.5f), V(1.5f, 1.5f, 1.5f), m.crate);
            k.Box("Crate", V(131f, 0.75f, 5.5f), V(1.5f, 1.5f, 1.5f), m.crate);
            k.Box("Crate", V(131f, 0.75f, -5.5f), V(1.5f, 1.5f, 1.5f), m.crate, true, 15f);

            // Шкафчики у входа
            k.Box("LockerW1", V(94.55f, 1.1f, -8.5f), V(0.7f, 2.2f, 3.5f), m.locker);
            k.LockerSpotAt(V(95.4f, 0f, -8.5f));
            k.Box("LockerW2", V(94.55f, 1.1f, 8.5f), V(0.7f, 2.2f, 3.5f), m.locker);
            k.LockerSpotAt(V(95.4f, 0f, 8.5f));

            // Грузовой лифт на выходе
            k.Box("ElevatorFrameN", V(135.5f, 1.5f, 2.35f), V(0.6f, 3f, 0.3f), m.metal);
            k.Box("ElevatorFrameS", V(135.5f, 1.5f, -2.35f), V(0.6f, 3f, 0.3f), m.metal);
            k.Sign("ГРУЗОВОЙ ЛИФТ →", V(131.5f, 0f, 0f), 0.3f);
            k.Sign("РЕАКТОРНЫЙ УРОВЕНЬ -1", V(99f, 0f, 0f), 0.35f);
            k.Sign("ШКАФЧИК · E — СПРЯТАТЬСЯ", V(98.5f, 0f, 6.2f), 0.22f);
            k.Sign("ШКАФЧИК · E — СПРЯТАТЬСЯ", V(98.5f, 0f, -6.2f), 0.22f);
            k.Sign("ВДВОЁМ ДЕРЖИТЕ E ЗДЕСЬ", V(110f, 0f, -3.4f), 0.26f);
            k.Arrow(V(97f, 0f, 0f), 0f);
            k.Arrow(V(101f, 0f, 0f), 0f);
            k.Arrow(V(105f, 0f, 0f), 0f);
            k.Arrow(V(131f, 0f, 1.6f), 0f);

            // Два военных: кольцо вокруг реактора и южная часть зала
            k.PlaceGuard(true, V(119.5f, 0f, -6f), V(119.5f, 0f, 6f), V(128.5f, 0f, 6f), V(128.5f, 0f, -6f));
            k.PlaceGuard(true, V(104f, 0f, -10f), V(114f, 0f, -10f), V(114f, 0f, -6.5f), V(104f, 0f, -6.5f));

            if (!k.L1)
            {
                k.Box("Rubble", V(100f, 0.4f, 1f), V(2f, 0.8f, 1.4f), m.rubble, true, 30f);
                k.Box("Rubble", V(121f, 0.5f, 7f), V(2.2f, 1f, 1.6f), m.rubble, true, 15f);
                k.Box("Rubble", V(127.5f, 0.4f, -7f), V(2f, 0.8f, 1.4f), m.rubble, true, 50f);
                k.Box("BrokenPipe", V(118f, 0.25f, 0f), V(5f, 0.5f, 0.5f), m.metal, true, 75f);
                k.Box("Crack", V(124f, 4.2f, 3.05f), V(0.12f, 3f, 0.05f), m.dark, false);
                k.GlowLight(V(124f, 5f, 0f), 7f, 16f);
            }
            k.GlowLight(V(108f, 3.5f, 0f), 4f, 14f);
            k.GlowLight(V(98f, 3.5f, 0f), 2.5f, 9f);

            k.PlaceMutant(V(117.5f, 0f, -5.5f));
            k.PlaceClot(V(118f, 0f, 5f));
            k.PlaceMutant(V(100f, 0f, -2f), true);
            k.PlaceMutant(V(108f, 0f, 7f), true);
        }

        // ---------- 4а. Нижние лаборатории ----------

        static void Labs(LevelKit k, Mats m)
        {
            k.Group("4_Labs");
            // Шлюз дезинфекции и стеклянный коридор
            k.Floor(136f, 172f, -3.5f, 3.5f, m.hiFloor);
            k.WallX(3.5f, 136f, 148f, m.hiWall, Zone.Closed, -1);
            k.WallX(-3.5f, 136f, 148f, m.hiWall, Zone.Closed, +1);
            k.GlassX(3.5f, 148f, 172f, m.glass, m.metal, Gap(159f, 3f));
            k.GlassX(-3.5f, 148f, 172f, m.glass, m.metal, Gap(156f, 3f));
            k.Sign("ШЛЮЗ ДЕЗИНФЕКЦИИ", V(140f, 0f, 0f), 0.3f);
            k.Box("ShowerStripN", V(138f, 0.03f, 2.7f), V(3f, 0.06f, 0.5f), m.glowCyan, false);
            k.Box("ShowerStripS", V(138f, 0.03f, -2.7f), V(3f, 0.06f, 0.5f), m.glowCyan, false);
            k.Sign("СТЕКЛЯННЫЙ КОРИДОР", V(160f, 0f, 0f), 0.3f);
            k.Sign("КЛЮЧ 3 · КРИО-СКЛАД", V(159f, 0f, 2.1f), 0.24f);
            k.Sign("ДВЕРЬ 3 · НУЖЕН КЛЮЧ 3", V(168f, 0f, -2.2f), 0.24f);
            if (!k.L1) k.Sign("ДВЕРЬ ЗАПЕРТА · НАПАРНИК ОТКРОЕТ ЕЁ В ПРОШЛОМ", V(156f, 0f, 1.2f), 0.2f);
            k.Arrow(V(142f, 0f, 0f), 0f);
            k.Arrow(V(146f, 0f, 0f), 0f);
            k.Arrow(V(159f, 0f, 1.7f), -90f, 0.8f);
            k.Arrow(V(150f, 0f, -1.7f), 0f);
            k.Arrow(V(168f, 0f, 0f), 0f);
            k.PlaceCheckpoint(V(138f, 0f, 0f));

            // Крио-склад (север)
            k.Floor(148f, 172f, 3.5f, 14f, m.hiFloor);
            k.WallX(14f, 148f, 172f, m.hiWall, Zone.Closed, -1);
            k.WallZ(148f, 3.5f, 14f, m.hiWall, Zone.Closed, +1);
            foreach (float x in new[] { 150f, 153f, 156f, 162f, 165f, 168f, 170.5f })
                k.Cyl("CryoCapsule", V(x, 1.1f, 13f), 0.6f, 2.2f, m.glowCyan);
            foreach (float x in new[] { 152f, 164f, 168f })
                k.Cyl("CryoCapsule", V(x, 1.1f, 9.5f), 0.6f, 2.2f, m.glowCyan);
            k.PlaceItem(ItemType.Key, 3, V(167f, 0f, 6.8f));
            k.PlaceItem(ItemType.Barrel, 0, V(152f, 0f, 6f));
            k.PlaceItem(ItemType.Barrel, 0, V(156f, 0f, 7.5f));
            k.Sign("КРИО-СКЛАД", V(160f, 0f, 6f), 0.35f);

            // Лаборатория (юг)
            k.Floor(148f, 172f, -14f, -3.5f, m.hiFloor);
            k.WallX(-14f, 148f, 172f, m.hiWall, Zone.Closed, +1);
            k.WallZ(148f, -14f, -3.5f, m.hiWall, Zone.Closed, +1);
            k.Box("HoloTable", V(152f, 0.45f, -8f), V(2.4f, 0.9f, 1.2f), m.white);
            k.Box("HoloTop", V(152f, 1.0f, -8f), V(2f, 0.1f, 0.9f), m.glowCyan, false);
            k.Box("HoloTable", V(158f, 0.45f, -10.5f), V(2.4f, 0.9f, 1.2f), m.white);
            k.Box("HoloTop", V(158f, 1.0f, -10.5f), V(2f, 0.1f, 0.9f), m.glowCyan, false);
            k.Cyl("RobotArm", V(164f, 1f, -9f), 0.3f, 2f, m.metal);
            k.Box("RobotArmHand", V(165f, 2f, -9f), V(2f, 0.25f, 0.25f), m.metal, false);
            k.Cyl("RobotArm", V(168f, 1f, -11f), 0.3f, 2f, m.metal);
            k.Sign("ЛАБОРАТОРИЯ", V(160f, 0f, -6f), 0.35f);

            // Военный в коридоре (на линии 1 после сопряжения здесь Герой 2)
            k.PlaceGuard(true, V(152f, 0f, -1.5f), V(168f, 0f, -1.5f), V(168f, 0f, 1.5f), V(152f, 0f, 1.5f));

            // Оружие Героя 1 на линии 2 — после реактора
            if (!k.L1)
            {
                k.PlaceItem(ItemType.Pistol, 0, V(140f, 0f, 2f));
                k.PlaceItem(ItemType.Ammo, 0, V(141f, 0f, -2f));
                k.PlaceItem(ItemType.Ammo, 0, V(166f, 0f, -10f));
                k.Box("Rubble", V(145f, 0.3f, -1f), V(1.4f, 0.6f, 1.2f), m.rubble, true, 30f);
                k.Box("Shards", V(156f, 0.03f, 0f), V(6f, 0.06f, 2.4f), m.glass, false);
                k.Box("Shards", V(166f, 0.03f, 1f), V(4f, 0.06f, 1.6f), m.glass, false);
            }
            k.GlowLight(V(142f, 3.2f, 0f), 2.5f, 9f);
            k.GlowLight(V(158f, 3.2f, 0f), 3f, 12f);
            k.GlowLight(V(160f, 3.2f, 9f), 3f, 12f);

            // Дверь в зал аппарата
            k.WallZ(172f, -14f, 14f, m.hiWall, Zone.None, 0, Gap(0f, 3f));
            k.PlaceDoor(3, 3, V(172f, 0f, 0f));

            k.PlaceMutant(V(156f, 0f, 1f));
            k.PlaceMutant(V(160f, 0f, 9f));
            k.PlaceMutant(V(146f, 0f, -1f), true);
            k.PlaceMutant(V(162f, 0f, -8f), true);
        }

        // ---------- 4б. Зал аппарата ----------

        static void MachineHall(LevelKit k, Mats m)
        {
            k.Group("5_MachineHall");
            k.Floor(172f, 206f, -12f, 12f, m.hiFloor);
            k.WallX(12f, 172f, 206f, m.hiWall, Zone.Closed, -1);
            k.WallX(-12f, 172f, 206f, m.hiWall, Zone.Closed, +1);
            k.WallZ(206f, -12f, 12f, m.hiWall, Zone.Closed, -1);
            k.PlaceCheckpoint(V(176f, 0f, -8f));

            // Круглая плита, трап и платформа туннеля
            k.Cyl("Plate", V(197f, 0.02f, 0f), 6f, 0.04f, m.white, false);
            k.Box("Ramp", V(186f, 0.35f, 0f), V(8.05f, 0.3f, 4.5f), m.metal, true, 0f, 0, null, Quaternion.Euler(0f, 0f, 7.2f));
            k.Box("TunnelDeck", V(198f, 0.5f, 0f), V(16f, 1f, 4.5f), m.metal);
            k.Box("RailN", V(186f, 0.9f, 2.4f), V(8f, 0.1f, 0.1f), m.glowWhite, false, 0f, 0, null, Quaternion.Euler(0f, 0f, 7.2f));
            k.Box("RailS", V(186f, 0.9f, -2.4f), V(8f, 0.1f, 0.1f), m.glowWhite, false, 0f, 0, null, Quaternion.Euler(0f, 0f, 7.2f));

            // Кольца аппарата: на линии 1 белые сегменты, на линии 2 — призматическая аура
            var prism = new[] { m.glowGreen, m.glowCyan, m.glowRed, m.glowWhite };
            for (int i = 0; i < 6; i++)
            {
                float x = 192f + i * 2.4f;
                var ringMat = k.L1 ? m.glowWhite : prism[i % prism.Length];
                k.Box("RingPostN", V(x, 2.8f, 2.4f), V(0.35f, 3.6f, 0.35f), m.metal);
                k.Box("RingPostS", V(x, 2.8f, -2.4f), V(0.35f, 3.6f, 0.35f), m.metal);
                k.Box("RingTop", V(x, 4.75f, 0f), V(0.35f, 0.35f, 5.15f), m.metal, false);
                k.Box("RingGlowN", V(x, 2.8f, 2.1f), V(0.12f, 3f, 0.12f), ringMat, false);
                k.Box("RingGlowS", V(x, 2.8f, -2.1f), V(0.12f, 3f, 0.12f), ringMat, false);
                k.Box("RingGlowTop", V(x, 4.45f, 0f), V(0.12f, 0.12f, 4.5f), ringMat, false);
            }
            k.Box("TunnelEnd", V(205.6f, 3f, 0f), V(0.3f, 4.6f, 4.5f), m.glowWhite, false);

            // Начало трапа: здесь Герой 2 выбирает концовку
            k.PlaceMachine(V(181f, 0f, 0f), 3f);
            k.Sign("АППАРАТ · НАЧАЛО ТРАПА", V(179.5f, 0f, 3.8f), 0.3f);
            k.Sign("ЗАЛ АППАРАТА -12", V(178f, 0f, -3.8f), 0.3f);
            k.Arrow(V(175f, 0f, 0f), 0f);
            k.Arrow(V(178f, 0f, 0f), 0f);

            // Галерея наблюдения за стеклом (север)
            k.Box("GalleryGlass", V(189f, 1.5f, 8f), V(24f, 3f, 0.15f), m.glass, true, 0f, 2);
            k.Box("GalleryDesk", V(189f, 0.5f, 10.8f), V(20f, 1f, 1.2f), m.dark);
            k.Box("GalleryScreen", V(189f, 1.3f, 11.3f), V(14f, 0.6f, 0.05f), m.glowCyan, false);
            k.Box("GalleryWallW", V(177f, 1.5f, 10f), V(0.3f, 3f, 4f), m.hiWall);
            k.Box("GalleryWallE", V(201f, 1.5f, 10f), V(0.3f, 3f, 4f), m.hiWall);
            // Фермы и лестницы по бокам
            foreach (float x in new[] { 190f, 195f, 200f })
                k.Box("Truss", V(x, 1.5f, -9f), V(0.4f, 3f, 0.4f), m.metal);
            k.Box("TrussBeam", V(195f, 3f, -9f), V(10.4f, 0.3f, 0.3f), m.metal, false);
            k.Box("Ladder", V(203f, 1.5f, -9f), V(0.3f, 3f, 1f), m.metal);

            if (!k.L1)
            {
                k.Box("Rubble", V(180f, 0.5f, 7f), V(2.2f, 1f, 1.6f), m.rubble, true, 20f);
                k.Box("Rubble", V(184f, 0.4f, -7f), V(2f, 0.8f, 1.4f), m.rubble, true, 55f);
                k.Box("Rubble", V(176f, 0.3f, 3f), V(1.4f, 0.6f, 1.2f), m.rubble, true, 10f);
                k.Box("FallenTruss", V(196f, 0.25f, -6f), V(7f, 0.4f, 0.4f), m.metal, true, 20f);
                k.GlowLight(V(197f, 5.5f, 0f), 8f, 18f);
            }
            k.GlowLight(V(180f, 3.5f, 0f), 3f, 12f);

            k.PlaceClot(V(184f, 0f, -5f));
            k.PlaceClot(V(190f, 0f, 5.5f));
            k.PlaceMutant(V(178f, 0f, 2f), true);
            k.PlaceMutant(V(186f, 0f, 8f), true);
        }
    }
}
