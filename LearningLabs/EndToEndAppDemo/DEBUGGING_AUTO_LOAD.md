# Debugging Auto-Load Issue

## 🔍 Step-by-Step Debugging Guide

### Step 1: Hard Refresh the Frontend

**IMPORTANT:** You need to hard refresh to get the latest code!

```bash
# In your browser:
Windows/Linux: Ctrl + Shift + R
Mac: Cmd + Shift + R

# OR clear cache completely:
1. Open DevTools (F12)
2. Right-click refresh button
3. Select "Empty Cache and Hard Reload"
```

### Step 2: Check Browser Console

After hard refresh, **open the browser console** (F12 → Console tab) and look for these logs:

#### ✅ Expected Logs (Working):
```javascript
[ChatPage] Mount effect triggered. Username: mbensaid
[ChatPage] Saved contextId from localStorage: mbensaid-mkl99c65-vtevfu
[ChatPage] ✅ Found saved contextId: mbensaid-mkl99c65-vtevfu
[ChatPage] 🚀 Starting auto-load for contextId: mbensaid-mkl99c65-vtevfu
[ChatPage] 📡 Fetching messages from backend...
[Threads] Fetching messages for threadId: mbensaid-mkl99c65-vtevfu
[Threads] Retrieved 4 messages for thread mbensaid-mkl99c65-vtevfu
[ChatPage] 📥 Response received: {threadId: "...", messageCount: 4, messages: Array(4)}
[ChatPage] Auto-loaded 4 messages from thread mbensaid-mkl99c65-vtevfu
```

#### ❌ Problem Logs (Not Working):

**Case 1: No contextId in localStorage**
```javascript
[ChatPage] Mount effect triggered. Username: mbensaid
[ChatPage] Saved contextId from localStorage: null
[ChatPage] No saved contextId found
```
**Solution:** You need to send a message first to create a contextId!

**Case 2: Username not available**
```javascript
[ChatPage] Mount effect triggered. Username: null
[ChatPage] No username, skipping auto-load
```
**Solution:** Make sure you're logged in!

**Case 3: Backend error**
```javascript
[ChatPage] Mount effect triggered. Username: mbensaid
[ChatPage] Saved contextId from localStorage: mbensaid-mkl99c65-vtevfu
[ChatPage] ✅ Found saved contextId: mbensaid-mkl99c65-vtevfu
[ChatPage] 🚀 Starting auto-load for contextId: mbensaid-mkl99c65-vtevfu
[ChatPage] 📡 Fetching messages from backend...
[Threads] Error fetching thread messages: Failed to fetch
```
**Solution:** Check if backend is running on port 5016!

### Step 3: Check localStorage

**In Browser DevTools:**
1. Open DevTools (F12)
2. Go to **Application** tab
3. Expand **Local Storage**
4. Click on `http://localhost:5173`
5. Look for key: `aiagent_context_id`

#### ✅ Should See:
```
Key: aiagent_context_id
Value: mbensaid-mkl99c65-vtevfu (or similar)
```

#### ❌ If Empty:
You need to send a message first to create a contextId!

### Step 4: Test the Complete Flow

#### Test 1: Create ContextId
1. Login to the app
2. Send a message: "Hello"
3. **Check console** - Should see:
   ```
   [ChatPage] Created and saved new contextId: mbensaid-mkl9...
   ```
4. **Check localStorage** - Should see `aiagent_context_id` with value

#### Test 2: Refresh and Auto-Load
1. Press F5 (or Ctrl+R)
2. **Check console immediately** - Should see:
   ```
   [ChatPage] Mount effect triggered. Username: mbensaid
   [ChatPage] ✅ Found saved contextId: ...
   [ChatPage] 🚀 Starting auto-load...
   ```
3. **Check UI** - Should see messages appear

### Step 5: Check Backend is Running

```bash
# Check if backend is running
curl http://localhost:5016/api/threads/test-id/messages

# Expected: 404 Not Found (means backend is responding)
# Bad: Connection refused (means backend is not running)
```

### Step 6: Check MongoDB

```bash
# Connect to MongoDB
mongosh mongodb://admin:password123@localhost:27017

# Switch to database
use aiagents_db

# Check if messages exist
db.threadMessages.find().limit(5).pretty()

# Should see messages with ThreadId field
```

## 🐛 Common Issues

### Issue 1: "Mount effect not triggering"

**Symptoms:**
- No console logs at all
- Page loads but nothing happens

**Causes:**
- Old JavaScript cached in browser
- Code didn't update

**Solution:**
```bash
# 1. Hard refresh (Ctrl+Shift+R)
# 2. Clear browser cache completely
# 3. Close and reopen browser
# 4. Check if Vite dev server is running
```

### Issue 2: "localStorage is null"

**Symptoms:**
```javascript
[ChatPage] Saved contextId from localStorage: null
```

**Causes:**
- Never sent a message before
- Clicked "Clear Chat" button
- Logged out (clears localStorage)

**Solution:**
1. Send a message to create contextId
2. Verify in localStorage (Application tab)
3. Then refresh

### Issue 3: "Backend not responding"

**Symptoms:**
```javascript
[Threads] Error fetching thread messages: Failed to fetch
```

**Causes:**
- Backend not running
- Wrong port
- CORS issue

**Solution:**
```bash
# Start backend
cd AIAgentsBackend
dotnet run

# Should see:
# Now listening on: http://localhost:5016
```

### Issue 4: "404 Thread not found"

**Symptoms:**
```javascript
[Threads] Error: Thread not found: mbensaid-mkl99c65-vtevfu
```

**Causes:**
- ContextId in localStorage doesn't match any thread in MongoDB
- Messages were never saved to database

**Solution:**
1. Clear localStorage (click "Clear Chat")
2. Send new messages
3. Verify messages are in MongoDB
4. Then refresh

## 🧪 Manual Testing Script

**Run this step-by-step:**

```bash
# 1. Start Backend
cd AIAgentsBackend && dotnet run
# Wait for: "Now listening on: http://localhost:5016"

# 2. Start Frontend (in new terminal)
cd AIAgentsFrontend && npm run dev
# Wait for: "Local: http://localhost:5173"

# 3. Open Browser
# Go to: http://localhost:5173

# 4. Open DevTools
# Press F12

# 5. Go to Console Tab
# Keep console open for all steps

# 6. Login
# Enter username: "testuser"

# 7. Check Console
# Should see: [ChatPage] Mount effect triggered...

# 8. Send Message
# Type: "Hello, this is a test"
# Press Send

# 9. Check Console
# Should see: [ChatPage] Created and saved new contextId: testuser-...

# 10. Check localStorage
# DevTools → Application → Local Storage
# Should see: aiagent_context_id = testuser-...

# 11. HARD REFRESH PAGE
# Press: Ctrl + Shift + R

# 12. Check Console Immediately
# Should see:
#   [ChatPage] Mount effect triggered. Username: testuser
#   [ChatPage] ✅ Found saved contextId: testuser-...
#   [ChatPage] 🚀 Starting auto-load...
#   [ChatPage] 📡 Fetching messages from backend...
#   [Threads] Retrieved X messages...
#   [ChatPage] Auto-loaded X messages...

# 13. Check UI
# Should see your "Hello, this is a test" message

# ✅ SUCCESS! Auto-load is working!
```

## 📝 Checklist

Before reporting an issue, verify:

- [ ] Backend is running (check port 5016)
- [ ] Frontend is running (check port 5173)
- [ ] MongoDB is running (check port 27017)
- [ ] Logged in with a username
- [ ] Sent at least one message
- [ ] ContextId exists in localStorage
- [ ] Hard refreshed the browser (Ctrl+Shift+R)
- [ ] Console logs show "Mount effect triggered"
- [ ] Console logs show "Found saved contextId"
- [ ] No errors in console

## 🎯 What to Share

If still not working, share:

1. **Console logs** (copy all logs starting with `[ChatPage]`)
2. **localStorage screenshot** (Application tab)
3. **Network tab** (any failed requests?)
4. **Browser** (Chrome, Firefox, etc.)
5. **Backend terminal** (any errors?)

## 💡 Quick Fixes

### Quick Fix 1: Clear Everything and Start Fresh
```bash
# 1. In browser DevTools
Application → Local Storage → Clear All

# 2. Refresh page (F5)

# 3. Send a message

# 4. Hard refresh (Ctrl+Shift+R)

# Should work now!
```

### Quick Fix 2: Restart Everything
```bash
# 1. Stop backend (Ctrl+C)
# 2. Stop frontend (Ctrl+C)
# 3. Close browser
# 4. Start backend: cd AIAgentsBackend && dotnet run
# 5. Start frontend: cd AIAgentsFrontend && npm run dev
# 6. Open browser fresh
# 7. Login and test
```

### Quick Fix 3: Hard Refresh Multiple Times
```bash
# Sometimes browser caching is stubborn
# 1. Press Ctrl+Shift+R (hard refresh)
# 2. Wait 2 seconds
# 3. Press Ctrl+Shift+R again
# 4. Check console logs
```

---

**If you follow all these steps and it's still not working, share the console logs and we'll debug further!** 🔍

