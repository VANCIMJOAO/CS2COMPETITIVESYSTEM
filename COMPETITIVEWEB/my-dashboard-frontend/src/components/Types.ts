// types.ts
export interface PlayerStats {
    SteamID: string;
    PlayerName: string;
    Kills: number;
    Deaths: number;
    Headshots: number;
  }
  
  export interface GameEvent {
    time: string;
    eventType: string;
    team?: 'T' | 'CT';
    player1: string;
    player2?: string;
    weapon?: string;
    isHeadshot?: boolean;
  }
  
  // Você pode adicionar outras interfaces compartilhadas aqui
  