# Theory

Notes for the theory part of the 6-week AI Engineer plan: about 20 minutes a day, one note per sitting.

**How to use a note:** read it once, then answer its "Check yourself" questions *before* opening the answers. Anything you can't explain out loud in an interview, re-read the next day. Each note ties the theory back to code and numbers from this repo. Theory you've seen happen in your own code sticks better.

Measured numbers in these notes come from this repo on 2026-10-25 with `claude-sonnet-5-5` unless stated otherwise. Models, prices and API details change. Check the linked docs before quoting a number in an interview.

## Week 1 (19.10.–25.10.): LLM API basics

| # | Topic | One-line takeaway | Done |
|---|---|---|---|
| 1 | [Tokenization](week-1/01-tokenization.md) | Models read and bill in tokens, not characters, and Croatian costs ~50% more tokens than English. | [ ] |
| 2 | [Embeddings](week-1/02-embeddings.md) | Text becomes a vector; similar meaning → nearby vectors. This is the core of RAG retrieval. | [ ] |
| 3 | [Context window](week-1/03-context-window.md) | Everything the model sees in one request. The API is stateless, so you resend (and pay for) history every turn. | [ ] |
| 4 | [Temperature and sampling](week-1/04-temperature-and-sampling.md) | How the next token is picked, and why current Claude models no longer let you tune it. | [ ] |

## Coming up

Added when the week starts:

- **Weeks 3–4 (RAG):** chunking, hybrid search (full-text + vectors), reciprocal rank fusion, reranking, retrieval metrics (recall@k, MRR).
- **Week 5 (agents):** the agent loop, tool design, MCP, guarding against runaway loops.
- **Week 6 (interview prep):** attention in brief, fine-tuning vs RAG, system design for LLM apps.
