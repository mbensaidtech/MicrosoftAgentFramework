# Middleware Unification - AgentContextMiddleware

## 📋 Summary

Unified the contextId extraction logic from duplicated controller code into a single, centralized middleware that handles **all agent endpoints** (both A2A and Frontend).

## 🔄 What Changed

### ✅ **BEFORE** - Duplicated Manual Extraction

Each controller manually extracted contextId:

```csharp
// OrchestratorAgentController.cs
var contextId = request.ContextId ?? Guid.NewGuid().ToString("N");
HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;

// MessageFormulatorAgentController.cs
var contextId = request.ContextId ?? Guid.NewGuid().ToString("N");
HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;

// ... repeated in every controller
```

**Problems:**
- ❌ Code duplication across multiple controllers
- ❌ Inconsistent behavior between A2A and Frontend
- ❌ No centralized validation or security
- ❌ Hard to maintain and test

---

### ✅ **AFTER** - Unified Middleware

One middleware handles all agent endpoints:

```csharp
// AgentContextMiddleware.cs - Runs automatically for ALL agent requests
public async Task InvokeAsync(HttpContext context)
{
    if (context.Request.Path.StartsWithSegments("/a2a") || 
        context.Request.Path.StartsWithSegments("/api/agents"))
    {
        // Extract contextId from A2A format (params.contextId) OR Frontend format (direct contextId)
        // Generate new GUID if not provided
        // Store in HttpContext.Items for entire request pipeline
    }
}
```

**Controllers now simply:**

```csharp
// Just read from HttpContext.Items (already populated by middleware)
var contextId = HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] as string;
```

**Benefits:**
- ✅ **DRY** - Single source of truth
- ✅ **Consistency** - Same behavior everywhere
- ✅ **Security** - Centralized validation point
- ✅ **Maintainability** - Update once, affects all
- ✅ **Testability** - Test middleware independently

---

## 📁 Files Created/Modified

### **Created:**
1. **`Agents/Middleware/AgentContextMiddleware.cs`** - New unified middleware

### **Modified:**
1. **`Extensions/ApplicationBuilderExtensions.cs`** - Added `UseAgentContext()` method
2. **`Program.cs`** - Changed from `UseA2AContext()` to `UseAgentContext()`
3. **`Controllers/OrchestratorAgentController.cs`** - Removed manual contextId extraction
4. **`Controllers/MessageFormulatorAgentController.cs`** - Removed manual contextId extraction

### **Legacy Code:**
- **`Controllers/Base/AgentControllerBase.cs`** - Still has manual extraction (for legacy controllers not yet migrated)

---

## 🎯 How It Works

### **Request Flow:**

```
┌─────────────────────────────────────────────────────┐
│ 1. HTTP Request arrives                             │
│    POST /api/agents/orchestrator/stream             │
│    Body: { "contextId": "...", "message": "..." }   │
└───────────────────┬─────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────┐
│ 2. AgentContextMiddleware (runs BEFORE controller)  │
│    - Reads request body                             │
│    - Extracts contextId (A2A or Frontend format)    │
│    - Generates new GUID if not provided             │
│    - Stores in HttpContext.Items["A2A_ContextId"]   │
│    - Adds metadata: IsNewContext, RequestType       │
└───────────────────┬─────────────────────────────────┘
                    │
                    ▼
┌─────────────────────────────────────────────────────┐
│ 3. Controller Action (OrchestratorAgentController)  │
│    - Reads contextId from HttpContext.Items         │
│    - Uses it for MongoDB, logging, etc.             │
│    - No need to extract or validate manually        │
└─────────────────────────────────────────────────────┘
```

---

## 🔑 Middleware Features

### **1. Multi-Format Support**

Automatically detects and handles both formats:

**A2A Format:**
```json
{
  "params": {
    "contextId": "user-123-ai-abc"
  }
}
```

**Frontend Format:**
```json
{
  "contextId": "conv-mbensaid-xyz-ai-abc123",
  "message": "Hello"
}
```

### **2. Auto-Generation**

If no contextId is provided:
```csharp
contextId = Guid.NewGuid().ToString("N");
// Example: "a1b2c3d4e5f67890abcdef1234567890"
```

### **3. Metadata Storage**

Stores useful metadata for controllers:
- `HttpContext.Items["A2A_ContextId"]` - The contextId
- `HttpContext.Items["IsNewContext"]` - `true` if newly generated
- `HttpContext.Items["RequestType"]` - `"A2A"` or `"Frontend"`

### **4. Colored Console Logging**

```
[AgentContextMiddleware] Extracted contextId from Frontend: conv-mbensaid-xyz-ai-abc123
[AgentContextMiddleware] Generated new contextId: a1b2c3d4e5f67890...
```

---

## 🚀 Usage in Controllers

### **Before (Manual):**
```csharp
[HttpPost("stream")]
public async Task Stream([FromBody] MessageFormulatorRequest request, ...)
{
    // Manual extraction - REPEATED IN EVERY CONTROLLER
    var contextId = request.ContextId ?? Guid.NewGuid().ToString("N");
    HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] = contextId;
    
    // ... rest of code
}
```

### **After (Middleware):**
```csharp
[HttpPost("stream")]
public async Task Stream([FromBody] MessageFormulatorRequest request, ...)
{
    // Just read from HttpContext.Items - MIDDLEWARE ALREADY DID THE WORK
    var contextId = HttpContext.Items[MongoVectorChatMessageStore.ContextIdKey] as string 
                    ?? throw new InvalidOperationException("ContextId not found. Ensure AgentContextMiddleware is registered.");
    
    var isNewThread = HttpContext.Items["IsNewContext"] as bool? ?? true;
    var requestType = HttpContext.Items["RequestType"] as string ?? "Unknown";
    
    // ... rest of code
}
```

---

## 🧪 Testing

### **Unit Test Example:**

```csharp
[Fact]
public async Task AgentContextMiddleware_ExtractsContextId_FromFrontendFormat()
{
    // Arrange
    var context = new DefaultHttpContext();
    context.Request.Method = "POST";
    context.Request.Path = "/api/agents/orchestrator/stream";
    context.Request.Body = CreateJsonBody(new { contextId = "test-123", message = "Hi" });
    
    var middleware = new AgentContextMiddleware(next, logger);
    
    // Act
    await middleware.InvokeAsync(context);
    
    // Assert
    Assert.Equal("test-123", context.Items["A2A_ContextId"]);
    Assert.Equal("Frontend", context.Items["RequestType"]);
    Assert.False((bool)context.Items["IsNewContext"]);
}
```

---

## 📊 Comparison Table

| Aspect | Old (Manual) | New (Middleware) |
|--------|-------------|------------------|
| **Lines of code** | ~5 lines × N controllers | 1 middleware (used by all) |
| **Consistency** | ❌ Each controller different | ✅ Same behavior everywhere |
| **Testing** | ❌ Test each controller | ✅ Test middleware once |
| **Maintenance** | ❌ Update N controllers | ✅ Update 1 middleware |
| **Security** | ❌ No centralized validation | ✅ Centralized validation point |
| **Logging** | ❌ Inconsistent or missing | ✅ Centralized colored logs |
| **Format support** | ❌ Manual parsing each time | ✅ Auto-detects format |

---

## 🎓 Best Practices Applied

1. **Single Responsibility** - Middleware does ONE thing: extract contextId
2. **DRY (Don't Repeat Yourself)** - Logic defined once, used everywhere
3. **Separation of Concerns** - Controllers focus on business logic, middleware handles infrastructure
4. **Open/Closed Principle** - Easy to extend (add new formats) without modifying controllers
5. **Dependency Injection** - Middleware uses DI for logger and services

---

## 🔮 Future Enhancements

Potential additions to the middleware:

- **Authentication/Authorization** - Validate JWT tokens
- **Rate Limiting** - Per contextId or customer
- **Request Validation** - JSON schema validation
- **Correlation IDs** - For distributed tracing
- **Performance Metrics** - Request timing and monitoring

---

## 📝 Migration Checklist

If you have other controllers that still use manual extraction:

- [ ] Ensure `app.UseAgentContext()` is registered in `Program.cs`
- [ ] Replace manual `var contextId = request.ContextId ?? Guid.NewGuid()` with `HttpContext.Items[ContextIdKey]`
- [ ] Remove `HttpContext.Items[ContextIdKey] = contextId` assignments
- [ ] Use `IsNewContext` and `RequestType` metadata if needed
- [ ] Test with both A2A and Frontend requests
- [ ] Update unit tests to test middleware instead of controller logic

---

## 🎯 Key Takeaway

**Before:** Each controller manually extracts contextId (code duplication)  
**After:** One middleware automatically extracts contextId for ALL endpoints (centralized, consistent, maintainable)

This is a **textbook example** of good software architecture! 🏆
