import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import api from '../services/api';

const Login: React.FC = () => {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const navigate = useNavigate();

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      const response = await api.post('/login', { username, password });
      if (response.data.token) {
        localStorage.setItem('token', response.data.token); // Armazenar o token JWT
        navigate('/dashboard'); // Redirecionar para o dashboard
      } else {
        alert('Invalid credentials');
      }
    } catch (error) {
      alert('Error logging in, please try again.');
    }
  };

  return (
    <div className="login-container">
      <h1>Login</h1>
      <form onSubmit={handleLogin}>
        <input
          type="text"
          placeholder="Username"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          autoComplete="username" // Corrigido para autoComplete
        />
        <input
          type="password"
          placeholder="Password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          autoComplete="current-password" // Corrigido para autoComplete
        />
        <button type="submit">Login</button>
      </form>
    </div>
  );
};

export default Login;
