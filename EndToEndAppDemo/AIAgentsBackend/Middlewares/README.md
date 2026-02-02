# Middlewares Organization

This folder contains **two distinct types of middlewares** that serve different purposes in the application pipeline.

---

## 📂 Folder Structure

```
Middlewares/
├── Http/              ← ASP.NET Core HTTP Request Middlewares
│   └── AgentContextMiddleware.cs
│
├── Agent/             ← AI Agent Function/Tool Call Middlewares
│   └── FunctionCallLoggingMiddleware.cs
│
└── README.md          ← This file
```

---

## 🔄 Two Types of Middlewares

### 1️⃣ **HTTP Middlewares** (`Middlewares/Http/`)

**Purpose:** Process HTTP requests in the ASP.NET Core pipeline  
**Execution:** Runs **before** controllers, for every HTTP request  
**Interface:** Uses ASP.NET Core's `RequestDelegate` pattern

#### Examples:

**`AgentContextMiddleware.cs`**
- **What it does:** Extracts `contextId` from HTTP request body (supports both A2A and Frontend formats)
- **When it runs:** For all `/api/agents/*` and `/a2a/*` endpoints
- **Pattern:**
  ```csharp
  public class AgentContextMiddleware
  {
      private readonly RequestDelegate next;
      
      public async Task InvokeAsync(HttpContext context)
      {
          // Parse HTTP request body
          // Extract contextId from A2A format (params.contextId) or Frontend format (contextId)
          // Generate new GUID if not provided
          // Store in HttpContext.Items
          await next(context);
      }
  }
  ```
- **Registration:** `app.UseAgentContext()` in `Program.cs`

---

### 2️⃣ **Agent Middlewares** (`Middlewares/Agent/`)

**Purpose:** Intercept AI agent function/tool calls  
**Execution:** Runs **during** AI agent execution, when calling tools  
**Interface:** Uses Microsoft.Extensions.AI's agent middleware pattern

#### Examples:

**`FunctionCallLoggingMiddleware.cs`**
- **What it does:** Logs AI agent tool invocations (function name, arguments)
- **When it runs:** Every time the AI agent calls a tool/function
- **Pattern:**
  ```csharp
  public static class FunctionCallLoggingMiddleware
  {
      public static async ValueTask<object?> LogFunctionCallAsync(
          AIAgent callingAgent,
          FunctionInvocationContext context,
          Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
          CancellationToken cancellationToken)
      {
          // Log function call details
          var result = await next(context, cancellationToken);
          // Return result
          return result;
      }
  }
  ```
- **Registration:** `.WithMiddleware(FunctionCallLoggingMiddleware.LogFunctionCallAsync)` in `FluentAIAgentBuilder`

---

## 📊 Comparison Table

| Aspect | HTTP Middleware | Agent Middleware |
|--------|----------------|------------------|
| **Location** | `Middlewares/Http/` | `Middlewares/Agent/` |
| **Purpose** | Process HTTP requests | Intercept AI tool calls |
| **Pipeline** | ASP.NET Core HTTP pipeline | AI Agent execution pipeline |
| **When it runs** | Before controller action | During agent tool execution |
| **Interface** | `RequestDelegate`, `HttpContext` | `FunctionInvocationContext`, `AIAgent` |
| **Registration** | `app.UseXxx()` in `Program.cs` | `.WithMiddleware()` in agent builder |
| **Example** | Extract contextId, authentication | Log tool calls, validate permissions |

---

## 🔄 Execution Flow

### **HTTP Request with AI Agent:**

```
1. HTTP Request arrives
   POST /api/agents/orchestrator/stream
   Body: { "message": "...", "contextId": "..." }
   
   ↓
   
2. 🌐 HTTP Middleware runs (Middlewares/Http/)
   ├── AgentContextMiddleware
   │   └── Extracts contextId from body
   │   └── Stores in HttpContext.Items
   
   ↓
   
3. 🎯 Controller Action runs
   └── OrchestratorAgentController.Stream()
       └── Gets agent from factory
       └── Calls agent.RunStreamingAsync()
   
   ↓
   
4. 🤖 AI Agent Execution
   └── Agent decides to call a tool
   
   ↓
   
5. 🔧 Agent Middleware runs (Middlewares/Agent/)
   ├── FunctionCallLoggingMiddleware
   │   └── Logs: "Tool Call: 'order_agent' (Args: [query = ...])"
   │   └── Calls next middleware
   └── Actual tool execution
   
   ↓
   
6. 📤 Response streamed back to client
```

---

## 🎓 Key Differences

### **HTTP Middleware:**
- ✅ Runs **once** per HTTP request
- ✅ Has access to `HttpContext`, `Request`, `Response`
- ✅ Can short-circuit the pipeline (return without calling `next`)
- ✅ Used for: Authentication, logging, context extraction, CORS, etc.

### **Agent Middleware:**
- ✅ Runs **multiple times** per request (once per tool call)
- ✅ Has access to `FunctionInvocationContext`, tool arguments
- ✅ Can intercept, modify, or log function calls
- ✅ Used for: Tool call logging, permission checks, argument validation, etc.

---

## 📝 Naming Convention

To avoid confusion:

| Type | Naming Pattern | Example |
|------|---------------|---------|
| **HTTP Middleware** | `{Purpose}Middleware` | `AgentContextMiddleware` |
| **Agent Middleware** | `{Purpose}Middleware` | `FunctionCallLoggingMiddleware` |

Both use the same suffix, but are in **different folders** to clearly separate their purpose.

---

## 🔧 How to Add New Middlewares

### **Adding HTTP Middleware:**

1. Create file in `Middlewares/Http/`
2. Implement `InvokeAsync(HttpContext context)` pattern
3. Register in `Program.cs` using `app.UseXxx()`

```csharp
// Middlewares/Http/MyHttpMiddleware.cs
namespace AIAgentsBackend.Middlewares.Http;

public class MyHttpMiddleware
{
    private readonly RequestDelegate next;
    
    public MyHttpMiddleware(RequestDelegate next) => this.next = next;
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Your logic here
        await next(context);
    }
}

// Program.cs
app.UseMyHttp();
```

---

### **Adding Agent Middleware:**

1. Create file in `Middlewares/Agent/`
2. Implement `ValueTask<object?> Method(AIAgent, FunctionInvocationContext, Func, CancellationToken)` pattern
3. Register in agent builder using `.WithMiddleware()`

```csharp
// Middlewares/Agent/MyAgentMiddleware.cs
namespace AIAgentsBackend.Middlewares.Agent;

public static class MyAgentMiddleware
{
    public static async ValueTask<object?> ExecuteAsync(
        AIAgent callingAgent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        // Your logic here
        return await next(context, cancellationToken);
    }
}

// AgentFactory.cs
agent.AsBuilder()
     .Use(MyAgentMiddleware.ExecuteAsync)
     .Build();
```

---

## 🎯 Summary

- **`Middlewares/Http/`** → ASP.NET Core HTTP request pipeline
- **`Middlewares/Agent/`** → AI agent tool call pipeline
- **Different purposes, different interfaces, different execution contexts**
- **Organized in separate folders to avoid confusion** ✅

This clear separation makes it obvious which middleware operates at which level of the application! 🎓
