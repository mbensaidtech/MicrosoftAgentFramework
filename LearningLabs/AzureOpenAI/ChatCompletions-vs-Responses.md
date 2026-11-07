# Chat Completions and Responses API

The labs of Part 1 run on the **Chat Completions** API. Lab 01 (scenario 6) introduces the **Responses API**, and three labs end with Responses scenarios, placed where the two APIs really differ:

| Lab | Chat Completions scenarios | Responses scenarios (bonus) | The question they answer |
|-----|----------------------------|---------------------|--------------------------|
| [Lab 04 — MCP client](Lab04-AIAgentWithMCPClient/README.md) | 1 to 3 | 4: the same MCP client on Responses · 5: hosted MCP | Who connects to the MCP server and runs its tools: your application, or the service? |
| [Lab 05 — Sessions](Lab05-AIAgentWithSessions/README.md) | 1 to 3 | 4: state kept by the service · 5: Responses with `store = false` and your storage | Who keeps the conversation: your application, or the service? |
| [Lab 07 — Agentic RAG](Lab07-AgenticRAG-VectorStore/README.md) | 1 to 4 | 5: your vector store on Responses · 6: hosted file search | Who stores and searches your documents: your application, or the service? |

These Responses scenarios are a **bonus**: a lab is completed once its Chat Completions scenarios pass, and *completed with its bonus* (**Completed+** in the Lab Bench dashboard and for the trainer) once its Responses scenarios pass too. They are declared in `labs.json` (`bonusScenarios`) and marked `// Bonus scenario` in `Program.cs`.

Each Responses part has the same structure: **first** what does *not* change (your MCP client, your storage, your vector store work on the Responses API), **then** what the Responses API adds (the service orchestrates it). Run a lab with `dotnet run -- --scenarios 4,5` (Lab 07: `1,5,6`) to see only its Responses part.

## What does not change

Agent Framework hides the API behind the same abstractions. On both APIs you write the same code for:

- `AIAgent`, `RunAsync`, `RunStreamingAsync`, `AgentResponse`, `RunAsync<T>()` (structured output), token usage;
- function tools (`AIFunctionFactory`), MCP tools from your own `McpClient`, tool approval (Lab 09), middleware (Lab 03, 11);
- `AgentSession`, `SerializeSessionAsync` / `DeserializeSessionAsync`, `ChatHistoryProvider` and `AIContextProviders` — as long as the history stays on your side.

The only line that changes is the creation of the agent:

```csharp
AIAgent agent = client.GetChatClient(deployment).AsAIAgent(options);               // Chat Completions
AIAgent agent = client.GetResponsesClient().AsAIAgent(options, model: deployment);  // Responses (OPENAI001)
```

This is why the course does not duplicate its labs for the Responses API: copying a lab and replacing `GetChatClient()` with `GetResponsesClient()` would teach nothing. The Responses scenarios focus on the differences below.

## What really changes

| | Chat Completions | Responses API |
|---|---|---|
| Unit of exchange | Messages | Typed **items** (`message`, `function_call`, `function_call_output`, `reasoning`, `mcp_call`, `file_search_call`...) |
| Conversation state | Always on your side: each request sends the history | Your choice: kept by the service (`store = true`, the default, 30 days; `previous_response_id`) or on your side (`store = false`) — Lab 05 |
| Hosted tools, run by the service | No (silently left out of the request, see below) | Hosted MCP (Lab 04), file search (Lab 07), code interpreter, web search, image generation |
| Background responses (long runs) | No | Yes (they need `store = true`) |
| Reasoning of reasoning models across turns | Not kept | Kept by the service, or sent back as encrypted content with `store = false` |
| `ChatHistoryProvider`, chat reducer, compaction | Yes | Yes with `store = false`; not with the state kept by the service |
| Models | Widest compatibility | The models and regions that support the Responses API |

Microsoft recommends the Responses API for new Azure OpenAI applications (*"Responses is the recommended primary client: it supports the full set of hosted tools"*, [Azure OpenAI provider](https://learn.microsoft.com/agent-framework/agents/providers/azure-openai)).

## Exclusive to Responses, or just orchestrated by the service?

The Responses API makes some capabilities available **as hosted tools**. The capability itself is not exclusive: with Chat Completions — or with Responses — you can build it in your application.

| Capability | Built by your application (both APIs) | Hosted by the service (Responses only) |
|---|---|---|
| Call the tools of an MCP server | `McpClient` + `McpClientTool` (Lab 04, scenarios 2 to 4) | `HostedMcpServerTool` (Lab 04, scenario 5) |
| Keep a conversation | `AgentSession` + `ChatHistoryProvider` (Lab 05, scenarios 1 to 3 and 5) | `store = true` + `previous_response_id` (Lab 05, scenario 4); Conversations API on OpenAI |
| Search your documents (RAG) | Vector store + search tool or `TextSearchProvider` (Lab 07, scenarios 1 to 5) | `HostedFileSearchTool` on a vector store of the service (Lab 07, scenario 6) |

What the hosted version brings is **less code and fewer round trips**: the service runs the tool loop inside one request. What it costs is **control and independence**: the provider sees the calls and the data, stores what it needs (files, vector stores, responses), and the feature only exists at this provider.

| | Orchestrated by your application | Orchestrated by the service |
|---|---|---|
| Control | Every call, result and stored message goes through your code: filter, redact, log, retry, authorize per user | What the service exposes (`AllowedTools`, `ApprovalMode`, `MaximumResultCount`, ranking options, `include`) |
| Flexibility | Any MCP server (local, private), any store, any embedding model, any chunking | Public MCP servers, the embeddings and chunking of the service |
| Persistence | Where you decide, as long as you decide | At the provider: responses 30 days, files and vector stores until deleted |
| Dependency on the provider | None: change model provider, keep the feature | The feature, its data and its ids exist only at this provider |
| Code and latency | More code, one round trip per tool step | Less code, one request |

## Pitfalls checked in these labs

- **A hosted tool given to a Chat Completions agent is silently ignored.** No error: the request is sent without the tool, and the model answers from its own knowledge (Lab 04 and Lab 07 troubleshooting).
- **`store = true` is the default.** Every response, with its inputs (your prompts, tool results, retrieved documents), is kept 30 days by the service. Use `AsIChatClientWithStoredOutputDisabled(model)` to turn it off (Lab 05 scenario 5, Lab 07 scenario 5).
- **A `ChatHistoryProvider` and stored responses do not mix**: *Only ConversationId or ChatHistoryProvider may be used, but not both* (Lab 05).
- **`previous_response_id` is not cheaper**: the history is not sent, but the model reads it and it is billed (Lab 05 scenario 4).
- **The response id changes at every turn**: it is not a conversation id, and never a business id (Lab 05, *Four identifiers you must not confuse*).
- **Hosted resources must be cleaned up**: stored responses (`DeleteResponseAsync`), files and vector stores (`DeleteFileAsync`, `DeleteVectorStoreAsync`) stay at the provider until you delete them or they expire.

## Azure OpenAI specifics (checked on 2026-11-07)

| Feature | Azure OpenAI v1 endpoint (`/openai/v1/`) |
|---|---|
| Responses API, `store`, `previous_response_id`, `GET` / `DELETE /responses/{id}` | Yes (30-day retention) |
| Hosted MCP (`mcp` tool) | Yes; the MCP server must be reachable from the service (TLS 1.2+, no mTLS, no service tags) |
| Files, vector stores, file search | Yes |
| Conversations API (`/conversations`, `conv_...`) | **No**: 404. Documented in Lab 05, not run |

## Versions and experimental APIs

Microsoft Agent Framework **1.27.0**, OpenAI .NET SDK **2.14.0**, Microsoft.Extensions.AI **10.10.1**, ModelContextProtocol **2.2.0**. Some of the APIs used by the Responses scenarios are still flagged experimental; the labs keep a `#pragma warning disable` around them:

| Diagnostic | APIs |
|---|---|
| `OPENAI001` (OpenAI SDK) | `GetResponsesClient()`, `ResponsesClient`, `GetVectorStoreClient()`, `VectorStoreClient`, `CreateResponseOptions`, `FileSearchCallResponseItem`, `McpToolDefinitionListItem`, `GetConversationClient()` |
| `MAAI001` (Agent Framework) | `AsIChatClientWithStoredOutputDisabled` |

`HostedMcpServerTool`, `HostedFileSearchTool`, `HostedVectorStoreContent`, `McpServerToolCallContent` and `CitationAnnotation` (Microsoft.Extensions.AI) are stable.

## Links

- [Azure OpenAI provider in Agent Framework](https://learn.microsoft.com/agent-framework/agents/providers/azure-openai)
- [Azure OpenAI Responses API](https://learn.microsoft.com/azure/ai-foundry/openai/how-to/responses)
- [OpenAI — migrate to the Responses API](https://platform.openai.com/docs/guides/migrate-to-responses) and [conversation state](https://platform.openai.com/docs/guides/conversation-state)
- [Agent Framework — conversation storage](https://learn.microsoft.com/agent-framework/concepts/agents/conversations/storage?pivots=programming-language-csharp)
