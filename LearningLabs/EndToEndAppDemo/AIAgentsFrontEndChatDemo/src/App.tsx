import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { LoginPage } from './pages/LoginPage';
import { ChatPage } from './pages/ChatPage';
import { ChatPageModal } from './pages/ChatPageModal';
import { ChatPageSideBySide } from './pages/ChatPageSideBySide';
import { isLoggedIn } from './utils/session';

function ProtectedRoute({ children }: { children: React.ReactNode }) {
  if (!isLoggedIn()) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}

function PublicRoute({ children }: { children: React.ReactNode }) {
  if (isLoggedIn()) {
    return <Navigate to="/chat" replace />;
  }
  return <>{children}</>;
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route
          path="/login"
          element={
            <PublicRoute>
              <LoginPage />
            </PublicRoute>
          }
        />
        <Route
          path="/chat"
          element={
            <ProtectedRoute>
              <ChatPage />
            </ProtectedRoute>
          }
        />
        <Route
          path="/chat-modal"
          element={
            <ProtectedRoute>
              <ChatPageModal />
            </ProtectedRoute>
          }
        />
        <Route
          path="/chat-side-by-side"
          element={
            <ProtectedRoute>
              <ChatPageSideBySide />
            </ProtectedRoute>
          }
        />
        <Route path="/" element={<Navigate to="/chat" replace />} />
        <Route path="*" element={<Navigate to="/chat" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
