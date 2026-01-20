# Thread History Feature

## Overview

This document explains the **Thread History** feature that allows users to load previous conversation messages from MongoDB using the `contextId` (which serves as the `threadId` in the database).

## Architecture

### Backend Components

#### 1. **ThreadRepository** (`AIAgentsBackend/Repositories/ThreadRepository.cs`)
- **Purpose**: Data access layer for MongoDB thread messages
- **Collection**: `threadMessages` in MongoDB
- **Key Method**: `GetThreadMessagesAsync(string threadId)`
- **Features**:
  - Queries MongoDB for all messages with matching `threadId`
  - Orders messages by timestamp
  - Creates index on `threadId` and `timestamp` for performance

#### 2. **ThreadsController** (`AIAgentsBackend/Controllers/ThreadsController.cs`)
- **Purpose**: REST API endpoint for thread operations
- **Endpoint**: `GET /api/threads/{threadId}/messages`
- **Response**: Returns `ThreadMessagesResponse` with message count and message list
- **Error Handling**: Returns 404 if thread not found

#### 3. **DTOs** (`AIAgentsBackend/Controllers/Models/`)
- `ThreadMessagesResponse`: Contains threadId, messageCount, and messages array
- `MessageDto`: Individual message with key, timestamp, messageText, and serializedMessage

### Frontend Components

#### 1. **API Service** (`AIAgentsFrontend/src/services/api.ts`)
- **Function**: `getThreadMessages(threadId: string)`
- **Method**: GET request to `/api/threads/{threadId}/messages`
- **Returns**: `ApiResultWithDebug<ThreadMessagesResponse>`
- **Features**:
  - Includes debug information (timing, status, errors)
  - Handles 404 errors gracefully
  - Logs all operations to console

#### 2. **ChatPage Component** (`AIAgentsFrontend/src/pages/ChatPage.tsx`)
- **Function**: `loadThreadHistory()`
- **Features**:
  - Validates that a `contextId` exists before loading
  - Fetches messages from backend
  - Parses serialized messages to determine role (user/agent)
  - Replaces current chat messages with loaded history
  - Shows loading state during fetch
  - Displays error alerts if loading fails
  - Adds debug information to debug panel

#### 3. **UI Button**
- **Location**: Agent header (next to context badge)
- **Visibility**: Only shown when a `contextId` exists
- **Label**: "📜 Load History" (changes to "⏳ Loading..." during fetch)
- **Style**: Purple gradient matching the app theme

## How It Works

### Step-by-Step Flow

1. **User starts a conversation**
   - Frontend generates a `contextId` (e.g., `mbensaid-mkl99c65-vtevfu`)
   - This `contextId` is sent with every message to the backend
   - Backend stores messages in MongoDB with this `contextId` as the `threadId`

2. **User clicks "Load History" button**
   - Frontend calls `loadThreadHistory()` function
   - Function validates that a `contextId` exists

3. **Backend request**
   - Frontend sends GET request to `/api/threads/{contextId}/messages`
   - Backend queries MongoDB `threadMessages` collection
   - Returns all messages with matching `threadId`, ordered by timestamp

4. **Message parsing**
   - Frontend receives `ThreadMessagesResponse`
   - Each message's `serializedMessage` is parsed to determine role
   - Messages are converted to `ChatMessage` format

5. **UI update**
   - Current chat messages are replaced with loaded history
   - Messages display in chronological order
   - User can continue the conversation from where they left off

## Database Schema

### Collection: `threadMessages`

```json
{
  "_id": "ObjectId",
  "ThreadId": "mbensaid-mkl99c65-vtevfu",
  "Key": "msg-1737123456789",
  "Timestamp": 1737123456789,
  "MessageText": "Which country was the first to land a human on the Moon?",
  "SerializedMessage": "{\"role\":\"user\",\"content\":\"Which country was the first to land a human on the Moon?\"}"
}
```

### Index
- Compound index on `ThreadId` (ascending) + `Timestamp` (ascending)
- Name: `idx_threadId_timestamp`
- Purpose: Optimize queries for fetching thread messages

## Configuration

### Backend (`appsettings.json`)

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://admin:password123@localhost:27017",
    "DatabaseName": "aiagents_db",
    "ChatMessageStoreCollectionName": "chat_history",
    "ThreadMessagesCollectionName": "threadMessages"
  }
}
```

### Dependency Injection (`ServiceCollectionExtensions.cs`)

```csharp
// Register Thread Repository
services.AddScoped<IThreadRepository, ThreadRepository>();
```

## API Reference

### GET /api/threads/{threadId}/messages

**Description**: Retrieves all messages for a specific thread.

**Parameters**:
- `threadId` (path parameter): The thread ID (contextId) to fetch messages for

**Response** (200 OK):
```json
{
  "threadId": "mbensaid-mkl99c65-vtevfu",
  "messageCount": 4,
  "messages": [
    {
      "key": "msg-1737123456789",
      "timestamp": 1737123456789,
      "messageText": "Which country was the first to land a human on the Moon?",
      "serializedMessage": "{\"role\":\"user\",\"content\":\"Which country was the first to land a human on the Moon?\"}"
    },
    {
      "key": "msg-1737123456790",
      "timestamp": 1737123456790,
      "messageText": "The United States was the first country to land a human on the Moon...",
      "serializedMessage": "{\"role\":\"assistant\",\"content\":\"The United States was the first country to land a human on the Moon...\"}"
    }
  ]
}
```

**Response** (404 Not Found):
```json
{
  "error": "Thread with ID 'invalid-thread-id' not found or has no messages."
}
```

## Usage Example

### 1. Start a conversation
```
User: "Which country was the first to land a human on the Moon?"
Agent: "The United States was the first country to land a human on the Moon..."
```

### 2. Note the contextId
The UI shows: `ID: mbensaid-mkl9...`

### 3. Click "Load History"
The button appears next to the context badge when a conversation is active.

### 4. View loaded messages
All previous messages from this thread are loaded and displayed in the chat.

## Error Handling

### Frontend
- **No contextId**: Shows alert "No active thread. Start a conversation first."
- **404 Error**: Shows alert "Failed to load thread history: Thread not found: {threadId}"
- **Network Error**: Shows alert with error message
- **Parse Error**: Logs warning and uses `messageText` as fallback

### Backend
- **Invalid threadId**: Returns 400 Bad Request
- **Thread not found**: Returns 404 Not Found
- **Database error**: Returns 500 Internal Server Error

## Security Considerations

1. **ContextId Validation**: The backend validates the `contextId` signature to prevent tampering
2. **User Authorization**: In production, add user authentication to ensure users can only access their own threads
3. **Rate Limiting**: Consider adding rate limiting to prevent abuse of the history endpoint

## Future Enhancements

1. **Pagination**: Load messages in chunks for very long threads
2. **Search**: Add ability to search within thread history
3. **Export**: Allow users to export thread history as JSON/PDF
4. **Thread List**: Show a list of all user threads with preview
5. **Thread Metadata**: Store thread title, creation date, last activity
6. **Partial Load**: Load only recent messages and fetch older ones on demand

## Testing

### Manual Testing Steps

1. **Start backend**: `cd AIAgentsBackend && dotnet run`
2. **Start frontend**: `cd AIAgentsFrontend && npm run dev`
3. **Login** to the application
4. **Send messages** to create a conversation
5. **Note the contextId** from the UI
6. **Refresh the page** (conversation will be lost from UI)
7. **Start a new conversation** (to get the same contextId, you'd need to manually set it)
8. **Click "Load History"** to restore the conversation

### API Testing with curl

```bash
# Get thread messages
curl -X GET http://localhost:5016/api/threads/mbensaid-mkl99c65-vtevfu/messages

# Expected response
{
  "threadId": "mbensaid-mkl99c65-vtevfu",
  "messageCount": 4,
  "messages": [...]
}
```

### MongoDB Query

```javascript
// Connect to MongoDB
use aiagents_db

// Find all messages for a thread
db.threadMessages.find({ ThreadId: "mbensaid-mkl99c65-vtevfu" }).sort({ Timestamp: 1 })

// Count messages in a thread
db.threadMessages.countDocuments({ ThreadId: "mbensaid-mkl99c65-vtevfu" })
```

## Troubleshooting

### Issue: "Thread not found" error
**Solution**: Verify the thread exists in MongoDB and the `threadId` is correct

### Issue: Messages not loading
**Solution**: 
1. Check browser console for errors
2. Verify backend is running
3. Check MongoDB connection
4. Verify collection name in `appsettings.json`

### Issue: Wrong messages displayed
**Solution**: Verify the `contextId` matches the thread you want to load

### Issue: Parsing errors
**Solution**: Check that `serializedMessage` contains valid JSON with `role` and `content` fields

## Code References

### Key Files Modified
1. `AIAgentsBackend/Repositories/IThreadRepository.cs` - Interface
2. `AIAgentsBackend/Repositories/ThreadRepository.cs` - Implementation
3. `AIAgentsBackend/Controllers/ThreadsController.cs` - REST API
4. `AIAgentsBackend/Controllers/Models/ThreadMessagesResponse.cs` - DTO
5. `AIAgentsBackend/Controllers/Models/MessageDto.cs` - DTO
6. `AIAgentsFrontend/src/services/api.ts` - API client
7. `AIAgentsFrontend/src/pages/ChatPage.tsx` - UI component
8. `AIAgentsFrontend/src/pages/ChatPage.css` - Styles

## Conclusion

The Thread History feature provides a seamless way for users to retrieve and continue previous conversations. It leverages the existing `contextId` mechanism and MongoDB storage to offer a complete conversation history experience.

