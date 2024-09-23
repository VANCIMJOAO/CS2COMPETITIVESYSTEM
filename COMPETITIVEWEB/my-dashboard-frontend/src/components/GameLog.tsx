import React from 'react';
import styles from './GameLog.module.css';

interface GameEvent {
  time: string;
  eventType: string;
  team?: 'T' | 'CT';
  player1: string;
  player2?: string;
  weapon?: string;
  isHeadshot?: boolean;
}

interface GameLogProps {
  events: GameEvent[];
}

// Função para buscar o ícone correto da arma
const getWeaponIcon = (weapon: string) => {
  const iconPath = `/icons/${weapon}.svg`;
  return iconPath;
};

// Função para agrupar os eventos por round
const groupEventsByRound = (events: GameEvent[]) => {
  const rounds: { [key: string]: GameEvent[] } = {};
  events.forEach((event) => {
    if (!rounds[event.time]) {
      rounds[event.time] = [];
    }
    rounds[event.time].push(event);
  });
  return rounds;
};

const GameLog: React.FC<GameLogProps> = ({ events }) => {
  const rounds = groupEventsByRound(events);

  return (
    <div className={styles.gameLogContainer}>
      {Object.entries(rounds).map(([round, roundEvents], index) => (
        <div key={index} className={styles.roundBlock}>
          <h3 className={styles.roundTitle}>{round}</h3>
          {roundEvents.map((event, idx) => (
            <div key={idx} className={`${styles.logEvent} ${event.team ? styles[event.team] : ''}`}>
              <span className={styles.time}>{event.time}</span>
              <span className={styles.description}>
                {event.eventType === 'round start' && <span>{event.player1}</span>}
                {event.eventType === 'kill' && (
                  <>
                    {event.player1}
                    {event.weapon && (
                      <img
                        src={getWeaponIcon(event.weapon)}
                        alt={event.weapon}
                        className={styles.weaponIcon}
                      />
                    )}
                    {event.player2 && <span> matou {event.player2}</span>}
                    {event.isHeadshot && <span className={styles.headshot}> (HS)</span>}
                  </>
                )}
                {event.eventType === 'round win' && <span>{event.player1}</span>}
              </span>
            </div>
          ))}
        </div>
      ))}
    </div>
  );
};

export default GameLog;
