# Temperature and sampling

> At every step the model produces a **probability distribution** over its whole vocabulary for the next token, and one token is **sampled** from it. **Temperature** (and `top_p` / `top_k`) reshape that distribution: lower is more predictable, higher is more varied. On current Claude models you can no longer set them. Interviews still ask about them, and other providers still expose them.

## How the next token is chosen

1. The model outputs a score (**logit**) for every token in the vocabulary.
2. **Softmax** turns the scores into probabilities that sum to 1.
3. One token is picked at random according to those probabilities, appended to the text, and the process repeats.

**Temperature `T`** divides the logits before softmax: `p_i = exp(z_i / T) / Σ exp(z_j / T)`.

Worked example with three candidate tokens, logits `[2.0, 1.0, 0.1]`:

| Temperature | Probabilities | Effect |
|---|---|---|
| `T = 0.5` | 0.86 / 0.12 / 0.02 | sharper: the top token almost always wins |
| `T = 1.0` | 0.66 / 0.24 / 0.10 | the model's distribution as trained |
| `T = 2.0` | 0.50 / 0.30 / 0.19 | flatter: unlikely tokens get picked more often |

As `T → 0` this becomes **greedy decoding** (always take the top token). Even then, outputs aren't guaranteed identical across runs: floating-point and batching effects on the server can flip near-ties.

Two related knobs:

- **`top_k`:** sample only from the `k` most likely tokens.
- **`top_p` (nucleus sampling):** sample only from the smallest set of tokens whose probabilities add up to `p` (e.g. 0.9). This cuts off the long tail of nonsense tokens while still allowing variety.

## The current Claude reality (important, and recent)

- Claude **4.7 and later** models (including Haiku/Sonnet/Opus 5.5 used here) accept only the default sampling values.
- Since API version **`2026-10-21`**, sending `temperature`, `top_p` or `top_k` returns a **400 error on every model**, even at the default value.
- With the newer C# SDK, the `IChatClient` adapter **silently ignores** `ChatOptions.Temperature` / `TopP` / `TopK`. Setting them does nothing and raises no error. Don't build on them.

So when someone asks "how do you make the output consistent?", "set temperature to 0" is no longer the answer for Claude. Use these instead, all of which this repo already does or will do:

1. **Structured outputs:** `GetResponseAsync<Racun>` sends a JSON schema, and the API **constrains decoding**: at each step, tokens that would break the schema are excluded from sampling. The shape is guaranteed; only the values can vary.
2. **Clear prompts and examples:** a precise instruction ("copy values exactly as printed, never guess") narrows the distribution more than any temperature setting.
3. **Validation + retry:** `RacunValidator` + `InvoiceExtractor` catch wrong values and ask again.
4. **Effort / thinking:** the `effort` setting controls how much the model reasons before answering. It's a quality/cost knob, not a randomness knob, but more careful reasoning often means more consistent answers on hard inputs.
5. **Measure variance:** run the same input several times in your evals and report how often the answer changes (week 2).

## In this repo

The two week-1 caching runs sent the *identical* first question, and the answers came back at **806** and **873** output tokens, with different wording. That's sampling at the default temperature: same prompt, same model, different draw from the distribution. For extraction you want the values to come out the same every run, and that comes from the schema, the instructions and validation.

## When you'd want more or less randomness (other providers / older models)

- **Low (0–0.3):** extraction, classification, code, anything with one right answer.
- **Default (~1):** general chat and writing.
- **Higher:** brainstorming and creative variety, at the cost of more off-topic or wrong tokens.
- Change **either** temperature **or** top_p, not both, or the effects get hard to reason about.

## Common mistakes

- Treating temperature as a "creativity" or "intelligence" dial. It only reshapes the distribution the model already has.
- Assuming `temperature = 0` makes outputs fully deterministic.
- Setting `ChatOptions.Temperature` on a Claude client and believing it took effect.
- Using randomness controls to fix a vague prompt.

## Check yourself

1. Walk through what happens between "the model has processed the prompt" and "one new token is appended".
2. With logits `[2.0, 1.0, 0.1]`, why does lowering temperature make the first token more likely?
3. What does `top_p = 0.9` do that `top_k = 50` doesn't?
4. An interviewer asks how you'd make invoice extraction consistent with Claude Sonnet 5.5. What do you say?
5. Why can structured outputs guarantee valid JSON when the model samples randomly?

<details>
<summary>Answers</summary>

1. The model outputs one logit per vocabulary token; logits are divided by temperature; softmax turns them into probabilities; optionally the candidates are cut down by top-k/top-p; one token is sampled; it's appended and the loop repeats.
2. Dividing by `T < 1` stretches the gaps between logits (2.0 vs 1.0 becomes 4.0 vs 2.0), and softmax turns bigger gaps into much bigger probability ratios.
3. `top_p` adapts to the distribution: when the model is confident, the nucleus might be 2 tokens; when it's unsure, 200. `top_k` always keeps exactly k, regardless of how confident the model is.
4. Temperature isn't available on current Claude models (only defaults; a 400 error since API version `2026-10-21`). Consistency comes from structured outputs (schema-constrained decoding), precise instructions, validating values in code with a retry, and measuring run-to-run variance in evals.
5. Decoding is constrained. Before sampling each token, the API masks out every token that would make the output violate the schema, so randomness only ever chooses among valid continuations.

</details>

## Further reading

- [Structured outputs (Claude docs)](https://platform.claude.com/docs/en/build-with-claude/structured-outputs)
- Holtzman et al., *The Curious Case of Neural Text Degeneration* (2019), the paper that introduced nucleus (`top_p`) sampling: [arXiv:1904.09751](https://arxiv.org/abs/1904.09751)
