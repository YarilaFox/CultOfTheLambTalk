# Follower Talk

[Русский](README.md) | **English**

**[Download version 0.5.0](https://github.com/YarilaFox/CultOfTheLambTalk/releases/tag/v0.5.0)** · The source is public for inspection, but it is not open source. See [LICENSE](LICENSE) for the terms of use.

## Installation

Download `FollowerTalk-0.5.0.zip` from the release page. Exit the game, install BepInExPack_CultOfTheLamb 5.4.2101, then extract the ZIP into the game's root directory. The archive already includes COTL_API 0.3.4 and its assets; you do not need to install it separately. Detailed manual instructions are in `INSTALL_RU.md` inside the ZIP. The archive does not contain API keys or BepInEx. The COTL_API license is included separately.

The mod adds a **Talk** command when interacting with a follower. Input and replies appear in the game's normal dialogue UI: you type directly into the dialogue bubble, and the follower replies there and may choose a supported game action. Enter sends, Ctrl+V pastes, and input opens again after each reply. Esc ends the conversation without opening the pause menu. The camera moves between the follower and the Lamb without unintended zoom changes.

## Configuration

- Open the game's **Settings > Mods > Conversations** menu. Choose the provider, preset models, reply language, and interface language there.
- **Settings** opens a small editor for the API key and a custom model ID. You can also open it with F8.
- Paste the key with Ctrl+V. It is masked until you press the eye button. Each provider has its own key.
- Choose a preset model or enter its ID manually, then press **Save**. No network request is made without a key. OpenAI API and Gemini API have their own pricing and limits, separate from subscriptions to their chat apps.
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
