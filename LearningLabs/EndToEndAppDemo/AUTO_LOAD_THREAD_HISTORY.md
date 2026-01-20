# Auto-Load Thread History Feature

## 🎯 Overview

The application now **automatically loads your previous conversation** when you refresh the page or return to the chat! Your contextId is saved to your browser's localStorage, and on page load, the app fetches your thread history from MongoDB and displays it.

## ✨ Key Features

### 1. **Persistent ContextId**
- When you start a conversation, a unique `contextId` is generated
- This `contextId` is automatically saved to `localStorage`
- When you refresh the page, the same `contextId` is restored
- All your messages are associated with this `contextId` in MongoDB

### 2. **Auto-Load on Page Load**
- When the chat page loads and you're logged in:
  1. ✅ The app checks for a saved `contextId` in localStorage
  2. ✅ If found, it automatically fetches messages from the backend
  3. ✅ Messages are loaded and displayed in the chat
  4. ✅ You can continue the conversation from where you left off

### 3. **Manual Load (Still Available)**
- The "📜 Load History" button is still available
- Use it to refresh/reload messages from the database
- Useful if you want to see the latest state from the database

### 4. **New Chat**
- Clear Chat button (⟲) clears:
  - Current messages from UI
  - ContextId from state
  - ContextId from localStorage
  - Debug history
- Next message starts a fresh conversation with a new contextId

## 🔄 How It Works

### Flow Diagram

```
┌─────────────────────────────────────────────────────┐
│  1. User Sends First Message                        │
│     → Generate contextId                            │
│     → Save to localStorage                          │
│     → Save to state                                 │
└─────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────┐
│  2. User Sends More Messages                        │
│     → All messages saved to MongoDB                 │
│     → ThreadId = contextId                          │
└─────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────┐
│  3. User Refreshes Page                             │
│     → Page loads                                    │
│     → Check localStorage for contextId              │
└─────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────┐
│  4. Auto-Load Thread History                        │
│     → Fetch messages from MongoDB                   │
│     → Parse and display messages                    │
│     → Show loading indicator                        │
└─────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────┐
│  5. Continue Conversation                           │
│     → User can send new messages                    │
│     → New messages added to same thread             │
└─────────────────────────────────────────────────────┘
```

## 📝 Code Changes

### 1. Session Utility (`src/utils/session.ts`)

Added three new functions:

```typescript
// Save contextId to localStorage
export function saveContextId(contextId: string): void {
  localStorage.setItem(CONTEXT_ID_KEY, contextId);
}

// Get contextId from localStorage
export function getContextId(): string | null {
  return localStorage.getItem(CONTEXT_ID_KEY);
}

// Clear contextId from localStorage
export function clearContextId(): void {
  localStorage.removeItem(CONTEXT_ID_KEY);
}
```

### 2. ChatPage Component (`src/pages/ChatPage.tsx`)

#### a) Load contextId and Auto-Fetch History on Mount

```typescript
useEffect(() => {
  if (!username) return; // Only load if user is logged in

  const savedContextId = getContextId();
  if (savedContextId) {
    console.log('[ChatPage] Found saved contextId:', savedContextId);
    setContextId(savedContextId);
    
    // Auto-load thread history
    const autoLoadHistory = async () => {
      setIsLoadingHistory(true);
      try {
        const { data, debug } = await getThreadMessages(savedContextId);
        
        if (data.messageCount > 0) {
          // Convert and display messages
          const loadedMessages = data.messages.map(...);
          setMessages(loadedMessages);
          console.log(`Auto-loaded ${data.messageCount} messages`);
        }
      } catch (error) {
        // Handle errors gracefully
      } finally {
        setIsLoadingHistory(false);
      }
    };

    autoLoadHistory();
  }
}, [username]);
```

#### b) Save ContextId When Created

```typescript
const getOrCreateContextId = (): string => {
  if (contextId) return contextId;
  const newContextId = generateContextId();
  setContextId(newContextId);
  saveContextId(newContextId); // 👈 Save to localStorage
  return newContextId;
};
```

#### c) Clear ContextId on Logout and New Chat

```typescript
const handleLogout = () => {
  clearSession();
  clearContextId(); // 👈 Clear from localStorage
  navigate('/login');
};

const clearChat = () => {
  setMessages([]);
  setContextId(undefined);
  clearContextId(); // 👈 Clear from localStorage
  setDebugHistory([]);
  setExpandedDebugEntries(new Set());
};
```

#### d) Loading State in Empty State

```typescript
{messages.length === 0 ? (
  <div className="empty-state">
    {isLoadingHistory ? (
      <>
        <p>⏳ Loading conversation history...</p>
        <p className="hint">Please wait while we restore your previous messages</p>
      </>
    ) : (
      <>
        <p>Start a conversation with {selectedAgent?.name}</p>
        <p className="hint">{selectedAgent?.description}</p>
      </>
    )}
  </div>
) : (
  // Display messages
)}
```

## 🧪 Testing the Feature

### Test Scenario 1: Basic Auto-Load

1. **Start a conversation**
   ```
   User: "What is AI?"
   Agent: "AI stands for Artificial Intelligence..."
   ```

2. **Check localStorage**
   - Open DevTools → Application → Local Storage
   - Find key: `aiagent_context_id`
   - Value should be something like: `mbensaid-mkl99c65-vtevfu`

3. **Refresh the page** (F5 or Ctrl+R)

4. **Observe the behavior**
   - ✅ Page loads with "⏳ Loading conversation history..." message
   - ✅ Previous messages appear in the chat
   - ✅ ContextId badge shows the same ID
   - ✅ You can continue the conversation

5. **Console logs**
   ```
   [ChatPage] Found saved contextId: mbensaid-mkl99c65-vtevfu
   [Threads] Fetching messages for threadId: mbensaid-mkl99c65-vtevfu
   [Threads] Retrieved 2 messages for thread mbensaid-mkl99c65-vtevfu
   [ChatPage] Auto-loaded 2 messages from thread mbensaid-mkl99c65-vtevfu
   ```

### Test Scenario 2: New Chat After Refresh

1. **Have a conversation** (as in Test 1)

2. **Refresh the page** → Messages are restored

3. **Click "⟲" (Clear Chat) button**

4. **Send a new message**

5. **Observe the behavior**
   - ✅ A new contextId is generated
   - ✅ Old contextId is cleared from localStorage
   - ✅ New conversation starts fresh

### Test Scenario 3: No Previous Messages

1. **Clear localStorage**
   - DevTools → Application → Local Storage
   - Delete `aiagent_context_id`

2. **Refresh the page**

3. **Observe the behavior**
   - ✅ Empty state shows: "Start a conversation with..."
   - ✅ No loading indicator
   - ✅ No error messages

### Test Scenario 4: Logout and Login

1. **Have a conversation**

2. **Click Logout**

3. **Login again** (same or different user)

4. **Observe the behavior**
   - ✅ For same user: Previous conversation may load (if contextId was preserved)
   - ✅ For different user: Fresh start
   - ✅ ContextId is cleared on logout

### Test Scenario 5: Multiple Tabs

1. **Open chat in Tab 1**
   - Send message: "Hello"
   - Note the contextId

2. **Open chat in Tab 2** (same browser)
   - Should see the same conversation
   - Same contextId from localStorage

3. **Send message in Tab 2**: "How are you?"

4. **Refresh Tab 1**
   - Should see both messages
   - Messages synced via MongoDB

## 🎨 UI/UX Improvements

### Loading States

1. **Initial Load with Saved ContextId**
   ```
   ⏳ Loading conversation history...
   Please wait while we restore your previous messages
   ```

2. **Initial Load without ContextId**
   ```
   Start a conversation with History Agent
   I help you review your chat history and continue...
   ```

3. **Manual Reload Button**
   - Label: "📜 Load History"
   - Loading: "⏳ Loading..."
   - Only visible when contextId exists

### Console Logging

All operations log to console for debugging:

```javascript
// On page load
[ChatPage] Found saved contextId: mbensaid-mkl99c65-vtevfu
[ChatPage] Auto-loading thread history for: mbensaid-mkl99c65-vtevfu

// On fetch complete
[Threads] Fetching messages for threadId: mbensaid-mkl99c65-vtevfu
[Threads] Retrieved 4 messages for thread mbensaid-mkl99c65-vtevfu
[ChatPage] Auto-loaded 4 messages from thread mbensaid-mkl99c65-vtevfu

// On new contextId creation
[ChatPage] Created and saved new contextId: mbensaid-mkl9abcd-xyzwvu

// On chat clear
[ChatPage] Chat cleared. Ready for new conversation.
```

## 🔒 Data Persistence

### What's Stored in localStorage

```javascript
// Key: aiagent_context_id
// Value: "mbensaid-mkl99c65-vtevfu"
localStorage.getItem('aiagent_context_id')
```

### What's Stored in MongoDB

```json
{
  "_id": "ObjectId(...)",
  "ThreadId": "mbensaid-mkl99c65-vtevfu",
  "Key": "msg-1737123456789",
  "Timestamp": 1737123456789,
  "MessageText": "What is AI?",
  "SerializedMessage": "{\"role\":\"user\",\"content\":\"What is AI?\"}"
}
```

### Data Lifecycle

1. **Message Sent**
   - Saved to MongoDB with ThreadId = contextId
   - ContextId saved to localStorage

2. **Page Refresh**
   - ContextId loaded from localStorage
   - Messages fetched from MongoDB
   - UI displays loaded messages

3. **Clear Chat**
   - Messages cleared from UI
   - ContextId removed from localStorage
   - MongoDB messages remain (not deleted)

4. **Logout**
   - Session cleared
   - ContextId cleared from localStorage
   - MongoDB messages remain

## 🚨 Error Handling

### Scenario: MongoDB Down

```javascript
[ChatPage] Error auto-loading thread history: Failed to fetch
// User sees empty state (no error alert for auto-load)
// User can still start a new conversation
```

### Scenario: Thread Not Found (404)

```javascript
[ChatPage] No previous messages found or error loading: Thread not found
// Silently handled, no alert shown
// User sees empty state
```

### Scenario: Invalid ContextId

```javascript
// If localStorage contains invalid contextId
// Backend returns 404
// Frontend handles gracefully, shows empty state
```

### Scenario: Parse Error

```javascript
[ChatPage] Failed to parse serialized message: SyntaxError
// Uses messageText as fallback
// Message still displays with default role
```

## 🎓 Use Cases

### Use Case 1: Daily Work

**Morning:**
1. Login to chat
2. Start conversation about project
3. Close browser

**Afternoon:**
1. Open chat again
2. 🎉 Previous conversation automatically restored
3. Continue where you left off

### Use Case 2: Research Session

**Session 1:**
1. Ask multiple questions about a topic
2. Get detailed answers
3. Take a break

**Session 2:**
1. Return to chat
2. 🎉 All previous Q&A restored
3. Ask follow-up questions with full context

### Use Case 3: Multi-Device (Same Browser)

**Device 1 (Desktop):**
1. Have a conversation
2. Note the contextId

**Device 2 (Laptop - Same browser account):**
1. If browser syncs localStorage
2. Open chat
3. 🎉 Same conversation loaded

## 🔧 Configuration

### No Configuration Required!

The feature works out of the box with:
- Default localStorage key: `aiagent_context_id`
- Automatic save on contextId creation
- Automatic load on page mount
- Graceful error handling

## 📊 Monitoring

### Browser DevTools

**Console Logs:**
- All operations logged with `[ChatPage]` prefix
- Successful loads show message count
- Errors show full error details

**Local Storage:**
- View: DevTools → Application → Local Storage
- Key: `aiagent_context_id`
- Value: Current thread's contextId

**Network Tab:**
- Watch for: `GET /api/threads/{contextId}/messages`
- Status 200: Messages found and loaded
- Status 404: No messages in this thread

## 🐛 Troubleshooting

### Issue: Messages Not Auto-Loading

**Possible Causes:**
1. No contextId in localStorage
2. Backend not running
3. MongoDB connection issue
4. Thread doesn't exist in database

**Solution:**
1. Check console for error messages
2. Verify localStorage has `aiagent_context_id`
3. Check backend is running on port 5016
4. Verify MongoDB has data in `threadMessages` collection

### Issue: Old Messages Keep Appearing

**Cause:** ContextId persists in localStorage

**Solution:** Click "⟲ Clear Chat" button to start fresh

### Issue: Different User Sees My Messages

**Cause:** ContextId contains username but isn't user-authenticated

**Solution:** For production, add proper user authentication to the backend

## 🚀 Future Enhancements

1. **Thread List**
   - Show list of all user's threads
   - Click to switch between threads
   - Show thread preview and timestamp

2. **Thread Metadata**
   - Store thread title
   - Store creation date
   - Store last activity time
   - Show thread summary

3. **Smart Context Management**
   - Expire old contextIds after X days
   - Archive inactive threads
   - Auto-cleanup localStorage

4. **Cross-Device Sync**
   - Use backend to store active contextId per user
   - Sync across devices
   - Resume conversation anywhere

5. **Thread Search**
   - Search within thread messages
   - Search across all threads
   - Filter by date, agent, topic

## 📚 Related Documentation

- **Thread History Feature**: `THREAD_HISTORY_FEATURE.md`
- **Quick Start Guide**: `QUICK_START_THREAD_HISTORY.md`
- **Backend README**: `AIAgentsBackend/README.md`
- **Frontend README**: `AIAgentsFrontend/README.md`

## 🎉 Summary

The auto-load feature provides a seamless experience:

- ✅ **Automatic**: No manual action needed
- ✅ **Persistent**: Survives page refreshes
- ✅ **Fast**: Loads in background on page mount
- ✅ **Reliable**: Graceful error handling
- ✅ **Transparent**: Console logs for debugging
- ✅ **User-Friendly**: Clear loading states

**Your conversations are now truly persistent! 🎊**

---

**Happy chatting! 🤖💬**

