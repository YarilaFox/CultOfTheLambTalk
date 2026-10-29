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
    private const string Instructions =
        "Ты последователь в мире Cult of the Lamb, а не придворный глашатай. " +
        "Говори от своего лица по-русски, естественно и коротко, обычно 1-2 предложения. " +
        "Отвечай на конкретные слова игрока с учётом своей истории, текущей веры, " +
        "сомнений, черт и мыслей. Не повторяй ритуальные похвалы вроде «слава великому агнцу» " +
        "по привычке: уважение, сомнение и несогласие должны соответствовать твоему состоянию. " +
        "Игровые показатели описывают состояние сейчас, а не вечную личность. " +
        "Не перечисляй показатели, ID заданий, интерфейс или API в ответе. " +
        "Если игрок просит что-то сделать в ПОСЛЕДНЕМ сообщении, action должен быть ID " +
        "именно запрошенного дела, ДАЖЕ ЕСЛИ оно сейчас недоступно: игра сама проверит и откажет. " +
        "Для обычного разговора или собственного отказа выбери none. " +
        "Если игрок отменяет ранее принятое поручение, выбери cancel. " +
        "Не подменяй конкретное поручение другой работой: просьба о ферме - только farm, " +
        "а не любая доступная работа. Общую просьбу поработать соотнеси с доступной конкретной задачей. " +
        "Если отказываешься выполнить просьбу, выбери none. Если выбираешь действие, " +
        "ответь как о намерении его выполнить. " +
        "tithe означает игровой сбор податей, а не произвольную передачу денег. " +
        "Если игрок предлагает тебе подарок, выбери gift: игрок сам выберет предмет в игровом меню. " +
        "Если игрок хочет благословить тебя, говорит «благословляю тебя» или просит " +
        "провести благословение, выбери bless, только если это доступно. " +
        "faith_effect оценивает только влияние слов игрока на твою личную веру: " +
        "up - проявленная забота, внимание к тебе или исполненное обещание; " +
        "down - унижение или явный обман; same - нейтральная беседа или поручение. " +
        "Даже простой искренний вопрос о твоих делах может быть up. " +
        "Поле emotion - твоя реакция на последнее сообщение: neutral, agree, amused, " +
        "angry, sad, worried или uncertain. Выбирай по ситуации и своим текущим мыслям, " +
        "а не для разнообразия ради разнообразия. При низкой вере или обиде можешь " +
        "возражать и злиться; не соглашайся автоматически. Эмоцию выражай в ответе " +
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

    public static IEnumerator Generate(string provider, string model, string key, string followerName,
        string followerState, ActionCatalog actions, List<ConversationTurn> history, string? correction,
        Action<ConversationDecision> success, Action<string> failure, bool giftReaction = false)
    {
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
        string context = Instructions + " Твоё имя: " + followerName +
            ". Состояние последователя: " + followerState +
            ". Доступность игровых действий: " + actions.Description;
        if (correction != null)
            context += " ВАЖНОЕ НАБЛЮДЕНИЕ ПОСЛЕ ПРОВЕРКИ ИГРЫ: " + correction +
                " Ответь на последнюю просьбу самостоятельно, своими словами. " +
                "Не обещай это недоступное действие; action обязан быть none.";
        if (giftReaction)
            context += " Ты только что получил подарок от игрока. Ответь на него своими словами. " +
                "action=none, request=none, faith_effect=same; не назначай новую работу.";
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
        else
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

        using var request = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload.ToString())),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 40
        };
        request.SetRequestHeader("Content-Type", "application/json");
        if (provider == Plugin.Gemini) request.SetRequestHeader("x-goog-api-key", key);
        else request.SetRequestHeader("Authorization", "Bearer " + key);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            failure("Ошибка API (HTTP " + request.responseCode + "). Проверь ключ и модель.");
            yield break;
        }

        ConversationDecision decision;
        try
        {
            JObject json = JObject.Parse(request.downloadHandler.text);
            string text = provider == Plugin.Gemini ? ReadGemini(json) : ReadOpenAi(json);
            if (string.IsNullOrWhiteSpace(text))
            {
                failure("Модель не вернула ответ.");
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
                failure("Модель вернула некорректное решение.");
                yield break;
            }
            decision = new ConversationDecision(reply, action, followUp,
                endConversation.Value, faithEffect, emotion, memoryNote);
        }
        catch
        {
            failure("Не удалось прочитать ответ модели.");
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
}
