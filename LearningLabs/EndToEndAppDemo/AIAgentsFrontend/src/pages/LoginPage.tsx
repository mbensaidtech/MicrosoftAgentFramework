import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { saveSession } from '../utils/session';
import './LoginPage.css';

export function LoginPage() {
  const [username, setUsername] = useState('');
  const [error, setError] = useState('');
  const navigate = useNavigate();

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    
    const trimmedUsername = username.trim();
    if (!trimmedUsername) {
      setError('Please enter your name');
      return;
    }

    if (trimmedUsername.length < 2) {
      setError('Name must be at least 2 characters');
      return;
    }

    saveSession(trimmedUsername);
    navigate('/chat');
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <h1>AI Agents Chat</h1>
          <p>Connect with intelligent agents</p>
        </div>
        
        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-group">
            <label htmlFor="username">Enter your name</label>
            <input
              type="text"
              id="username"
              value={username}
              onChange={(e) => {
                setUsername(e.target.value);
                setError('');
              }}
              placeholder="Your name"
              autoFocus
              autoComplete="off"
            />
            {error && <span className="error-message">{error}</span>}
          </div>
          
          <button type="submit" className="login-button">
            Start Chatting
          </button>
        </form>

        <div className="login-footer">
          <p>Session will expire after 15 minutes of inactivity</p>
        </div>
      </div>
    </div>
  );
}
