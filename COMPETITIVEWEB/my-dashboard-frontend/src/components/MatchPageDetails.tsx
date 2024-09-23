import React, { useEffect, useState, useCallback, useMemo } from 'react';
import { useParams } from 'react-router-dom';
import { API_BASE_URL } from './config';
import styles from './MatchPageDetails.module.css';
import GameLog from './GameLog';
import MatchStats from './MatchStats';

interface PlayerStats {
  SteamID: string;
  PlayerName: string;
  Kills: number;
  Deaths: number;
  Headshots: number;
}

interface RoundDetails {
  RoundNumber: number;
  IsRoundStart: boolean;
  WinnerTeamId: number;
  Team1Kills: number;
  Team2Kills: number;
  WasOvertime: boolean;
  ScoreTeam1: number;
  ScoreTeam2: number;
  PlayerKills: { [steamID: string]: string[] };
  PlayerNicknames: { [steamID: string]: string };
}

interface MatchDetails {
  matchId: string;
  Date: string;
  Team1: string;
  Team2: string;
  Team1Id: number;
  Team2Id: number;
  Status: string;
  Team1Score: number;
  Team2Score: number;
  Map: string;
  PlayerSteamIDsTeam1: string[];
  PlayerSteamIDsTeam2: string[];
  RoundDetails: RoundDetails[];
  PlayerStatsPerMatch: { [steamID: string]: PlayerStats };
  PlayerNicknames: { [steamID: string]: string };
}

interface GameEvent {
  time: string;
  eventType: string;
  team?: 'T' | 'CT';
  player1: string;
  player2?: string;
  weapon?: string;
}

const MatchPageDetails: React.FC = () => {
  const { matchId } = useParams<{ matchId: string }>();
  const [matchDetails, setMatchDetails] = useState<MatchDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState<string | null>(null);

  useEffect(() => {
    const fetchMatchDetails = async () => {
      try {
        setLoading(true);
        const response = await fetch(`${API_BASE_URL}/api/matches/${matchId}`);
        if (!response.ok) {
          throw new Error('Erro ao carregar os detalhes da partida');
        }
        const data = await response.json();

        // Normalizando SteamIDs para garantir consistência
        data.PlayerSteamIDsTeam1 = data.PlayerSteamIDsTeam1?.map((id: number | string) => id.toString().trim()) || [];
        data.PlayerSteamIDsTeam2 = data.PlayerSteamIDsTeam2?.map((id: number | string) => id.toString().trim()) || [];

        data.PlayerStatsPerMatch = Object.keys(data.PlayerStatsPerMatch).reduce((acc: any, steamID) => {
          acc[steamID.toString().trim()] = data.PlayerStatsPerMatch[steamID];
          return acc;
        }, {});

        data.PlayerNicknames = Object.keys(data.PlayerNicknames).reduce((acc: any, steamID) => {
          acc[steamID.toString().trim()] = data.PlayerNicknames[steamID];
          return acc;
        }, {});

        setMatchDetails(data);
      } catch (err: any) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    };

    fetchMatchDetails();
  }, [matchId]);

  const getPlayersForTeam = (teamSteamIDs: string[]): PlayerStats[] => {
    if (!matchDetails) return [];

    const { PlayerStatsPerMatch } = matchDetails;

    const teamPlayers = Object.values(PlayerStatsPerMatch).filter((player) => {
      const steamId = player.SteamID.toString().trim();
      return teamSteamIDs.includes(steamId);
    });

    return teamPlayers;
  };

  const generateGameLogEvents = useCallback(() => {
    const events: GameEvent[] = [];

    matchDetails?.RoundDetails.forEach((round) => {
      if (!events.some(event => event.time === `Round ${round.RoundNumber}` && event.eventType === 'round start')) {
        events.push({
          time: `Round ${round.RoundNumber}`,
          eventType: 'round start',
          team: undefined,
          player1: 'Round Iniciado',
        });
      }

      Object.entries(round.PlayerKills).forEach(([steamID, kills]) => {
        const player1 = matchDetails.PlayerNicknames[steamID.toString().trim()];
        (kills as string[]).forEach((killDetail) => {
          const [killAction, weaponInfo] = killDetail.split(' com ');
          const [killer, victim] = killAction.split(' matou ');
          events.push({
            time: `Round ${round.RoundNumber}`,
            eventType: 'kill',
            team: undefined,
            player1: killer || player1,
            player2: victim || '',
            weapon: weaponInfo || '',
          });
        });
      });

      if (!events.some(event => event.time === `Round ${round.RoundNumber}` && event.eventType === 'round win')) {
        if (round.WinnerTeamId) {
          events.push({
            time: `Round ${round.RoundNumber}`,
            eventType: 'round win',
            team: round.WinnerTeamId === 1 ? 'T' : 'CT',
            player1: `${round.WinnerTeamId === 1 ? 'T' : 'CT'} venceu o round`,
          });
        }
      }
    });

    return events;
  }, [matchDetails]);

  const gameLogEvents = useMemo(() => generateGameLogEvents(), [generateGameLogEvents]);

  const filteredGameLogEvents = useMemo(() => {
    if (!filter) return gameLogEvents;
    return gameLogEvents.filter((event) => event.eventType === filter);
  }, [gameLogEvents, filter]);

  if (loading) return <div className={styles.loader}>Carregando...</div>;
  if (error) return <div className={styles.error}>{error}</div>;
  if (!matchDetails) return <div className={styles.error}>Partida não encontrada</div>;

  // Separando os jogadores por time
  const team1Players = getPlayersForTeam(matchDetails.PlayerSteamIDsTeam1 || []);
  const team2Players = getPlayersForTeam(matchDetails.PlayerSteamIDsTeam2 || []);

  return (
    <div className={styles.matchDetailsContainer}>
    <div className={styles.matchHeader}>
      <div className={styles.matchTeams}>
        <div className={styles.teamName}>{matchDetails.Team1}</div>
        <div className={styles.vs}>vs</div>
        <div className={styles.teamName}>{matchDetails.Team2}</div>
      </div>
      <div className={styles.matchInfo}>
        <p><strong>Status:</strong> {matchDetails.Status}</p>
        <p><strong>Data:</strong> {new Date(matchDetails.Date).toLocaleString()}</p>
        <p><strong>Mapa:</strong> {matchDetails.Map}</p>
      </div>
    </div>

      <div className={styles.twitchEmbed}>
        <iframe
          title="Twitch Stream"
          src={`https://player.twitch.tv/?channel=arenarapidfire&parent=${window.location.hostname}`}
          height="400"
          width="100%"
          allowFullScreen={true}
          frameBorder="0"
        ></iframe>
      </div>

      <div className={styles.statsContainer}>
        <MatchStats
          teamName={matchDetails.Team1}
          playerStats={team1Players}
        />
        <MatchStats
          teamName={matchDetails.Team2}
          playerStats={team2Players}
        />
      </div>

      <h2 className={styles.sectionTitle}>Game Log</h2>
      <div className={styles.filters}>
        <button
          className={!filter ? styles.activeFilter : ''}
          onClick={() => setFilter(null)}
        >
          Todos
        </button>
        <button
          className={filter === 'kill' ? styles.activeFilter : ''}
          onClick={() => setFilter('kill')}
        >
          Kills
        </button>
        <button
          className={filter === 'round win' ? styles.activeFilter : ''}
          onClick={() => setFilter('round win')}
        >
          Rounds Vencidos
        </button>
      </div>
      <GameLog events={filteredGameLogEvents} />
    </div>
  );
};

export default MatchPageDetails;

