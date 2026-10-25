# Context window

> The **context window** is the maximum number of tokens a model can work with in one request: everything you send (system prompt, tool definitions, the whole conversation, documents) **plus** everything it writes back. The model has no memory between requests. If it's not in the context, it doesn't exist.

## The idea

```
┌──────────────────────── context window (1M tokens on current Claude models) ────────────────────────┐
│ tools │ system prompt │ message 1 │ reply 1 │ message 2 │ reply 2 │ … │ new message │  → output ≤ max_tokens │
└──────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

Two separate limits:

- **Context window:** input + output together. Current Claude models (Haiku 5.5, Sonnet 5.5, Opus 5.5) have **1M tokens**.
- **`max_tokens` (`MaxOutputTokens` in M.E.AI):** your cap on the *output* of one response, up to 128K on current models. Large values need streaming so the HTTP request doesn't time out. When the model hits it, generation stops mid-sentence with stop reason `max_tokens`.

## The API is stateless

The chat in `Program.cs` *feels* like a conversation, but every turn sends the **entire** `history` list again. The model rereads it all from scratch each time.

Measured over 5 turns in week 1 (cache off):

```
call:          1     2     3     4     5     6     7     8
input tokens:  563 → 1448 → 1509 → 1648 → 1752 → 2063 → 2167 → 2335
```

Input grows with every turn, so the **total** cost of a conversation grows roughly with the *square* of its length: each new turn pays again for everything before it. Those 8 calls billed **13,485** input tokens to handle a few hundred tokens of actual new questions.

Two fixes, for two different problems:

- **Cost:** **prompt caching.** The repeated prefix is billed at 10% of the normal input price on a cache read. Measured: −70% input cost on that same conversation (see `PromptCache.cs`).
- **Size:** caching doesn't make the context smaller. For conversations that would outgrow the window: drop or summarize old turns, clear old tool results (*context editing*), or use server-side *compaction*, which summarizes earlier context automatically.

## In this repo: the hidden 1,024-token cap (found and fixed)

`AsIChatClient("claude-sonnet-5-5")` defaults to **`max_tokens` = 1,024** when `ChatOptions.MaxOutputTokens` isn't set. In week 1 the first answer about PDV used 806 tokens, close to that cap. A longer answer would have stopped mid-sentence, and a long structured-output JSON would have come back unparseable.

The fix (`Program.cs`): the client default is now 16,000 (`AsIChatClient(model, defaultMaxOutputTokens: …)`), which keeps non-streaming calls like the extractor under HTTP timeouts. The streaming chat sets `MaxOutputTokens = 64_000`. Both the chat and `InvoiceExtractor` now check `FinishReason`: a `Length` (cut off) or `ContentFilter` (refused) response is reported instead of being treated as a normal answer. After the fix, a long guide came back complete at **6,758** output tokens.

## Bigger isn't free

A 1M-token window doesn't mean you should fill it:

- **Cost:** you pay for every input token on every call (cached or not).
- **Latency:** time to first token grows with input size. The model has to read everything before writing anything.
- **Quality:** models use information at the start and end of a long context more reliably than information buried in the middle ("lost in the middle"), and very long, noisy contexts degrade answers ("context rot"). Ten relevant paragraphs beat ten irrelevant documents plus the one that matters.

That last point is the whole case for **RAG**: retrieve the few chunks that matter instead of pasting the entire corpus.

## What counts against the window

Everything in the request: system prompt, **tool definitions** (the tool-use system prompt the API adds plus your tools' JSON schemas: most of the 563 tokens on the very first chat call), **structured-output schema**, images and PDFs, previous tool calls and results, and **thinking** tokens the model produces before answering.

## Common mistakes

- Thinking the model "remembers" earlier sessions. It only knows what's in this request.
- Confusing `max_tokens` (an output cap) with the context window.
- Leaving the library default for `max_tokens` and getting silently truncated answers or broken JSON.
- Stuffing whole documents in "because it fits" instead of retrieving what's relevant.
- Not checking `stop_reason` / `FinishReason`. A response that stopped at `max_tokens` (`ChatFinishReason.Length`) is incomplete.

## Check yourself

1. Your chat app has a 10-turn conversation. Why is turn 10 more expensive than turn 1 even if both questions are one line?
2. What's the difference between the context window and `max_tokens`?
3. Does prompt caching let a conversation grow past the context window? Why or why not?
4. Why might stuffing 500 pages into a 1M-token context give a *worse* answer than retrieving 5 relevant pages?
5. The extractor gets a response it can't parse as `Racun`. Name a context-related cause.

<details>
<summary>Answers</summary>

1. The API is stateless, so turn 10 resends turns 1–9 (questions, answers and tool calls) as input. Input grows each turn.
2. The context window is the total budget for input + output in one request (1M on current Claude). `max_tokens` is your cap on the output of a single response.
3. No. Caching makes the repeated prefix cheaper to bill and faster to process, but the tokens are still in the context. Size needs trimming, summarizing, context editing or compaction.
4. Cost and latency aside, models attend less reliably to information buried in long, noisy contexts. Irrelevant material competes with the relevant passage.
5. The response hit `max_tokens` (1,024 by default in this adapter, if you don't set it) and the JSON was cut off. Check the finish reason (`ChatFinishReason.Length`), and set the output cap deliberately.

</details>

## Further reading

- [Context windows (Claude docs)](https://platform.claude.com/docs/en/build-with-claude/context-windows)
- [Prompt caching (Claude docs)](https://platform.claude.com/docs/en/build-with-claude/prompt-caching)
- [Compaction](https://platform.claude.com/docs/en/build-with-claude/compaction) and [context editing](https://platform.claude.com/docs/en/build-with-claude/context-editing): the server-side options for long conversations.
- Liu et al., *Lost in the Middle: How Language Models Use Long Contexts* (2023): [arXiv:2307.03172](https://arxiv.org/abs/2307.03172)
