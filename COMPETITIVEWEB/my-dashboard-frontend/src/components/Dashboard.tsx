import React, { useEffect, useState } from 'react';
import api from '../services/api';
import { useNavigate } from 'react-router-dom';
import styles from './Dashboard.module.css';  // Importando o CSS Module

const Dashboard: React.FC = () => {
  const [status, setStatus] = useState<string>('');
  const [isModalOpen, setIsModalOpen] = useState<boolean>(true);
  const [isTeamModalOpen, setIsTeamModalOpen] = useState<boolean>(false);
  const [teamName, setTeamName] = useState<string>(''); 
  const [players, setPlayers] = useState<{ steamId: string, photo: File | null }[]>([]);
  const [tournaments, setTournaments] = useState<any[]>([]);
  const [teams, setTeams] = useState<any[]>([]);
  const [activeSection, setActiveSection] = useState<string>('Home');
  const navigate = useNavigate();

  const handleSectionChange = (section: string) => {
    setActiveSection(section);
    localStorage.setItem('activeSection', section);
  };

  useEffect(() => {
    const token = localStorage.getItem('token');
    
    const savedSection = localStorage.getItem('activeSection');
    if (savedSection) {
      setActiveSection(savedSection);
    }

    if (!token) {
      navigate('/login');
    } else {
      api.get('/status', { headers: { Authorization: `Bearer ${token}` } })
        .then(response => setStatus(response.data.status))
        .catch(() => navigate('/login'));

      api.get('/tournaments')
        .then(response => {
          setTournaments(response.data);
          if (response.data.length > 0) {
            setIsModalOpen(false);
          }
        })
        .catch(error => console.error('Erro ao carregar campeonatos:', error));

      api.get('/teams', { headers: { Authorization: `Bearer ${token}` } })
        .then(response => setTeams(response.data))
        .catch(error => console.error('Erro ao carregar times:', error));
    }
  }, [navigate]);

  const handleSaveTeam = async () => {
    const token = localStorage.getItem('token');
    if (!token) {
      alert('Você precisa estar logado para salvar um time.');
      navigate('/login');
      return;
    }

    for (const player of players) {
      if (player.photo) {
        const formData = new FormData();
        formData.append('photo', player.photo);

        try {
          const response = await api.post('/upload', formData, {
            headers: { 
              'Content-Type': 'multipart/form-data',
              'Authorization': `Bearer ${token}`
            }
          });
          player.photo = response.data.filePath;
        } catch (error) {
          console.error('Erro ao fazer upload da foto:', error);
        }
      }
    }

    const newTeam = { teamName, players };

    try {
      await api.post('/teams', newTeam, {
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      setTeams([...teams, newTeam]);
      setIsTeamModalOpen(false);
    } catch (error) {
      console.error('Erro ao salvar time:', error);
    }
  };

  const handleAddPlayer = () => {
    setPlayers([...players, { steamId: '', photo: null }]);
  };

  const renderContent = () => {
    switch (activeSection) {
      case 'Home':
        return <div><h2>Bem-vindo ao Dashboard!</h2></div>;
      case 'Campeonatos':
        return (
          <div>
            <h3>Campeonatos:</h3>
            {tournaments.length > 0 ? (
              <ul>
                {tournaments.map((tournament, index) => (
                  <li key={index}>
                    <h4>{tournament.name}</h4>
                    <p>Data: {tournament.date}</p>
                    <p>Horário de Início: {tournament.startTime}</p>
                    <p>Número de Times: {tournament.teamCount}</p>
                    <p>Número de Servidores: {tournament.serverCount}</p>
                  </li>
                ))}
              </ul>
            ) : (
              <p>Nenhum campeonato criado ainda.</p>
            )}
          </div>
        );
      case 'Times':
        return (
          <div>
            <h3>Times Criados:</h3>
            {teams.length > 0 ? (
              <div className={styles['team-grid']}>
                {teams.map((team, index) => (
                  <div key={index} className={styles['team-card']}>
                    <h4>{team.teamName}</h4>
                    <p>Jogadores:</p>
                    <div className={styles['team-players']}>
                      {team.players && team.players.length > 0 ? (
                        team.players.map((player: { steamId: string, photo: string | null }, playerIndex: number) => (
                          <div key={playerIndex} className={styles['player-info']}>
                            <img src={`http://localhost:3001/api${player.photo}`} alt="Foto do jogador" />
                            <p>SteamID: {player.steamId || "Nenhum SteamID"}</p>
                          </div>
                        ))
                      ) : (
                        <p>Nenhum jogador adicionado ainda.</p>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <p>Nenhum time criado ainda.</p>
            )}
            <button onClick={() => setIsTeamModalOpen(true)}>Adicionar Time</button>
          </div>
        );
      case 'Configurações':
        return <div><h2>Configurações</h2></div>;
      default:
        return null;
    }
  };

  return (
    <div className={styles.dashboard}>
      <aside className={styles.sidebar}>
        <ul>
          <li onClick={() => handleSectionChange('Home')} className={activeSection === 'Home' ? styles.active : ''}>Home</li>
          <li onClick={() => handleSectionChange('Campeonatos')} className={activeSection === 'Campeonatos' ? styles.active : ''}>Campeonatos</li>
          <li onClick={() => handleSectionChange('Times')} className={activeSection === 'Times' ? styles.active : ''}>Times</li>
          <li onClick={() => handleSectionChange('Configurações')} className={activeSection === 'Configurações' ? styles.active : ''}>Configurações</li>
        </ul>
      </aside>
      <div className={styles.content}>
        {renderContent()}

        {isTeamModalOpen && (
          <div className={styles.modal}>
            <h2>Adicionar Time</h2>
            <form>
              <div>
                <label>Nome do Time:</label>
                <input
                  type="text"
                  value={teamName}
                  onChange={(e) => setTeamName(e.target.value)}
                />
              </div>
              <div>
                <h3>Jogadores</h3>
                {players.map((player, index) => (
                  <div key={index}>
                    <label>SteamID:</label>
                    <input
                      type="text"
                      value={player.steamId}
                      onChange={(e) => {
                        const updatedPlayers = [...players];
                        updatedPlayers[index].steamId = e.target.value;
                        setPlayers(updatedPlayers);
                      }}
                    />
                    <label>Foto:</label>
                    <input
                      type="file"
                      onChange={(e) => {
                        const updatedPlayers = [...players];
                        updatedPlayers[index].photo = e.target.files ? e.target.files[0] : null;
                        setPlayers(updatedPlayers);
                      }}
                    />
                  </div>
                ))}
                <button type="button" onClick={handleAddPlayer}>Adicionar Jogador</button>
              </div>
              <button type="button" onClick={handleSaveTeam}>
                Salvar Time
              </button>
              <button type="button" onClick={() => setIsTeamModalOpen(false)}>
                Cancelar
              </button>
            </form>
          </div>
        )}
      </div>
    </div>
  );
};

export default Dashboard;
