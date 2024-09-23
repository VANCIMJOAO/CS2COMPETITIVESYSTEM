import React from 'react';
import styles from './PlayerStats.module.css'; // Importa o CSS específico para as estatísticas

interface PlayerStatsProps {
  kills: number;
  deaths: number;
  assists: number;
  headshots: number;
  kdRatio: number | string;
}

const PlayerStats: React.FC<PlayerStatsProps> = ({ kills, deaths, assists, headshots, kdRatio }) => {
  return (
    <div className={styles.statsContainer}>
      <div className={styles.statItem}>
        <h3>Kills</h3>
        <p>{kills}</p>
      </div>
      <div className={styles.statItem}>
        <h3>Deaths</h3>
        <p>{deaths}</p>
      </div>
      <div className={styles.statItem}>
        <h3>Assists</h3>
        <p>{assists}</p>
      </div>
      <div className={styles.statItem}>
        <h3>Headshots</h3>
        <p>{headshots}</p>
      </div>
      <div className={styles.statItem}>
        <h3>K/D Ratio</h3>
        {/* Verifique se deaths é 0, se for mostre kills ou um texto personalizado */}
        <p>{deaths === 0 ? kills : kdRatio}</p>
      </div>
    </div>
  );
};

export default PlayerStats;
