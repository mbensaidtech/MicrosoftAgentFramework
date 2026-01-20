# Thread Repository Pattern

This folder contains the repository pattern implementation for MongoDB data access in the AI Agents Backend.

## 📁 Files

- **`IThreadRepository.cs`** - Repository interface defining the contract for thread data access
- **`ThreadRepository.cs`** - MongoDB implementation of the repository interface

## 🎯 Purpose

The ThreadRepository provides a clean abstraction layer for MongoDB operations related to conversation thread messages. Instead of directly accessing MongoDB throughout the application, all data access goes through the repository.

## ✨ Features

### Method Available

| Method | Description | Parameters | Returns |
|--------|-------------|------------|---------|
| `GetThreadMessagesAsync` | Get all messages for a specific thread | `threadId`, `cancellationToken` | `IEnumerable<ChatHistoryItem>` |

### Performance Optimizations

The repository automatically creates a MongoDB compound index for optimal query performance:

- **`idx_threadId_timestamp`** - Compound index on ThreadId and Timestamp for fast sorted queries

## 🔧 Registration

The repository is automatically registered in the dependency injection container via `ServiceCollectionExtensions.cs`:

```csharp
services.AddScoped<IThreadRepository, ThreadRepository>();
```

## 📖 Usage Examples

### Example 1: Inject in a Controller

```csharp
public class MyController : ControllerBase
{
    private readonly IThreadRepository _threadRepository;
    
    public MyController(IThreadRepository threadRepository)
    {
        _threadRepository = threadRepository;
    }
    
    [HttpGet("thread/{threadId}/messages")]
    public async Task<IActionResult> GetThreadMessages(string threadId)
    {
        var messages = await _threadRepository.GetThreadMessagesAsync(threadId);
        return Ok(messages);
    }
}
```

### Example 2: Get Thread Messages

```csharp
// Get all messages for a specific thread
var messages = await _threadRepository.GetThreadMessagesAsync("abc123");

Console.WriteLine($"Thread has {messages.Count()} messages");

foreach (var message in messages)
{
    Console.WriteLine($"[{message.Timestamp}] {message.MessageText}");
}
```

## 🎮 API Endpoints

A `ThreadsController` has been created to expose the repository operations via REST API:

| HTTP Method | Endpoint | Description |
|-------------|----------|-------------|
| `GET` | `/api/threads/{threadId}/messages` | Get all messages for a specific thread |

### API Example

**Get thread messages:**
```bash
curl http://localhost:5016/api/threads/abc123/messages
```

**Response:**
```json
{
  "threadId": "abc123",
  "messageCount": 5,
  "messages": [
    {
      "key": "abc123msg1",
      "timestamp": 1234567890,
      "messageText": "Hello",
      "serializedMessage": "{...}"
    }
  ]
}
```

## 🏗️ Architecture Benefits

### ✅ Separation of Concerns
- Data access logic is isolated in the repository
- Controllers and services don't need to know MongoDB details
- Easy to switch database implementations

### ✅ Testability
- Easy to mock `IThreadRepository` in unit tests
- Can create in-memory implementations for testing
- No need to mock MongoDB directly

### ✅ Maintainability
- All MongoDB queries in one place
- Easy to optimize or modify queries
- Centralized error handling

### ✅ Reusability
- Repository can be injected in controllers, services, or agents
- Consistent API across the application
- No duplicate MongoDB code

## 🔍 Data Model

The repository works with `ChatHistoryItem` from the Stores folder:

```csharp
public sealed class ChatHistoryItem
{
    public string? Key { get; set; }              // Unique message key
    public string? ThreadId { get; set; }         // Thread identifier
    public long Timestamp { get; set; }           // Unix timestamp
    public string? SerializedMessage { get; set; } // Full message JSON
    public string? MessageText { get; set; }      // Message text content
}
```

## 🔐 Configuration

The repository uses MongoDB settings from `appsettings.json`:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://admin:password123@localhost:27017",
    "DatabaseName": "aiagents_db",
    "ThreadMessagesCollectionName": "threadMessages"
  }
}
```

Or via environment variables:

```bash
export MongoDB__ConnectionString="mongodb://admin:password123@localhost:27017"
export MongoDB__DatabaseName="aiagents_db"
export MongoDB__ThreadMessagesCollectionName="threadMessages"
```

> 📝 **Note:** The repository uses the `threadMessages` collection, which is separate from the `chat_history` collection used by the AI agents' conversation memory system.

## 🧪 Testing Example

```csharp
[Fact]
public async Task GetThreadMessages_ReturnsMessages()
{
    // Arrange
    var mockRepo = new Mock<IThreadRepository>();
    mockRepo.Setup(x => x.GetThreadMessagesAsync("test123", default))
            .ReturnsAsync(new List<ChatHistoryItem>
            {
                new() { ThreadId = "test123", MessageText = "Hello" },
                new() { ThreadId = "test123", MessageText = "World" }
            });
    
    var controller = new ThreadsController(mockRepo.Object, logger);
    
    // Act
    var result = await controller.GetThreadMessages("test123", default);
    
    // Assert
    Assert.NotNull(result);
    Assert.Equal(2, result.Value.MessageCount);
}
```

## 📚 Best Practices

1. **Always use the repository** - Never access MongoDB directly in controllers or services
2. **Inject via interface** - Always inject `IThreadRepository`, not `ThreadRepository`
3. **Handle cancellation** - Always pass `CancellationToken` for async operations
4. **Validate input** - The repository validates thread IDs, but controllers should too
5. **Log operations** - The repository logs all operations for debugging

## 🔮 Future Enhancements

Potential additions to the repository:

- `GetAllThreadIdsAsync()` - Get all unique thread IDs
- `GetThreadMessageCountAsync()` - Get message count without fetching all messages
- `DeleteThreadAsync()` - Delete a thread and all its messages
- `GetThreadsByDateRangeAsync()` - Filter threads by date
- `SearchThreadsAsync(string query)` - Full-text search in messages
- `AddMessageAsync()` - Add a single message to a thread
- `UpdateMessageAsync()` - Update a specific message
- `GetLatestMessagesAsync(int count)` - Get N most recent messages for a thread

## 📄 License

This is part of the Microsoft Agent Framework learning materials.

