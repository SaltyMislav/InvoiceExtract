# Embeddings

> An **embedding** is a list of numbers (a vector) that represents the meaning of a piece of text. Texts with similar meaning get vectors that point in similar directions. That's what lets you search by meaning instead of by exact words, and it's the "retrieval" in RAG (weeks 3–4).

## The idea

An **embedding model** reads text and outputs a fixed-length vector, for example 1,024 floats, no matter how long the text was:

```
"Račun za hosting"        → [ 0.12, -0.48,  0.33, ... ]   (1,024 numbers)
"Faktura za web hosting"  → [ 0.10, -0.45,  0.35, ... ]   ← close: same meaning
"Recept za palačinke"     → [-0.61,  0.22, -0.05, ... ]   ← far: different topic
```

The individual numbers mean nothing on their own. What matters is the **geometry**: distances and angles between vectors. The model was trained so that texts people consider related end up close together.

## Two kinds of embeddings (don't mix them up)

- **Token embeddings** live *inside* every LLM. The first layer maps each token ID (see [tokenization](01-tokenization.md)) to a vector, and the rest of the network works on those. You never handle them directly.
- **Text embeddings** (sentence/document embeddings) come from a separate embedding model that gives **one vector per piece of text**. These are what you store in a database and search over. "Embeddings" in an AI engineering job means these.

## Measuring similarity: cosine

The standard measure is **cosine similarity**, the cosine of the angle between two vectors:

```
cos(a, b) = (a · b) / (|a| × |b|)        range: -1 … 1   (1 = same direction)
```

Toy example in 3 dimensions:

```
a = [1, 2, 2]   b = [2, 4, 4]   c = [2, -1, 0]

cos(a, b) = (2 + 8 + 8) / (3 × 6)        = 1.0   → same direction, only longer
cos(a, c) = (2 - 2 + 0) / (3 × √5)       = 0.0   → unrelated (perpendicular)
```

In C#:

```csharp
static float Cosine(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
{
    float dot = 0, na = 0, nb = 0;
    for (var i = 0; i < a.Length; i++)
    {
        dot += a[i] * b[i];
        na += a[i] * a[i];
        nb += b[i] * b[i];
    }
    return dot / (MathF.Sqrt(na) * MathF.Sqrt(nb));
}
```

Many embedding models return **normalized** vectors (length 1). Then cosine similarity equals the plain dot product, which is cheaper, and it's what vector databases like pgvector optimize for.

## What they're used for

- **Semantic search / RAG retrieval:** embed every document chunk once and store the vectors. At question time, embed the question and return the nearest chunks.
- **Deduplication, clustering, classification, recommendations:** anything where "how similar are these?" is the question.

## In this stack

- **Claude has no embeddings endpoint.** Anthropic points to **Voyage AI** for embeddings; Azure OpenAI and open models (e.g. multilingual-e5, bge-m3) are alternatives. Claude still writes the answers. The plan's week 3–4 setup is exactly this split: embeddings from Voyage or Azure OpenAI, answers from Claude.
- In .NET, `Microsoft.Extensions.AI` has **`IEmbeddingGenerator<string, Embedding<float>>`**: the same "swap the provider, keep the code" idea as `IChatClient`.
- **Croatian matters:** embedding quality differs a lot between models on non-English text. Comparing 2–3 multilingual models on Croatian is one of the plan's week 3–4 findings, and one few candidates can show.

## Practical rules

- **Use the same model for documents and queries.** Vectors from different models live in different spaces and can't be compared, even with the same dimension count. Changing models means re-embedding everything.
- **Chunk size matters.** One vector per chunk means a 20-page chunk blurs into an average of many topics. Chunks that are too small lose context. (Week 3 compares fixed-length vs heading-based chunking.)
- **Vectors are weak on exact strings.** Searching for OIB `69502483115` or invoice number `2026-0417` by meaning is the wrong tool, and keyword search nails it. That's why the RAG project uses **hybrid search** (Postgres full-text + vectors, merged with RRF).
- **Dimensions cost storage and speed:** 1,024 float32 values = 4 KB per chunk. Bigger isn't automatically better; measure retrieval quality.

## Common mistakes

- Mixing vectors from two embedding models in one index.
- Expecting embeddings to understand exact identifiers, numbers or negation ("invoices *not* paid").
- Skipping measurement. "It looks relevant" isn't a metric; recall@k is (week 3).

## Check yourself

1. What's the difference between the token embeddings inside Claude and the embeddings you store in pgvector?
2. You switch from one embedding model to another with the same dimension. Can you keep the old vectors?
3. Why would pure vector search do badly on "find invoice with OIB 69502483115"?
4. When are cosine similarity and dot product the same?
5. Why does a very large chunk make retrieval worse?

<details>
<summary>Answers</summary>

1. Token embeddings are the LLM's internal first layer (one vector per token, never exposed). Stored embeddings come from a separate embedding model, one vector per chunk of text, built for similarity search.
2. No. Each model has its own vector space; matching dimensions is a coincidence, not compatibility. Re-embed everything.
3. Embeddings capture meaning, and an 11-digit identifier has no meaning to capture. Nearby vectors are "documents about invoices", not the one with those exact digits. Keyword/full-text search (or a plain SQL filter) handles exact matches, hence hybrid search.
4. When both vectors are normalized to length 1: the denominator `|a|×|b|` is 1.
5. Its single vector averages many topics, so it's moderately close to many queries and very close to none. The relevant sentence gets diluted.

</details>

## Further reading

- [Voyage AI documentation](https://docs.voyageai.com/): current models, multilingual options, input types for queries vs documents.
- Mikolov et al., *Efficient Estimation of Word Representations in Vector Space* (word2vec, 2013): [arXiv:1301.3781](https://arxiv.org/abs/1301.3781). The classic "king − man + woman ≈ queen" paper.
- Reimers & Gurevych, *Sentence-BERT* (2019): [arXiv:1908.10084](https://arxiv.org/abs/1908.10084). How sentence-level embeddings for search came about.
