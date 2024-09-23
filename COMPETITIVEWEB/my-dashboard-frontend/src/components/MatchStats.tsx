import React from 'react';
import { useNavigate } from 'react-router-dom';  // Importar o hook de navegação
import styles from './MatchStats.module.css';
import { PlayerStats } from './Types';

interface MatchStatsProps {
  teamName: string;
  playerStats: PlayerStats[];
}

const MatchStats: React.FC<MatchStatsProps> = ({ teamName, playerStats }) => {
  const navigate = useNavigate();  // Inicializa o hook useNavigate

  const handlePlayerClick = (steamID: string) => {
    // Navega para a página do jogador usando o SteamID
    navigate(`/player/${steamID}`);
  };

  return (
    <div className={styles.teamStats}>
      <h2 className={styles.teamName}>{teamName}</h2>
      <table className={styles.statsTable}>
        <thead>
          <tr>
            <th>Jogador</th>
            <th>Kills</th>
            <th>Mortes</th>
            <th>HS</th>
            <th>+/-</th>
          </tr>
        </thead>
        <tbody>
          {playerStats.map((player) => {
            const kdDiff = player.Kills - player.Deaths;
            return (
              <tr key={player.SteamID}>
                {/* Evento de clique no nome do jogador */}
                <td
                  className={styles.playerName}
                  onClick={() => handlePlayerClick(player.SteamID)}
                  style={{ cursor: 'pointer', color: 'white' }}  // Estiliza para parecer um link
                >
                  {player.PlayerName}
                </td>
                <td>{player.Kills}</td>
                <td>{player.Deaths}</td>
                <td>{player.Headshots}</td>
                <td>
                  <strong className={kdDiff >= 0 ? styles.positive : styles.negative}>
                    {kdDiff >= 0 ? `+${kdDiff}` : kdDiff}
                  </strong>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
};

export default MatchStats;
