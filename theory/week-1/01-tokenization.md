# Tokenization

> An LLM never sees characters. Text is split into **tokens** (subword pieces), each mapped to an integer ID. You pay per token, the context window is measured in tokens, and output is generated one token at a time.

## The idea

A tokenizer turns text into a sequence of IDs from a fixed **vocabulary** (typically tens of thousands to a few hundred thousand entries). The model only ever works with those IDs.

```
"Račun je izdan"  →  ["Ra", "č", "un", " je", " iz", "dan"]  →  [4521, 268, 403, 584, 2391, 9827]
                      (illustrative split, not Claude's real one)
```

Why not whole words? There are too many (every name, typo, number and inflection would need an entry). Why not single characters? The sequences would be very long, and the model would have to learn spelling before meaning. Subwords are the compromise: common words are one token, and rare words are built from pieces.

## How it works: byte-pair encoding (BPE)

Most modern tokenizers, including Claude's, work in the BPE family:

1. Start with the raw **bytes** of the text (UTF-8). Every possible input can be represented, so there's never an "unknown word".
2. Over a huge training corpus, find the most frequent adjacent pair and merge it into a new token. Repeat tens of thousands of times.
3. The merges learned in training are applied in order to any new text.

The result reflects the training data. Pieces that were frequent get their own token; rare ones stay split. Training data is dominated by English, so English words merge into few tokens. Croatian words, and especially the diacritics `č ć š ž đ` (2 bytes each in UTF-8), merge less.

## In this repo (measured)

Counted with the free `count_tokens` endpoint on `claude-sonnet-5-5`:

| Text | Characters | Tokens | Chars per token |
|---|---|---|---|
| English sentence about the invoice total and due date | 134 | 46 | 2.9 |
| The same sentence in Croatian | 129 | **69** | 1.9 |
| `samples/racun-001.txt` (the whole sample invoice) | 636 | 324 | 2.0 |

Single words (approximate, ±1, since message overhead was subtracted):

| Word | Tokens | | Word | Tokens |
|---|---|---|---|---|
| `payment` | 2 | | `plaćanje` | **5** |
| `accounting` | 3 | | `računovodstvo` | 6 |
| `invoice` | 3 | | `račun` | 2 |
| `placanje` (no diacritic) | 3 | | `69502483115` (OIB) | 4 |
| `1.167,50` | 5 | | `2026-10-14` | 6 |

What this shows:

- **The same meaning in Croatian takes ~50% more tokens** (69 vs 46): more cost, more context used, slower output.
- **Diacritics cost tokens**: `plaćanje` takes 5, `placanje` takes 3. (Don't strip them. Correct text matters more than the saving.)
- **Numbers are split into chunks**: an 11-digit OIB is 4 tokens, and a date is 6. The model never "sees" a number as a quantity, only as pieces of text.
- **Haiku, Sonnet and Opus 5.5 gave identical counts** (69) for the Croatian sentence: one tokenizer across the family.

There's also overhead you don't write yourself. A one-word message costs about 8 tokens of structure. In week 1's runs, a short chat question billed **563** input tokens, mostly tool definitions plus the tool-use system prompt the API adds. Extracting the 324-token invoice billed **1,121** input tokens, because the JSON schema and instructions also count.

## Why it matters

- **Cost:** you're billed per input and output token. Token count, not character count, is what to estimate.
- **Character-level tasks are hard:** counting letters, reversing strings, long arithmetic. The model sees `["Ra","č","un"]`, not letters or digits. That's why `RacunValidator` adds up the line items in C# instead of asking the model whether the total is right. **Do arithmetic in code, not in the prompt.**
- **Latency:** output is generated one token per step, so a 500-token answer takes about 500 forward passes. Streaming shows them as they come.
- **Tokenizers are model-specific.** Claude's tokenizer changed with Opus 4.7, and the same text can be ~1.0–1.35× the tokens of older Claude models. Never estimate Claude tokens with `tiktoken` (OpenAI's tokenizer): it undercounts, especially on non-English text. Use the `count_tokens` endpoint instead.

## Common mistakes

- Estimating cost as "words × 1.3". That's an English rule of thumb, and Croatian breaks it.
- Assuming `max_tokens` counts characters or words.
- Trusting the model's arithmetic or letter counting without checking it in code.
- Using one model's token count for another provider's model.

## Check yourself

1. Why does the Croatian sentence need more tokens than the English one with the same meaning?
2. A model says `računovodstvo` has 12 letters. Why might it get this wrong?
3. The invoice text is 324 tokens, but the extraction call billed 1,121 input tokens. Where did the other ~800 come from?
4. Why does `RacunValidator` check `Σ stavke + PDV = Ukupno` in C# instead of asking the model to?
5. Why is `tiktoken` the wrong tool for estimating a Claude bill?

<details>
<summary>Answers</summary>

1. BPE merges reflect the training data, which is mostly English. Common English words merged into single tokens; Croatian words and diacritics (2 UTF-8 bytes each) stayed split into more pieces.
2. It never sees letters, only token IDs for chunks like `ra`/`ču`/`novod`/`stvo`. Counting letters means reasoning about pieces it can't directly inspect.
3. Overhead that's sent but not written in the message: the instructions (system prompt), the JSON schema for `Racun` (structured output), and message formatting.
4. Numbers are split into token chunks, so arithmetic is unreliable for the model and trivial and exact for code. Use the model for reading, and code for checking.
5. It's OpenAI's tokenizer with a different vocabulary. It undercounts Claude tokens (by more on non-English text and code). Token counts are model-specific; use Claude's `count_tokens` endpoint.

</details>

## Further reading

- [Token counting (Claude docs)](https://platform.claude.com/docs/en/build-with-claude/token-counting)
- Sennrich et al., *Neural Machine Translation of Rare Words with Subword Units* (2015), the paper that brought BPE to NLP: [arXiv:1508.07909](https://arxiv.org/abs/1508.07909)
- Andrej Karpathy, *Let's build the GPT Tokenizer* (YouTube): builds BPE from scratch in about 2 hours. Good for a weekend.
