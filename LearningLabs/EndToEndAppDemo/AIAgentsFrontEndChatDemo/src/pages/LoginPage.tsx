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
      setError('Veuillez entrer votre nom');
      return;
    }

    if (trimmedUsername.length < 2) {
      setError('Le nom doit contenir au moins 2 caractères');
      return;
    }

    saveSession(trimmedUsername);
    navigate('/chat');
  };

  return (
    <div className="login-container">
      <div className="login-card">
        <div className="login-header">
          <h1>MBS Store</h1>
          <p>Assistant de rédaction de messages</p>
        </div>
        
        <form onSubmit={handleSubmit} className="login-form">
          <div className="form-group">
            <label htmlFor="username">Entrez votre nom</label>
            <input
              type="text"
              id="username"
              value={username}
              onChange={(e) => {
                setUsername(e.target.value);
                setError('');
              }}
              placeholder="Votre nom"
              autoFocus
              autoComplete="off"
            />
            {error && <span className="error-message">{error}</span>}
          </div>
          
          <button type="submit" className="login-button">
            Commencer
          </button>
        </form>

        <div className="login-footer">
          <p>La session expire après 15 minutes d'inactivité</p>
        </div>
      </div>
    </div>
  );
}
