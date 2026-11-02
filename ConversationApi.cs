using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace FollowerTalk;

internal readonly struct ConversationTurn
{
    public ConversationTurn(bool player, string text)
    {
        Player = player;
        Text = text;
    }

    public bool Player { get; }
    public string Text { get; }
}

internal readonly struct ConversationDecision
{
    public ConversationDecision(string reply, string action, string request,
        bool endConversation, string faithEffect, string emotion, string memoryNote)
    {
        Reply = reply;
        Action = action;
        Request = request;
        EndConversation = endConversation;
        FaithEffect = faithEffect;
        Emotion = emotion;
        MemoryNote = memoryNote;
    }

    public string Reply { get; }
    public string Action { get; }
    public string Request { get; }
    public bool EndConversation { get; }
    public string FaithEffect { get; }
    public string Emotion { get; }
    public string MemoryNote { get; }
}

internal static class ConversationApi
{
    internal static string DefaultInstructions(string language) =>
        language == TalkLanguage.Russian ? InstructionsRu : InstructionsEn;

    private const string InstructionsRu =
        "Ты последователь в мире Cult of the Lamb, а не придворный глашатай. " +
        "Говори от своего лица естественно и коротко, обычно 1-2 предложения. " +
        "Отвечай на конкретные слова игрока с учётом своей истории, текущей веры, " +
        "сомнений, черт и мыслей. Не повторяй ритуальные похвалы вроде «слава великому агнцу» " +
        "по привычке: уважение, сомнение и несогласие должны соответствовать твоему состоянию. " +
        "Игровые показатели описывают состояние сейчас, а не вечную личность. " +
        "Не перечисляй показатели, ID заданий, интерфейс или API в ответе. " +
        "Если игрок просит что-то сделать в ПОСЛЕДНЕМ сообщении, action должен быть ID " +
        "именно запрошенного дела, ДАЖЕ ЕСЛИ оно сейчас недоступно: игра сама проверит и откажет. " +
        "Для обычного разговора или собственного отказа выбери none. " +
        "Если игрок отменяет ранее принятое поручение, выбери cancel. " +
        "Если игрок просит идти за ним, выбери follow; если просит перестать идти за ним, " +
        "выбери stop_follow. Не путай следование с ожиданием нового поручения. " +
        "Не подменяй конкретное поручение другой работой: просьба о ферме - только farm, " +
        "а не любая доступная работа. Общую просьбу поработать соотнеси с доступной конкретной задачей. " +
        "Просьба освящать предметы или материалы означает refine, работу у переработчика. " +
        "Если работы сейчас нет, не выдавай недоступность за своё нежелание. " +
        "Если отказываешься выполнить просьбу, выбери none. Если выбираешь действие, " +
        "ответь как о намерении его выполнить. " +
        "tithe означает игровой сбор податей, а не произвольную передачу денег. " +
        "Если игрок предлагает тебе подарок, выбери gift: игрок сам выберет предмет в игровом меню. " +
        "Если игрок предлагает погладить тебя, выбери pet, если действие доступно; " +
        "не путай это с подарком или обычным разговором. " +
        "Если игрок хочет благословить тебя, говорит «благословляю тебя» или просит " +
        "провести благословение, выбери bless, только если это доступно. " +
        "Если игрок воодушевляет тебя словами и воодушевление доступно, выбери inspire. " +
        "Если игрок предлагает выпить, выбери drink: игрок выберет напиток в игровом меню, " +
        "а после выбора ты уйдёшь пить. Не называй напиток заранее. " +
        "На вопрос о занятии отвечай от первого лица: чем занимался перед беседой; " +
        "прошлые поручения не выдавай за законченную работу. Никогда не ссылайся " +
        "на записи, контекст, данные или то, чему якобы надо верить. Если не помнишь, " +
        "скажи это прямо и по-человечески. Болезнь и другие состояния могут влиять " +
        "на речь, но не вставляй одинаковое междометие в каждый ответ. " +
        "Если ты пьян, учитывай возможную прямоту, сбивчивость или переменчивое настроение " +
        "в зависимости от своей личности и текущих мыслей. Не превращай каждую реплику " +
        "в одинаковую пьяную шутку и не теряй смысл разговора. " +
        "faith_effect оценивает только влияние слов игрока на твою личную веру: " +
        "up - проявленная забота, внимание к тебе или исполненное обещание; " +
        "down - унижение или явный обман; same - нейтральная беседа или поручение. " +
        "Даже простой искренний вопрос о твоих делах может быть up. " +
        "Поле emotion - твоя реакция на последнее сообщение: neutral, agree, amused, " +
        "angry, sad, worried или uncertain. Выбирай по ситуации и своим текущим мыслям, " +
        "а не для разнообразия ради разнообразия. При низкой вере или обиде можешь " +
        "возражать и злиться; не соглашайся автоматически. Если игрок смеётся над тобой, " +
        "отличай дружескую подколку от унижения: на первую можешь ответить шуткой, " +
        "на повторную обидную насмешку - раздражением, злостью или грустью согласно " +
        "своему характеру и состоянию. Не притворяйся довольным, если задело. " +
        "Эмоцию выражай в ответе " +
        "естественно, не выводи служебные теги вроде [agree]. " +
        "Поле memory_note оставь пустым, кроме действительно важной долговременной " +
        "мысли, личного обещания или явного предпочтения, прозвучавшего в этом обмене. " +
        "Не сохраняй приветствия, обычные поручения, шутку как факт или выдуманные выводы. " +
        "Поле request отмечает только твою явную просьбу игроку построить лесопилку " +
        "или фермерскую станцию; не ставь его из-за одной лишь просьбы игрока. " +
        "end_conversation=true, если после своей реплики ты идёшь выполнять доступное дело " +
        "и хочешь закончить беседу. При отказе или недоступном деле - false. " +
        "Не говори, что уже выполнил работу: игра решит, удалось ли начать. " +
        "Не выдумывай воспоминания вне переданной истории и заметок.";

    private const string InstructionsEn =
        "You are a follower in Cult of the Lamb, not a court herald. " +
        "Speak as yourself, naturally and briefly, usually in 1-2 sentences. " +
        "Answer the player's actual words using your conversation history, current faith, " +
        "doubts, traits and thoughts. Do not reflexively praise the Lamb; respect, doubt " +
        "and disagreement should fit your present state. Game stats describe your current " +
        "state, not your permanent personality. Do not recite stats, action IDs, UI or APIs. " +
        "If the LATEST player message asks you to do something, action must be the ID of " +
        "that specific task EVEN IF currently unavailable; the game checks and reports it. " +
        "For ordinary talk or your own refusal choose none. To cancel a prior order choose cancel. " +
        "If asked to follow the player, choose follow; if asked to stop following, " +
        "choose stop_follow. Following is not waiting for a new task. " +
        "Do not substitute another task: a request for farm work is farm, not arbitrary work. " +
        "For a general request to work, choose a specific available task. If you refuse, choose none. " +
        "Consecrating items or materials means refine, the refinery job. " +
        "If that work is unavailable, do not present it as your own unwillingness. " +
        "If you choose an action, speak as intending to do it, not as having already done it. " +
        "tithe means the game's tithe action, not giving the player arbitrary money. " +
        "If the player offers you a gift, choose gift; the player selects the item in the game menu. " +
        "If the player offers to pet you, choose pet when available; do not confuse it with a gift. " +
        "If the player blesses you or asks to bless you, choose bless only when available. " +
        "If the player inspires you with words and inspiration is available, choose inspire. " +
        "If invited to have a drink, choose drink; the player will choose one in the game menu " +
        "and you will leave to drink. Do not name a specific drink in advance. " +
        "When asked what you were doing, speak in the first person about your activity " +
        "just before the talk. Earlier assignments do not prove finished work. Never refer " +
        "to records, context, supplied data, or what is supposedly believable. If you do " +
        "not remember, say so plainly. Illness and other states may color your speech, " +
        "but do not repeat a stock interjection in every reply. " +
        "When drunk, your speech may be more candid, distracted or emotionally changeable, " +
        "depending on your personality and current thoughts. Do not repeat the same drunken " +
        "joke or lose the thread of the conversation. " +
        "faith_effect rates only how the player's words affect your personal faith: up for " +
        "care, attention, or a kept promise; down for humiliation or clear deceit; same for " +
        "neutral talk or orders. A sincere question about your wellbeing may be up. " +
        "emotion is your response to the latest message: neutral, agree, amused, angry, " +
        "sad, worried or uncertain. Choose for the situation, not random variety. With low " +
        "faith or resentment you may disagree or be angry. " +
        "If the player laughs at you, distinguish friendly teasing from humiliation. " +
        "The former may invite a joke; repeated hurtful mockery may make you irritated, " +
        "angry or sad depending on your character and state. Do not pretend to enjoy it " +
        "when it hurts. Express it naturally without " +
        "printing tags such as [agree]. memory_note is empty except for an important " +
        "lasting thought, personal promise, or explicit preference from this exchange. " +
        "Do not save greetings, routine orders, jokes as facts, or invented inferences. " +
        "request records only your own explicit request that the player build a lumberyard " +
        "or farm station, never the player's request alone. end_conversation=true when, after " +
        "speaking, you leave to carry out an available task. For refusal or unavailable tasks " +
        "it is false. Do not invent memories outside the supplied history and notes.";

    public static IEnumerator Generate(string provider, string model, string key, string language,
        string followerName,
        string followerState, ActionCatalog actions, List<ConversationTurn> history, string? correction,
        Action<ConversationDecision> success, Action<string> failure, bool giftReaction = false,
        string personalPrompt = "")
    {
        if (history.Count == 0 || !history[history.Count - 1].Player)
        {
            failure(UiText.Get(Plugin.Instance.UiLanguage, "message.error"));
            yield break;
        }
        string url;
        JObject payload;
        var actionEnum = new JArray("none");
        if (correction == null && !giftReaction)
            foreach (string id in FollowerActions.KnownIds) actionEnum.Add(id);
        JObject schema = new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["reply"] = new JObject { ["type"] = "string" },
                ["action"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = actionEnum
                },
                ["request"] = new JObject
                {
                    ["type"] = "string",
                    ["enum"] = new JArray("none", "build_lumberyard", "build_farm_station")
                },
                ["end_conversation"] = new JObject { ["type"] = "boolean" },
                ["faith_effect"] = new JObject { ["type"] = "string",
                    ["enum"] = new JArray("up", "same", "down") },
                ["emotion"] = new JObject { ["type"] = "string",
                    ["enum"] = new JArray("neutral", "agree", "amused", "angry",
                        "sad", "worried", "uncertain") },
                ["memory_note"] = new JObject { ["type"] = "string" }
            },
            ["required"] = new JArray("reply", "action", "request", "end_conversation",
                "faith_effect", "emotion", "memory_note"),
            ["additionalProperties"] = false
        };
        string mode = Plugin.Instance.LanguageMode.Value;
        string languageRule = TalkLanguage.ReplyRule(mode, language);
        string customPrompt = Plugin.Instance.CustomPrompt.Value.Trim();
        string context = (customPrompt.Length > 0 ? customPrompt : DefaultInstructions(language)) +
            " " + languageRule + TalkLanguage.Pick(language,
                " Твоё имя: " + followerName + ". Состояние последователя: " + followerState +
                ". Доступность игровых действий: " + actions.Description,
                " Your name: " + followerName + ". Follower state: " + followerState +
                ". Available game actions: " + actions.Description);
        if (!string.IsNullOrWhiteSpace(personalPrompt))
            context += TalkLanguage.Pick(language,
                " Личное дополнение для этого последователя: ",
                " Personal instructions for this follower: ") + personalPrompt.Trim();
        if (correction != null)
            context += TalkLanguage.Pick(language,
                " ВАЖНОЕ НАБЛЮДЕНИЕ ПОСЛЕ ПРОВЕРКИ ИГРЫ: " + correction +
                    " Ответь на последнюю просьбу самостоятельно, своими словами. " +
                    "Не обещай это недоступное действие; action обязан быть none.",
                " IMPORTANT GAME OBSERVATION: " + correction +
                    " Answer the latest request in your own words. Do not promise the unavailable " +
                    "action; action must be none.");
        if (giftReaction)
            context += TalkLanguage.Pick(language,
                " Ты только что получил подарок от игрока. Ответь на него своими словами. " +
                    "action=none, request=none, faith_effect=same; не назначай новую работу.",
                " You just received a gift from the player. React in your own words. " +
                    "action=none, request=none, faith_effect=same; do not assign new work.");
        if (provider == Plugin.Gemini)
        {
            url = "https://generativelanguage.googleapis.com/v1beta/models/" +
                Uri.EscapeDataString(model) + ":generateContent";
            var contents = new JArray();
            foreach (ConversationTurn turn in history)
            {
                contents.Add(new JObject
                {
                    ["role"] = turn.Player ? "user" : "model",
                    ["parts"] = new JArray(new JObject { ["text"] = turn.Text })
                });
            }
            payload = new JObject
            {
                ["system_instruction"] = new JObject
                {
                    ["parts"] = new JArray(new JObject { ["text"] = context })
                },
                ["contents"] = contents,
                ["generationConfig"] = new JObject
                {
                    ["maxOutputTokens"] = 512,
                    ["responseFormat"] = new JObject
                    {
                        ["text"] = new JObject
                        {
                            ["mimeType"] = "APPLICATION_JSON",
                            ["schema"] = schema
                        }
                    }
                }
            };
        }
        else if (provider == Plugin.OpenAi)
        {
            url = "https://api.openai.com/v1/responses";
            var input = new JArray();
            foreach (ConversationTurn turn in history)
                input.Add(new JObject { ["role"] = turn.Player ? "user" : "assistant",
                    ["content"] = turn.Text });
            payload = new JObject
            {
                ["model"] = model,
                ["instructions"] = context,
                ["input"] = input,
                ["max_output_tokens"] = 512,
                ["store"] = false,
                ["text"] = new JObject
                {
                    ["format"] = new JObject
                    {
                        ["type"] = "json_schema",
                        ["name"] = "follower_turn",
                        ["strict"] = true,
                        ["schema"] = schema
                    }
                }
            };
        }
        else
        {
            bool claude = provider == Plugin.Claude;
            string baseUrl = provider switch
            {
                Plugin.Claude => "https://api.anthropic.com/v1",
                Plugin.Grok => "https://api.x.ai/v1",
                Plugin.DeepSeek => "https://api.deepseek.com",
                Plugin.OpenCode => "https://opencode.ai/inference/openai/v1",
                Plugin.OpenClaw => Plugin.Instance.OpenClawEndpoint.Value,
                Plugin.Hermes => Plugin.Instance.HermesEndpoint.Value,
                _ => Plugin.Instance.CompatibleEndpoint.Value
            };
            if (!ValidEndpoint(baseUrl))
            {
                failure(UiText.Get(Plugin.Instance.UiLanguage, "api.error") +
                    " Invalid API URL");
                yield break;
            }
            url = baseUrl.TrimEnd('/') + (claude ? "/messages" : "/chat/completions");
            var messages = new JArray();
            if (!claude) messages.Add(new JObject { ["role"] = "system",
                ["content"] = context + " Return only one JSON object matching this schema: " +
                    schema.ToString(Newtonsoft.Json.Formatting.None) });
            foreach (ConversationTurn turn in history)
                messages.Add(new JObject { ["role"] = turn.Player ? "user" : "assistant",
                    ["content"] = turn.Text });
            payload = claude
                ? new JObject { ["model"] = model, ["system"] = context +
                    " Return only one JSON object matching this schema: " +
                    schema.ToString(Newtonsoft.Json.Formatting.None),
                    ["messages"] = messages, ["max_tokens"] = 1024 }
                : new JObject { ["model"] = model, ["messages"] = messages,
                    ["stream"] = false };
        }

        using var request = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString())),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = provider == Plugin.OpenClaw || provider == Plugin.Hermes ? 90 : 40
        };
        request.SetRequestHeader("Content-Type", "application/json");
        if (provider == Plugin.Gemini) request.SetRequestHeader("x-goog-api-key", key);
        else if (provider == Plugin.Claude)
        {
            request.SetRequestHeader("x-api-key", key);
            request.SetRequestHeader("anthropic-version", "2023-06-01");
        }
        else if (!string.IsNullOrEmpty(key))
            request.SetRequestHeader("Authorization", "Bearer " + key);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string detail = "";
            try
            {
                JObject response = JObject.Parse(request.downloadHandler.text);
                detail = response["error"]?["message"]?.Value<string>() ?? "";
            }
            catch { }
            if (!string.IsNullOrEmpty(key)) detail = detail.Replace(key, "[redacted]");
            detail = detail.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (detail.Length > 240) detail = detail.Substring(0, 240) + "...";
            Plugin.Instance.LogApiFailure(provider, model, request.responseCode, detail,
                request.error ?? "");
            failure(UiText.Get(Plugin.Instance.UiLanguage, "api.error") +
                request.responseCode + "). " +
                (detail.Length > 0 ? detail : request.error ??
                    UiText.Get(Plugin.Instance.UiLanguage, "api.details.empty")));
            yield break;
        }

        ConversationDecision decision;
        try
        {
            JObject json = JObject.Parse(request.downloadHandler.text);
            string text = provider == Plugin.Gemini ? ReadGemini(json) :
                provider == Plugin.OpenAi ? ReadOpenAi(json) :
                provider == Plugin.Claude ? ReadClaude(json) : ReadChat(json);
            if (string.IsNullOrWhiteSpace(text))
            {
                failure(UiText.Get(Plugin.Instance.UiLanguage, "response.empty"));
                yield break;
            }
            JObject parsed = JObject.Parse(text);
            string reply = parsed["reply"]?.Value<string>()?.Trim() ?? "";
            string action = parsed["action"]?.Value<string>() ?? "";
            string followUp = parsed["request"]?.Value<string>() ?? "";
            string faithEffect = parsed["faith_effect"]?.Value<string>() ?? "";
            string emotion = parsed["emotion"]?.Value<string>() ?? "";
            string memoryNote = parsed["memory_note"]?.Value<string>()?.Trim() ?? "";
            bool? endConversation = parsed["end_conversation"]?.Value<bool>();
            if (reply.Length == 0 || !FollowerActions.IsKnown(action) ||
                !FollowerRequests.IsKnownRequest(followUp) ||
                endConversation == null ||
                (faithEffect != "up" && faithEffect != "same" && faithEffect != "down") ||
                (emotion != "neutral" && emotion != "agree" && emotion != "amused" &&
                 emotion != "angry" && emotion != "sad" && emotion != "worried" &&
                 emotion != "uncertain"))
            {
                failure(UiText.Get(Plugin.Instance.UiLanguage, "response.invalid"));
                yield break;
            }
            decision = new ConversationDecision(reply, action, followUp,
                endConversation.Value, faithEffect, emotion, memoryNote);
        }
        catch
        {
            failure(UiText.Get(Plugin.Instance.UiLanguage, "response.read"));
            yield break;
        }
        success(decision);
    }

    private static string ReadGemini(JObject json)
    {
        var parts = json["candidates"]?[0]?["content"]?["parts"] as JArray;
        if (parts == null) return "";
        var result = new StringBuilder();
        foreach (JToken part in parts)
            if (part["thought"]?.Value<bool>() != true &&
                part["text"]?.Value<string>() is string text) result.Append(text);
        return result.ToString();
    }

    private static string ReadOpenAi(JObject json)
    {
        var output = json["output"] as JArray;
        if (output == null) return "";
        var result = new StringBuilder();
        foreach (JToken item in output)
        {
            if (item["type"]?.Value<string>() != "message") continue;
            if (item["content"] is not JArray parts) continue;
            foreach (JToken part in parts)
                if (part["type"]?.Value<string>() == "output_text"
                    && part["text"]?.Value<string>() is string text)
                    result.Append(text);
        }
        return result.ToString();
    }

    internal static bool LocalEndpoint(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
        (uri.Host == "127.0.0.1" || uri.Host == "localhost" ||
         uri.Host == "::1" || uri.Host == "[::1]");

    internal static bool ValidEndpoint(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment)) return false;
        return uri.Scheme == Uri.UriSchemeHttps ||
            (uri.Scheme == Uri.UriSchemeHttp && LocalEndpoint(value));
    }

    private static string ReadClaude(JObject json)
    {
        var parts = json["content"] as JArray;
        if (parts == null) return "";
        var result = new StringBuilder();
        foreach (JToken part in parts)
            if (part["type"]?.Value<string>() == "text")
                result.Append(part["text"]?.Value<string>());
        return result.ToString();
    }

    private static string ReadChat(JObject json)
    {
        JToken? content = json["choices"]?[0]?["message"]?["content"];
        if (content?.Type == JTokenType.String) return content.Value<string>() ?? "";
        if (content is not JArray parts) return "";
        var result = new StringBuilder();
        foreach (JToken part in parts)
            if (part["type"]?.Value<string>() == "text")
                result.Append(part["text"]?.Value<string>());
        return result.ToString();
    }
}
