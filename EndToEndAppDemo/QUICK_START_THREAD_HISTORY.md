# Quick Start: Thread History Feature

## 🚀 What's New?

You can now **load previous conversation history** from the database using the contextId!

## 📋 Prerequisites

1. Backend running (`cd AIAgentsBackend && dotnet run`)
2. Frontend running (`cd AIAgentsFrontend && npm run dev`)
3. MongoDB running with existing conversation data

## 🎯 How to Use

### Step 1: Start a Conversation
1. Open the frontend (http://localhost:5173)
2. Login with your username
3. Send a message to the AI agent
4. Notice the **Context ID** displayed in the header (e.g., `ID: mbensaid-mkl9...`)

### Step 2: Load Thread History
1. Look for the **"📜 Load History"** button in the agent header
2. Click the button
3. Wait for the loading indicator (⏳ Loading...)
4. Your previous messages will be loaded from MongoDB!

### Step 3: Continue the Conversation
- All previous messages are now displayed
- You can continue the conversation from where you left off
- The contextId remains the same, so new messages are added to the same thread

## 🔍 Where to Find the Button

```
┌─────────────────────────────────────────────────────┐
│  History Agent                                      │
│  REST API • NON-STREAMING                           │
│  ID: mbensaid-mkl9...  [📜 Load History]           │
└─────────────────────────────────────────────────────┘
```

The button appears:
- ✅ Next to the Context ID badge
- ✅ Only when a contextId exists (after sending at least one message)
- ✅ In the agent header area

## 🧪 Testing the Feature

### Test Scenario 1: Load Existing Thread

```bash
# 1. Send some messages in the UI
User: "What is AI?"
Agent: "AI stands for Artificial Intelligence..."

# 2. Note the contextId (e.g., mbensaid-mkl99c65-vtevfu)

# 3. Click "Load History" button

# 4. Verify messages are loaded from database
```

### Test Scenario 2: API Testing

```bash
# Test the API endpoint directly
curl -X GET http://localhost:5016/api/threads/mbensaid-mkl99c65-vtevfu/messages

# Expected response:
{
  "threadId": "mbensaid-mkl99c65-vtevfu",
  "messageCount": 2,
  "messages": [
    {
      "key": "msg-1737123456789",
      "timestamp": 1737123456789,
      "messageText": "What is AI?",
      "serializedMessage": "{\"role\":\"user\",\"content\":\"What is AI?\"}"
    },
    {
      "key": "msg-1737123456790",
      "timestamp": 1737123456790,
      "messageText": "AI stands for Artificial Intelligence...",
      "serializedMessage": "{\"role\":\"assistant\",\"content\":\"AI stands for Artificial Intelligence...\"}"
    }
  ]
}
```

### Test Scenario 3: MongoDB Verification

```javascript
// Connect to MongoDB
mongosh mongodb://admin:password123@localhost:27017

// Switch to database
use aiagents_db

// Find thread messages
db.threadMessages.find({ ThreadId: "mbensaid-mkl99c65-vtevfu" }).pretty()

// Count messages
db.threadMessages.countDocuments({ ThreadId: "mbensaid-mkl99c65-vtevfu" })
```

## 📊 What Gets Loaded?

The feature loads:
- ✅ All messages in the thread (user and agent)
- ✅ Message timestamps
- ✅ Message content
- ✅ Message roles (user/assistant)

The messages are displayed in **chronological order** (oldest first).

## 🎨 UI Features

### Button States

1. **Default State**: `📜 Load History`
   - Purple gradient background
   - Hover effect with shadow
   - Clickable

2. **Loading State**: `⏳ Loading...`
   - Disabled (not clickable)
   - Reduced opacity
   - Shows loading emoji

3. **Hidden State**:
   - Button is hidden when no contextId exists
   - Start a conversation to see the button

### Debug Information

When you click "Load History":
1. Open the **Debug Panel** (click "Show Debug" in the sidebar)
2. You'll see the API request details:
   - Method: GET
   - URL: `/api/threads/{threadId}/messages`
   - Response status: 200 OK
   - Duration: Time taken to fetch
   - Response body: All loaded messages

## ⚠️ Error Handling

### Error: "No active thread"
**Cause**: No contextId exists yet  
**Solution**: Send at least one message to create a thread

### Error: "Thread not found"
**Cause**: The contextId doesn't exist in the database  
**Solution**: Verify the contextId is correct and messages were saved

### Error: "Failed to load thread history"
**Cause**: Backend or database connection issue  
**Solution**: 
1. Check backend is running
2. Check MongoDB is running
3. Check browser console for details

## 🔧 Configuration

### Backend Configuration

File: `AIAgentsBackend/appsettings.json`

```json
{
  "MongoDB": {
    "ThreadMessagesCollectionName": "threadMessages"
  }
}
```

### Frontend Configuration

No configuration needed! The feature uses the existing `contextId` from the chat session.

## 📝 Console Logs

### Frontend Console
```
[Threads] Fetching messages for threadId: mbensaid-mkl99c65-vtevfu
[Threads] Retrieved 4 messages for thread mbensaid-mkl99c65-vtevfu
[ChatPage] Loaded 4 messages from thread mbensaid-mkl99c65-vtevfu
```

### Backend Console
```
[ThreadRepository] Getting messages for thread: mbensaid-mkl99c65-vtevfu
[ThreadRepository] Found 4 messages for thread mbensaid-mkl99c65-vtevfu
```

## 🎓 Use Cases

### Use Case 1: Resume Conversation After Page Refresh
1. Have a conversation with the AI
2. Refresh the page (conversation is lost from UI)
3. Start a new conversation (generates new contextId)
4. Want to see old conversation? Use MongoDB to find the old contextId
5. Manually set the contextId or use the Load History feature

### Use Case 2: Review Past Conversations
1. Open the chat
2. Note the contextId from a previous session
3. Click "Load History" to review what was discussed

### Use Case 3: Debug Message Storage
1. Send messages to the AI
2. Click "Load History"
3. Verify messages are correctly stored in MongoDB
4. Check the Debug Panel for API details

## 🚦 Quick Checklist

Before using the feature, ensure:

- [ ] Backend is running on port 5016
- [ ] Frontend is running on port 5173
- [ ] MongoDB is running on port 27017
- [ ] You're logged in to the frontend
- [ ] You've sent at least one message (to create a contextId)
- [ ] The contextId is visible in the agent header

## 🎉 Success Indicators

You'll know it's working when:
1. ✅ Button appears after sending first message
2. ✅ Button shows loading state when clicked
3. ✅ Messages appear in the chat area
4. ✅ Console shows success logs
5. ✅ No error alerts appear

## 📚 Related Documentation

- **Full Feature Documentation**: `THREAD_HISTORY_FEATURE.md`
- **Backend README**: `AIAgentsBackend/README.md`
- **Frontend README**: `AIAgentsFrontend/README.md`

## 💡 Tips

1. **Keep the Debug Panel open** to see API requests and responses
2. **Check browser console** for detailed logs
3. **Use MongoDB Compass** to visually inspect thread messages
4. **Test with different contextIds** to see different conversations

## 🆘 Need Help?

If something isn't working:
1. Check the console logs (browser and backend)
2. Verify MongoDB has data in the `threadMessages` collection
3. Test the API endpoint directly with curl
4. Check the Debug Panel in the UI
5. Review the error message for specific guidance

---

**Happy chatting! 🤖💬**

