ГЕРОЙ — СКЕЛЕТНАЯ ВЕРСИЯ (заменяет старые ленты кадров)

Это скелет на обычных Transform (cut-out анимация): кости — пустые объекты,
части тела — SpriteRenderer на них. Пакет 2D Animation не нужен.
Unity сам плавно двигает кости между ключевыми позами.

УСТАНОВКА (5 минут)
1. Папку Art скопировать в Assets, чтобы получилось
   Assets/Art/Characters/Hero/Rig/... (60 png + hero_rig.json).
2. Скрипты:
   Unity/Runtime/HeroAppearance.cs, HeroAura.cs  ->  Assets/Scripts/Hero/
   Unity/Editor/HeroRigBuilder.cs                ->  Assets/Scripts/Hero/Editor/
   (папка обязательно называется Editor)
3. Меню Tools -> Наоборот -> Собрать героя.
   Скрипт сам настроит импорт спрайтов (пивоты в суставах, PPU 100),
   соберёт объект Hero в сцене и сохранит в Assets/Art/Characters/Hero/Generated:
     Hero.prefab, Hero.controller, 6 анимаций (.anim).
   Повторный запуск пересобирает всё заново.

АНИМАЦИИ (в Hero.controller уже с переходами)
  idle         покой
  walk         ходьба
  crouch_idle  присед на месте
  sneak        крадущийся шаг
  shoot        выстрел (вспышка на первом кадре)
  interact     взять / положить (E)

Параметры аниматора:
  animator.SetFloat("Speed", скорость);      // > 0.1 — идёт
  animator.SetBool("Crouch", shiftЗажат);    // присед / крадётся
  animator.SetTrigger("Shoot");
  animator.SetTrigger("Interact");

Поворот влево: localScale.x = -1 у объекта Hero.
Герой рисуется в плоскости XY. Для камеры под 50° поворачивай его к камере, как остальные спрайты.
Рост героя около 2,3 юнита. Нужен другой размер — масштабируй корень Hero.

ВНЕШНОСТЬ (выбирается один раз, одна на обоих игроков)
  GetComponent<HeroAppearance>().Set(hair, eyes, face, skin, hairColor, suit);
  индексы с 0: причёска 0–5, глаза 0–3, мимика 0–3, кожа 0–4, волосы 0–5, комбинезон 0–3.
  По сети достаточно синхронизировать эти 6 чисел (NetworkVariable) и вызвать Set.

ПИСТОЛЕТ
  GetComponent<HeroAppearance>().SetGunVisible(true);
  У Героя 1 — после сопряжения. Инверсный пистолет Героя 2 будет вместе с предметами.

АУРА ГЕРОЯ 2
  GetComponent<HeroAura>().SetAura(true);
  Призматический ореол за телом + радужная кромка по контуру, мерцает и «барахлит».
  Сила сдвигов и скорость переливов настраиваются в инспекторе HeroAura.

СВОИ АНИМАЦИИ
  Окно Animation -> выбрать Hero -> создать клип -> крутить кости
  (ArmN_Upper, LegF_Lower и т.д., N — ближняя сторона, F — дальняя).
  Для «отбиться» и «спрятаться» анимации добавлю, когда решим механику.

Старые папки idle / walk / shoot с лентами кадров больше не нужны.
