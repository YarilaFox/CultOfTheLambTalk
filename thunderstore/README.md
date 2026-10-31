# Follower Talk

Talk to followers through Cult of the Lamb's dialogue UI. Each follower can remember recent exchanges, respond according to their current state, and attempt supported in-game actions when asked.

## Setup

1. Install the package with a compatible mod manager. BepInEx and COTL_API are declared as dependencies.
2. Open the game's **Settings > Mods > Conversations** menu. Choose OpenAI API or Gemini API, a model, and the reply and interface languages.
3. Open **Settings** in that menu (or press F8), paste your API key, and save. The key is masked by default and stored locally for the current Windows user.
4. Choose **Talk** when interacting with a follower. Enter sends a message; Esc ends the conversation.

API subscriptions and ChatGPT/Gemini chat subscriptions are separate. Model calls may incur API charges. The mod does not include an API key or send messages without one.

## Notes

- Interface text supports the game's languages. Reply language can follow the player's message or be set manually.
- Supported actions use game tasks and availability checks; followers cannot perform arbitrary game actions.
- Memories, API keys, and settings stay in the local `BepInEx/config` folder. Do not share that folder when reporting bugs.
- For manual installation, troubleshooting, and source inspection, see [GitHub](https://github.com/YarilaFox/CultOfTheLambTalk).
