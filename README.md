# Follower Talk

<a id="english"></a>
<details open>
<summary><strong>English</strong></summary>

**[Download version 0.5.2](https://github.com/YarilaFox/CultOfTheLambTalk/releases/tag/v0.5.2)** · The source is public for inspection, but it is not open source. See [LICENSE](LICENSE) for the terms of use.

## Installation

Download `FollowerTalk-0.5.2.zip` from the release page. Exit the game, install BepInExPack_CultOfTheLamb 5.4.2101, then extract the ZIP into the game's root directory. The archive already includes COTL_API 0.3.4 and its assets; you do not need to install it separately. Detailed manual instructions are in `INSTALL_RU.md` inside the ZIP. The archive does not contain API keys or BepInEx. The COTL_API license is included separately.

The mod adds a **Talk** command when interacting with a follower. Input and replies appear in the game's normal dialogue UI: you type directly into the dialogue bubble, and the follower replies there and may choose a supported game action. Enter sends, Ctrl+V pastes, and input opens again after each reply. Esc ends the conversation without opening the pause menu. The camera moves between the follower and the Lamb without unintended zoom changes.

## Configuration

- [Setup and troubleshooting with screenshots](docs/SETUP.md).
- Open the game's **Settings > Mods > Conversations** menu. Choose the provider, preset models, reply language, and interface language there.
- **Settings** opens a small editor for the API key and a custom model ID. You can also open it with F8.
- Paste the key with Ctrl+V. It is masked until you press the eye button. Each provider has its own key.
- Providers: OpenAI, Gemini, Claude, Grok, OpenCode Console, DeepSeek, OpenClaw Gateway, Hermes Agent, and a custom OpenAI-compatible endpoint. Select a provider with the arrows in the Connection tab, then enter that provider's model ID and key. OpenClaw, Hermes, and custom endpoints also need their API base URL (ending in `/v1`). A custom local endpoint may have no key.
- OpenCode here means the [Console inference API](https://opencode.ai/v2/docs/console/api/inference), not the OpenCode desktop login. Its free chat models are not available to external API clients. OpenClaw's Chat Completions endpoint must be enabled in its gateway config; Hermes needs its API server enabled. Both agent gateways may run their own tools, so use only a gateway you trust and configure its permissions before connecting a game conversation.
- Choose a preset model or enter its ID manually, then press **Save**. Hosted providers require a key; a custom local endpoint can work without one. API pricing and limits are separate from subscriptions to chat apps.
- Reply and interface languages are independent. **Auto** replies in the language of the player's latest message; **Game language** follows Cult of the Lamb's language setting. The mod's interface supports all 14 game languages, and you can select one manually.
- Keys are stored in `BepInEx/config/FollowerTalk.keys.json`, encrypted for the current Windows user. The BepInEx configuration stores only provider and model names. The mod does not write the key to its log.

## Actions and memory

The model returns a reply and one action from the current catalog. The catalog is built from available in-game tasks nearby, including prayer, construction, farming, lumber work, cooking, cleaning, fishing, and other jobs. Sleep and eating are supported when the necessary conditions are met. A general request to work is mapped to a specific available task; a request to farm is not silently replaced with a different job. The model receives information about the availability of farm facilities and tasks.

The tithe action uses the game's normal animation and coin transfer, then reopens the conversation. It is available only after unlocking the doctrine and at most once per in-game day. For construction, the mod assigns a worker role and gives building work priority; the game then chooses the task.

If the model promises an unavailable action, the mod reports that game observation back to it and asks for a new reply in its own words. It does not substitute a canned line for the follower. An accepted task starts after the conversation ends and can be canceled with another message while the conversation is still open. On closing the dialogue, the follower leaves manual-control mode even if the task was unavailable or the API did not respond. Availability is checked again before assignment. Resources are not granted instantly: the follower has to do the work. Actions without a safe in-game task, including destructive commands, are not executed; the follower may discuss them but should not promise to carry them out.

The last five pairs of messages and up to eight important notes are stored per follower ID and save slot in `BepInEx/config/FollowerTalk.memory.*.json`. Notes are reserved for meaningful thoughts or promises and retain the words they came from. When the altar menu is open, **Address everyone** stores the player's message as a cult-wide announcement for future conversations. An explicit request such as "Call me ..." changes the preferred form of address in later dialogue; it does not rename the player in the game.

Before each reply, the model receives that follower's current faith, dissent, mood, hunger, rest, illness, traits, active thoughts, and activity. These are current game states, not fixed personality traits. Ritual praise is not required. Voice input is not implemented yet.

## Loyalty

The mod does not add a separate trust meter. The model sees the follower's existing in-game loyalty level. If a follower asks the Lamb to build a lumberyard or farming station, the mod records the number of such buildings. When a new one appears, the game gives that follower the normal reward for a fulfilled personal request through `AddAdoration(Quest)`. The same request is not rewarded twice. Pending requests are stored per save slot and follower ID in `BepInEx/config/FollowerTalk.requests.json`. Ordinary conversation alone does not change loyalty.

The model may also end a conversation before carrying out an available task. Its reply appears first; pressing **Continue** closes the dialogue and assigns the action. "Follow me" enables the game's follower-following state after the dialogue closes. "Stop following me" or canceling the task disables it; urgent in-game needs may interrupt following.

## Building

You need Cult of the Lamb, BepInExPack_CultOfTheLamb 5.4.2101, and COTL_API 0.3.4. Put `BepInEx.dll` and `COTL_API.dll` in `deps/`, then run `dotnet build -c Release`. You can override the game path with the MSBuild `GameDir` property. Place the compiled `bin/Release/netstandard2.1/FollowerTalk.dll` in the game's `BepInEx/plugins/FollowerTalk/` directory.

To remove only this mod, delete `BepInEx/plugins/FollowerTalk/`. Do not remove COTL_API or BepInEx if other mods use them.

</details>

<a id="russian"></a>
<details>
<summary><strong>Русский</strong></summary>

**[Скачать версию 0.5.2](https://github.com/YarilaFox/CultOfTheLambTalk/releases/tag/v0.5.2)** · Исходный код доступен для ознакомления, но не является open source: условия использования см. в [LICENSE](LICENSE).

## Установка

Скачайте `FollowerTalk-0.5.2.zip` на странице релиза. После выхода из игры
установите BepInExPack_CultOfTheLamb 5.4.2101, затем распакуйте ZIP в корневую
папку игры. В архиве уже есть COTL_API 0.3.4 со всеми его файлами; отдельно
ставить его не нужно. Подробные шаги есть в `INSTALL_RU.md` внутри ZIP.
В архиве нет API-ключей или BepInEx. Лицензия COTL_API включена отдельно.

Мод добавляет пункт «Поговорить» при взаимодействии с последователем. Ввод и
ответ находятся в обычном игровом диалоге: игрок печатает прямо в его облаке,
последователь отвечает там же и при необходимости выбирает игровое действие.
Enter отправляет текст, Ctrl+V вставляет, после ответа в том же диалоге снова
открывается ввод следующей реплики; Esc завершает беседу без меню паузы.
При смене говорящего камера плавно переводится между последователем и игроком
без случайного изменения масштаба.

## Настройка

- [Setup and troubleshooting with screenshots (English)](docs/SETUP.md).
- В настройках игры откройте вкладку Mods и категорию «Разговоры». Провайдер,
  готовые модели и оба языка меняются прямо в строках игрового меню.
- Пункт «Настроить ключ и модель» открывает небольшое окно для API-ключа и
  произвольного ID модели. Его также можно открыть по F8.
- Вставьте ключ в поле через Ctrl+V. Пока глаз закрыт, ключ отображается
  точками. Для каждого провайдера хранится отдельный ключ.
- Доступны OpenAI, Gemini, Claude, Grok, OpenCode Console, DeepSeek, OpenClaw
  Gateway, Hermes Agent и произвольный OpenAI-совместимый API. В разделе
  «Подключение» переключите провайдера стрелками, укажите ID модели и его ключ.
  Для OpenClaw, Hermes и собственного API укажите базовый URL с `/v1`.
  Локальный совместимый API может работать без ключа.
- OpenCode здесь означает Console inference API, а не вход через приложение;
  бесплатные модели для внешнего API недоступны. Для OpenClaw включите HTTP
  Chat Completions, для Hermes - API server. Эти агенты могут выполнять свои
  инструменты, поэтому подключайте только доверенный шлюз с настроенными правами.
- Выберите готовую модель в игровом меню или впишите её ID в окне, затем нажмите
  «Сохранить».
  Для облачных провайдеров требуется ключ; локальный совместимый API может
  работать без него. Тарифы и лимиты API не связаны с подписками на чат-приложения.
- Язык ответов и язык интерфейса выбираются отдельно.
  «Авто» для ответов следует языку последней реплики игрока, а «Язык игры» для
  интерфейса следует настройке Cult of the Lamb. Интерфейс мода переведён на все
  14 языков игры; его язык можно закрепить вручную в той же категории Mods.
- Ключи лежат в `BepInEx/config/FollowerTalk.keys.json`, зашифрованные для
  текущего пользователя Windows. В BepInEx-конфиге сохраняются только названия
  провайдера и моделей. Ключ не пишется в журнал мода.

## Действия

Модель возвращает реплику и одно действие из текущего каталога. Каталог
собирается из доступных игровых заданий поблизости: молитва, строительство,
ферма, рубка, готовка, уборка, рыбалка и другие виды работы. Дополнительно
поддержаны сон и еда при наличии готового блюда. Общую просьбу поработать модель
связывает с конкретным доступным заданием; просьба о ферме не превращается в
другую работу. Модель получает сведения о доступности фермерской станции и задач.
Сбор податей использует штатную игровую анимацию и выдачу монет. После неё
разговор открывается снова. Команда доступна только после открытия доктрины,
не более одного раза за игровой день. Для строительства
мод переводит последователя в рабочую роль и ставит строительный приоритет;
дальше задачу выбирает игровая система.
Если она пообещала недоступное действие, мод возвращает ей игровое наблюдение и
просит ответить заново собственными словами. Готовые реплики от имени
последователя не подставляются. Поручение запускается после выхода из разговора;
его можно отменить следующей репликой, пока беседа ещё идёт.
При закрытии беседы мод возвращает последователя из режима ручного управления,
даже если работа была недоступна или ответ API не пришёл. Перед назначением мод
снова проверяет доступность. Ресурсы не
начисляются мгновенно: последователь должен выполнить назначенное задание.
Действия без безопасного игрового задания, включая разрушительные команды,
не выполняются: последователь может обсудить их, но не должен обещать исполнение.
Пять последних пар реплик и до восьми важных заметок сохраняются отдельно по
ID последователя и сохранения игры в `BepInEx/config/FollowerTalk.memory.*.json`.
Заметки добавляются только для значимых мыслей или обещаний и содержат слова,
из которых они получены. При открытом меню алтаря кнопка «Сказать всем»
сохраняет слова игрока для всего культа. Явное «Называйте меня ...» меняет
предпочтительное обращение в будущих беседах, но не переименовывает персонажа
в самой игре. Перед каждым ответом модель получает текущие веру, недовольство,
настроение, сытость, отдых, болезнь, черты, активные мысли и занятие данного
последователя. Это текущие игровые показатели, а не постоянные черты характера;
ритуальная похвала не задана как обязательная реплика. Голосовой ввод пока не
подключён.

## Верность

Мод не создаёт отдельную шкалу доверия. Модель видит существующий в игре
уровень верности последователя. Если он сам попросил построить лесопилку или
фермерскую станцию, мод запоминает число построек. После появления новой
постройки игра начисляет этому последователю обычную награду за выполненную
личную просьбу через `AddAdoration(Quest)`. Повторной награды за ту же просьбу
нет. Ожидающие просьбы сохраняются по слоту игры и ID последователя в
`BepInEx/config/FollowerTalk.requests.json`. Обычная беседа сама по себе
верность не повышает и не снижает.

Модель также может закончить беседу перед выполнением доступного поручения.
Сначала показывается её реплика; после нажатия «дальше» разговор закрывается и
игра получает выбранное действие.
Просьба «иди за мной» включает игровое следование
за Ягнёнком после закрытия диалога. «Перестань идти за мной» или отмена
поручения выключает его; срочные потребности игры могут прервать следование.

## Сборка

Нужны Cult of the Lamb, BepInExPack_CultOfTheLamb 5.4.2101 и COTL_API 0.3.4.
Положите `BepInEx.dll` и `COTL_API.dll` в `deps/`, затем выполните
`dotnet build -c Release`. Путь к игре можно передать свойством MSBuild
`GameDir`. Собранный `bin/Release/netstandard2.1/FollowerTalk.dll` должен
находиться в `BepInEx/plugins/FollowerTalk/` игры.

Чтобы убрать только этот мод, удалите `BepInEx/plugins/FollowerTalk/`.
Не удаляйте COTL_API или BepInEx, если они нужны другим модам.

</details>
