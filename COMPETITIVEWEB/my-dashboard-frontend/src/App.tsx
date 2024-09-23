import React from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import Login from './components/Login';
import Dashboard from './components/Dashboard';
import PrivateRoute from './components/PrivateRoute';
import IndexPage from './components/IndexPage'; // Nova página index
import PlayerDetails from './components/PlayerDetails';
import MatchPageDetails from './components/MatchPageDetails';

const App: React.FC = () => {
  return (
    <Router>
      <Routes>
        <Route path="/" element={<IndexPage />} /> {/* Página de index */}
        <Route path="/login" element={<Login />} /> {/* Página de login */}
        <Route path="/dashboard" element={<PrivateRoute component={Dashboard} />} /> {/* Dashboard após login */}
        <Route path="/player/:steamId" Component={PlayerDetails} />
        <Route path="/matchdetails/:matchId" element={<MatchPageDetails />} />
      </Routes>
    </Router>
  );
};

export default App;
