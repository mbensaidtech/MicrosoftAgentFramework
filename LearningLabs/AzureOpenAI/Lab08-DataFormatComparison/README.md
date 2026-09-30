# Lab 08 - Data Format Comparison

## Objective

In this lab, you will measure how the **format of the data** exchanged between an agent and the model changes the **token usage**, with **Microsoft Agent Framework** and **Azure OpenAI**.

An agent gets a catalog of 50 hotels from a function tool and answers a question about it. You run the same question twice:

1. **JSON** — the tool returns the hotels as **objects** (the framework serializes them as JSON for the model) and the agent answers with **structured output** (`RunAsync<List<Hotel>>`, Lab 02).
2. **CSV** — the tool returns the hotels as **CSV text** and the instructions ask the agent to answer in CSV too; the answer is parsed back into objects.

Then you compare, side by side, the size of the tool result the model received and the input, output and total tokens of each run.

## What You Will Learn

- What the model actually receives when a function tool returns objects or text (`FunctionResultContent`)
- How to combine function tools with structured output (`RunAsync<T>` on an agent with tools)
- How to ask for a compact text format in the instructions, and what you lose compared to a JSON schema
- How to read the tool calls, the tool result size and the token usage of a run from `AgentResponse`
- When a compact format (CSV) is worth it, and when structured JSON is the better choice

## Prerequisites

- .NET 10 SDK or later
- An Azure OpenAI (or Microsoft Foundry) resource with a chat model deployment that supports structured outputs and function calling (for example `gpt-4o-mini` or `gpt-5.4-mini`)
- One of the following for authentication:
  - the resource **API key**, or
  - Azure CLI logged in (`az login`) with the **Cognitive Services OpenAI User** role on the resource
- Lab 02 (structured output) and Lab 03 (function tools) completed

## Project Structure

```
Lab08-DataFormatComparison/
├── README.md
├── Start/                          <-- Your working folder
│   ├── Program.cs                  <-- Complete the TODOs here
│   ├── Models/
│   │   └── Hotel.cs                <-- The hotel type (provided)
│   ├── Tools/
│   │   └── HotelTools.cs           <-- The two function tools: hotels as objects, hotels as CSV (provided)
│   ├── Data/
│   │   └── hotels.json             <-- The 50 hotels
│   ├── HotelData.cs                <-- Reads hotels.json (provided)
│   ├── HotelCsv.cs                 <-- CSV encoding and parsing of the hotels (provided)
│   ├── FormatConsole.cs            <-- Display helpers (provided)
│   ├── ConfigurationHelper.cs
│   ├── AzureOpenAISettings.cs
│   ├── appsettings.json
│   └── DataFormatComparison.csproj
└── Solution/                       <-- Reference solution
    └── ...
```

## Instructions

### Step 1: Configure your settings

Settings are read, in this order (last wins): `appsettings.json` → **user secrets** → **environment variables**.

1. Open `Start/appsettings.json` and set the non-secret values:

   ```json
   {
     "AzureOpenAI": {
       "Endpoint": "https://YOUR-RESOURCE.openai.azure.com/",
       "ChatDeploymentName": "YOUR-DEPLOYMENT-NAME"
     }
   }
   ```

   Use the resource endpoint shown in the Azure portal. The `/openai/v1/` suffix required by the v1 API is added for you.
   `*.openai.azure.com`, `*.cognitiveservices.azure.com` and `*.services.ai.azure.com` endpoints all work.

2. Choose an authentication method:

   - **API key** (simplest for local development) — store it as a user secret, **never** in `appsettings.json`:

     ```bash
     cd Start
     dotnet user-secrets set "AzureOpenAI:APIKey" "<your-api-key>"
     ```

     The user secrets id is shared by all the labs: if you already did it for a previous lab, there is nothing to do.
     You can also use the environment variable `AzureOpenAI__APIKey`.

   - **Microsoft Entra ID** — leave `APIKey` unset and run `az login`. `DefaultAzureCredential` picks up your Azure CLI identity.

3. **Or use the dashboard**: the *Azure OpenAI settings* of the optional [Lab Bench dashboard](../../../Dashboard/README.md#azure-openai-settings) write the endpoint, the deployment and the API key to the same user secrets, for every migrated lab (no file to edit, no command to type).

### Step 2: Look at the provided files

- `Models/Hotel.cs` — the hotel type, used everywhere: read from `Data/hotels.json`, returned by the JSON tool, deserialized from the structured output of scenario 1 and parsed back from the CSV answer of scenario 2. Its `[Description]` attributes are copied into the JSON schema of scenario 1.
- `Tools/HotelTools.cs` — the two function tools, both named `get_all_hotels` when given to an agent: `GetAllHotelsAsJson()` returns the hotels as **objects** (`IReadOnlyList<Hotel>`), `GetAllHotelsAsCsv()` returns them as **CSV text**. Their `[Description]` tells the model what the tool returns.
- `HotelCsv.cs` — `Header` (`Name,City,Stars,PricePerNight,Currency,Rooms,HasPool,HasWifi,Rating`), `Serialize(hotels)` and `Deserialize(text)`, which throws a `FormatException` when the text is not the expected CSV.
- `HotelData.cs` — `LoadAsync()` reads the 50 hotels of `Data/hotels.json`.
- `FormatConsole.cs` — `WriteToolCalls(response)` (the tools called and the size of the tool result **as the model received it**), `WriteHotels(hotels, heading)`, `WriteTokenUsage(response, heading)` and `WriteComparison(jsonResponse, csvResponse)`. `Program.cs` imports them with `using static`, so you can call them directly.
- `Program.cs` — the setup already loads the hotels, creates `hotelTools`, defines the `question` asked in both scenarios and the two variables (`jsonResponse`, `csvResponse`) used by the comparison at the end.

### Step 3: Complete the Program.cs

Open `Start/Program.cs` and complete the TODOs:

---

#### Setup: Configuration, client and hotel data

TODO 1 to 3 are the same as in Lab 01.

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 1** | Create the `OpenAIClientOptions` for the Azure OpenAI v1 endpoint | • `new OpenAIClientOptions { Endpoint = ... }` <br> • `AzureOpenAIEndpoint.ToV1Uri(settings.Endpoint)` (from `CommonUtilities`) returns the `.../openai/v1/` URI |
| **TODO 2** | Create the `OpenAIClient` | • API key: `new OpenAIClient(new ApiKeyCredential(settings.APIKey), clientOptions)` <br> • Entra ID: `new OpenAIClient(new BearerTokenPolicy(new DefaultAzureCredential(), "https://ai.azure.com/.default"), clientOptions)` <br> • Use a conditional on `string.IsNullOrWhiteSpace(settings.APIKey)` to support both |
| **TODO 3** | Get a `ChatClient` for the deployment | • `client.GetChatClient(settings.ChatDeploymentName)` |

---

#### Scenario 1: JSON - the tool returns objects, the agent answers with structured output

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 4** | Create the JSON tool and the agent | • `AITool getAllHotelsAsJson = AIFunctionFactory.Create(hotelTools.GetAllHotelsAsJson, "get_all_hotels");` <br> • `AIAgent jsonAgent = chatClient.AsAIAgent(instructions: "You are a hotel data assistant. Call get_all_hotels to get the hotels, then answer with every hotel that matches the request, in the requested order.", name: "HotelJsonAgent", tools: [getAllHotelsAsJson]);` <br> • The tool returns objects: the framework serializes them as JSON (indented, camelCase) in the tool result |
| **TODO 5** | Run the agent with structured output | • `jsonResponse = await jsonAgent.RunAsync<List<Hotel>>(question).WithSpinner("Running agent");` <br> • The JSON schema of `List<Hotel>` is sent as the response format of the run (Lab 02); `jsonResponse.Result` is already a `List<Hotel>` |
| **TODO 6** | Display the tool calls and the typed result | • `WriteToolCalls(jsonResponse);` — `Tool called: get_all_hotels()` and `Tool result sent to the model: ... characters` <br> • `WriteHotels(jsonResponse.Result, "Hotels returned by the agent");` <br> • Expected: 43 hotels, `Backpacker Hostel (Bangkok) - 25 USD/night` first |
| **TODO 7** | Display token usage | • `WriteTokenUsage(jsonResponse, "Token Usage (JSON):");` <br> • Total of the model calls of the run: the call that asks for the tool, then the call that answers with the tool result |

---

#### Scenario 2: CSV - the tool returns text, the agent answers in CSV

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 8** | Create the CSV tool and the agent | • `AITool getAllHotelsAsCsv = AIFunctionFactory.Create(hotelTools.GetAllHotelsAsCsv, "get_all_hotels");` <br> • `AIAgent csvAgent = chatClient.AsAIAgent(instructions: $"""..."""`, `name: "HotelCsvAgent", tools: [getAllHotelsAsCsv]);` with these instructions (an interpolated raw string literal, `{HotelCsv.Header}` on its own line): <br> `You are a hotel data assistant. Call get_all_hotels to get the hotels as CSV, then answer with every hotel that matches the request, in the requested order.` <br> `Answer ONLY in CSV, with exactly this header line and then one line per hotel, with the same columns and values as the tool result:` <br> `{HotelCsv.Header}` <br> `No explanation, no markdown, no code fences.` |
| **TODO 9** | Run the agent | • `csvResponse = await csvAgent.RunAsync(question).WithSpinner("Running agent");` — the non-generic `RunAsync`: the answer is text |
| **TODO 10** | Display the tool calls and the raw answer | • `WriteToolCalls(csvResponse);` <br> • `ColoredConsole.WritePrimaryLogLine("Agent answer (CSV):");` <br> • `ColoredConsole.WriteSecondaryLogLine(csvResponse.Text);` |
| **TODO 11** | Parse the CSV back into hotels | • `try { List<Hotel> csvHotels = HotelCsv.Deserialize(csvResponse.Text); ColoredConsole.WritePrimaryLogLine($"Parsed back {csvHotels.Count} hotels from the CSV answer"); }` <br> • `catch (FormatException ex) { ColoredConsole.WriteErrorLine($"The answer is not the expected CSV: {ex.Message}"); }` <br> • No schema guarantees the format: the instructions are a request, not a guarantee |
| **TODO 12** | Display token usage | • `WriteTokenUsage(csvResponse, "Token Usage (CSV):");` |

---

#### Comparison: JSON vs CSV

| TODO | Description | Hints |
|------|-------------|-------|
| **TODO 13** | Display the comparison (inside the provided `if`, which needs both responses) | • `WriteComparison(jsonResponse, csvResponse);` <br> • One row per measure: the size of the tool result, the input, output and total tokens, with the difference of CSV against JSON in percent |

---

### Step 4: Run and Test

**Run the Start project (your implementation):**
```bash
cd Start
dotnet run
```

**Run the Solution (reference):**
```bash
cd Solution
dotnet run
```

To run only one scenario, edit `scenariosToRun` at the top of `Program.cs` (for example `[2]`). The comparison at the end needs both.

## What the model receives and produces

```
 Scenario 1 - JSON                                    Scenario 2 - CSV

 RunAsync<List<Hotel>>(question)                      RunAsync(question)
        │  response format = JSON schema of List<Hotel>       │  instructions: "answer in CSV with this header"
        ▼                                                     ▼
 model call 1: "call get_all_hotels()"                model call 1: "call get_all_hotels()"
        │                                                     │
        ▼                                                     ▼
 tool returns IReadOnlyList<Hotel>                    tool returns a string (CSV)
   → framework: JsonElement (indented JSON array)       → framework: JsonElement (JSON string, "\n" escaped)
   → tool message: ~10 000 characters                   → tool message: ~2 800 characters
        │                                                     │
        ▼                                                     ▼
 model call 2: answers with JSON that follows         model call 2: answers with CSV text
 the schema → deserialized into List<Hotel>           → HotelCsv.Deserialize (may fail)
```

- **Tool results**: `AIFunctionFactory` turns the return value of a tool into a `JsonElement`. The OpenAI chat client then sends it as text: objects become **indented JSON** with the property names repeated for every hotel; a string is sent **as is**, as a JSON string (quotes, `\n` for the line breaks). `WriteToolCalls` measures this text from the `FunctionResultContent` kept in `response.Messages`.
- **Input tokens** are the sum of the two model calls of the run: instructions, question, tool definitions, then the same plus the tool call and the tool result (and, in scenario 1, the JSON schema of the response format).
- **Output tokens** are the tool call plus the answer: 43 hotels as JSON objects (`{"name": "...", "city": "...", ...}`) or as 43 CSV lines.

| | Scenario 1 — JSON + structured output | Scenario 2 — CSV in the instructions |
|---|---|---|
| Tool result | Objects, serialized by the framework | Text, built by your code |
| Answer format | Enforced by the JSON schema (`RunAsync<T>`) | Requested in the instructions only |
| Parsing the answer | Done for you: `response.Result` | Yours: `HotelCsv.Deserialize`, with a `FormatException` to handle |
| Tokens | The reference | Fewer input **and** output tokens for tabular data |
| Good for | Nested or optional data, guaranteed shape, small answers | Large flat tables (lists of records with the same columns), cost-sensitive calls |

Typical results with `gpt-4o-mini`: the CSV run uses about half the input tokens and about a third of the output tokens of the JSON run. Measure with your model: token counts depend on the tokenizer and on the answer.

## Key Concepts

| Concept | Description |
|---------|-------------|
| `AIFunctionFactory.Create(method, name)` | Turns a method into a tool; its return value is serialized to a `JsonElement` with `AIJsonUtilities.DefaultOptions` (indented, camelCase) |
| `FunctionCallContent` / `FunctionResultContent` | The tool call requested by the model and the tool result sent back, kept in `response.Messages` |
| Tool result as text | The OpenAI chat client sends a string result as is, and any other result serialized as JSON |
| `RunAsync<T>()` with tools | The response format (JSON schema of `T`) applies to the run; the tool loop still happens; `Result` is the typed answer |
| Instructions-only format | Any text format (CSV, Markdown...) can be requested in the instructions, with no guarantee: parse defensively |
| `AgentResponse.Usage` | `InputTokenCount`, `OutputTokenCount`, `TotalTokenCount`: the total of every model call of the run |
| CSV | Comma-separated values, one header line then one line per record: the property names appear once |

## Namespaces Reference

| Namespace | Purpose |
|-----------|---------|
| `OpenAI` | `OpenAIClient`, `OpenAIClientOptions` |
| `OpenAI.Chat` | `ChatClient` and the `AsAIAgent()` extension methods |
| `System.ClientModel` / `System.ClientModel.Primitives` | `ApiKeyCredential` / `BearerTokenPolicy` |
| `Azure.Identity` | `DefaultAzureCredential` |
| `Microsoft.Extensions.AI` | `AIFunctionFactory`, `AITool`, `FunctionCallContent`, `FunctionResultContent`, `AIJsonUtilities` (in `FormatConsole.cs`) |
| `Microsoft.Agents.AI` | `AIAgent`, `AgentResponse`, `AgentResponse<T>` |
| `System.ComponentModel` | `[Description]` (in `Models/Hotel.cs` and `Tools/HotelTools.cs`) |
| `System.Text.Json` | `JsonSerializer` (in `HotelData.cs` and `FormatConsole.cs`) |
| `CommonUtilities` | `ColoredConsole`, `WithSpinner()`, `AzureOpenAIEndpoint` |

## Expected Output

```
Endpoint: https://your-resource.openai.azure.com/
Deployment: your-deployment-name
Loaded 50 hotels from Data/hotels.json
----------------------------------------
=== Scenario 1: JSON - the tool returns objects, the agent answers with structured output ===
Question: Which hotels cost less than 100 USD per night? Give all of them, ordered by price per night, cheapest first.
Tool called: get_all_hotels()
Tool result sent to the model: 10348 characters
Hotels returned by the agent: 43
  Backpacker Hostel (Bangkok) - 25 USD/night - stars: 1 - rating: 3.8
  Budget Stay Express (Chicago) - 35 USD/night - stars: 2 - rating: 3.5
  Travelers Rest Motel (...) - 38 USD/night - ...
  ...
----------------------------------------
Token Usage (JSON):
  Input tokens: 3xxx
  Output tokens: 1xxx
  Total tokens: 5xxx
----------------------------------------
=== Scenario 2: CSV - the tool returns text, the agent answers in CSV ===
Question: Which hotels cost less than 100 USD per night? Give all of them, ordered by price per night, cheapest first.
Tool called: get_all_hotels()
Tool result sent to the model: 2765 characters
Agent answer (CSV):
Name,City,Stars,PricePerNight,Currency,Rooms,HasPool,HasWifi,Rating
Backpacker Hostel,Bangkok,1,25,USD,40,false,true,3.8
Budget Stay Express,Chicago,2,35,USD,60,false,true,3.5
...
Parsed back 43 hotels from the CSV answer
----------------------------------------
Token Usage (CSV):
  Input tokens: 1xxx
  Output tokens: xxx
  Total tokens: 2xxx
----------------------------------------
=== Comparison: JSON vs CSV ===
                            JSON       CSV   CSV vs JSON
Tool result (chars)        10348      2765          -73%
Input tokens                3xxx      1xxx          -5x%
Output tokens               1xxx       xxx          -6x%
Total tokens                5xxx      2xxx          -5x%
```

The sizes of the tool results are exact (same data every time). The token counts and the number of hotels returned vary from one run and one model to another.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `'AzureOpenAI:Endpoint' is not configured` | Replace the `YOUR-...` placeholders in `appsettings.json` (or use user secrets / environment variables) |
| `401 Unauthorized` with an API key | Check the key (user secret `AzureOpenAI:APIKey`) and that it belongs to the resource of the endpoint |
| `401` / `403` with Entra ID | Run `az login` and make sure your identity has the **Cognitive Services OpenAI User** role on the resource |
| `404 DeploymentNotFound` | `ChatDeploymentName` must be the **deployment** name, not the model name |
| `400` mentioning `response_format` / `json_schema` | The deployed model does not support structured outputs: use a more recent model (for example `gpt-4o-mini`) |
| `Tool called: (none)` | The agent answered without the tool: check that the tool is given to `AsAIAgent(..., tools: [...])` and that the instructions say to call `get_all_hotels` |
| `The answer is not the expected CSV: ...` in scenario 2 | Expected from time to time: the format is only described in the instructions (code fences are tolerated, a changed header or a missing column is not). Run again, or tighten the instructions |
| `JsonException` when reading `jsonResponse.Result` | The JSON answer does not match `Hotel` (a required property is missing): with `gpt-4o-mini` this is rare; a truncated answer (`MaxOutputTokens` too low) also causes it |
| The comparison is not displayed | Both scenarios must run: set `scenariosToRun = [1, 2]` |
| Fewer than 43 hotels | The model skipped some: the question asks for all of them, but nothing forces the model to be exhaustive |

## Provided Files

The following files are provided and should not be modified:

- `DataFormatComparison.csproj` - Project file with all required dependencies
- `ConfigurationHelper.cs` - Loads and validates the configuration
- `AzureOpenAISettings.cs` - Settings class for Azure OpenAI
- `Models/Hotel.cs` - The hotel type with its `[Description]` attributes
- `Tools/HotelTools.cs` - The two function tools (objects, CSV)
- `HotelData.cs` - Reads the hotel file
- `HotelCsv.cs` - CSV encoding and parsing
- `FormatConsole.cs` - Display helpers
- `Data/hotels.json` - The 50 hotels

The following files should be modified:

- `appsettings.json` - Update with your Azure OpenAI endpoint and deployment
- `Program.cs` - Complete the TODOs

### NuGet packages

| Package | Why |
|---------|-----|
| `Microsoft.Agents.AI.OpenAI` | Agent Framework + `AsAIAgent()` for OpenAI clients (brings `OpenAI` and `Microsoft.Extensions.AI`: `AIFunctionFactory`, `FunctionResultContent`) |
| `Azure.Identity` | `DefaultAzureCredential` for Entra ID authentication |
| `Microsoft.Extensions.Configuration.*` | `appsettings.json`, user secrets and environment variables, bound to `AzureOpenAISettings` |

No serialization library is needed: JSON comes with .NET (`System.Text.Json`, used by the framework), and the CSV encoding is a few lines in `HotelCsv.cs`.

## Going Further

- **Compact JSON**: the middle ground between the two scenarios is a tool that returns objects serialized without indentation. `AIFunctionFactory.Create(method, new AIFunctionFactoryOptions { Name = "get_all_hotels", SerializerOptions = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions) { WriteIndented = false } })` changes the serializer used for the tool result; compare the size with `WriteToolCalls`.
- **Answer size**: a `ChatOptions.MaxOutputTokens` on the agent (`ChatClientAgentOptions`) caps the answer; with structured output, a truncated answer becomes a `JsonException`, so leave room.
- **Structured output for a list**: `RunAsync<List<Hotel>>` works because the framework wraps a non-object schema in an object for the service; with `ChatResponseFormat.ForJsonSchema<T>()` (Lab 02, scenarios 3 and 4) you need a wrapper type (`class HotelList { public List<Hotel> Hotels { get; set; } }`).
- **Other compact formats**: the same approach applies to any text format the model knows well (Markdown tables, key/value lines...): the tool returns a string, the instructions describe the answer, and your code parses it. Measure before choosing: a format the model reproduces badly costs more in retries than it saves in tokens.
- **Tool definitions cost tokens too**: Lab 04 (scenario 3) shows how giving the agent only the tools it needs reduces the input tokens of every run.

## Coming from an older version of the lab?

| Before (preview) | Now (1.22.0) |
|------------------|--------------|
| `Azure.AI.OpenAI` / `AzureOpenAIClient` | `OpenAI` / `OpenAIClient` + Azure OpenAI v1 endpoint |
| `chatClient.CreateAIAgent(...)`, variables typed `ChatClientAgent` | `chatClient.AsAIAgent(...)`, variables typed `AIAgent` |
| `AgentRunResponse` / `AgentRunResponse<T>` | `AgentResponse` / `AgentResponse<T>` |
| Third-party "TOON" serializer packages (different between the exercise and the solution; the exercise's one no longer restores) and a "Toon" answer that was in fact CSV | No third-party package: **CSV**, encoded and parsed by the provided `HotelCsv.cs` |
| Tool returning the raw text of `hotels.json` | Tool returning `IReadOnlyList<Hotel>`: the framework serializes the objects as JSON (what the model sees is measured) |
| TODO in `Tools/HotelTools.cs` | Only `Program.cs` has TODOs; the tools are provided |
| Scenario 2 answering 3 columns, scenario 1 answering full objects | Both scenarios return the same 9 columns for the same question, and a comparison table is displayed |
| `<NoWarn>MEAI001</NoWarn>`, `Microsoft.Extensions.Hosting` | Removed; `Microsoft.Extensions.Configuration.*` packages |
| API key in `appsettings.json` | API key in user secrets or environment variables |

## Useful Links

- [Microsoft Agent Framework documentation](https://learn.microsoft.com/agent-framework/overview/?pivots=programming-language-csharp)
- [Using function tools with an agent](https://learn.microsoft.com/agent-framework/agents/tools/function-tools?pivots=programming-language-csharp)
- [Producing Structured Outputs with agents](https://learn.microsoft.com/agent-framework/agents/structured-outputs?pivots=programming-language-csharp)
- [Official sample `Agent_Step02_StructuredOutput`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/Agents/Agent_Step02_StructuredOutput)
- [Microsoft.Extensions.AI: function calling](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai#tool-calling)
- [Azure OpenAI v1 API](https://learn.microsoft.com/azure/foundry/openai/api-version-lifecycle)

## Solution

If you get stuck, check the complete solution in the `Solution/` folder.
