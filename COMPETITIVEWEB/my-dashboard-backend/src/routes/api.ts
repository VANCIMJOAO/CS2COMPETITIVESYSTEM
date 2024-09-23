import express, { Request, Response } from 'express';
import jwt from 'jsonwebtoken';
import multer from 'multer';
import path from 'path';
import fs from 'fs';
import { promises as fsPromises } from 'fs'; // For asynchronous operations
import { v4 as uuidv4 } from 'uuid';

const router = express.Router();

// Middleware to allow JSON in request body
router.use(express.json());
router.use(
  '/uploads',
  express.static(path.resolve('C:\\cs2-ds\\game\\bin\\win64\\data\\uploads'))
);

// Configuration for file storage during uploads
const storage = multer.diskStorage({
  destination: (req, file, cb) => {
    const uploadPath =
      process.env.UPLOAD_DIR ||
      'C:\\cs2-ds\\game\\bin\\win64\\data\\uploads'; // Path to save files
    cb(null, uploadPath);
  },
  filename: (req, file, cb) => {
    const filename = `${Date.now()}-${file.originalname}`;
    cb(null, filename); // Names the file with a timestamp
  },
});

const upload = multer({ storage }); // Upload middleware

// Function to read JSON asynchronously
const readJSON = async (filePath: string): Promise<any> => {
  try {
    const data = await fsPromises.readFile(filePath, 'utf8');
    return JSON.parse(data);
  } catch (error) {
    console.error('Error reading JSON:', error);
    return [];
  }
};

// Function to write JSON asynchronously
const writeJSON = async (filePath: string, data: any[]): Promise<void> => {
  try {
    await fsPromises.writeFile(
      filePath,
      JSON.stringify(data, null, 2),
      'utf8'
    );
  } catch (error) {
    console.error('Error writing JSON:', error);
  }
};

// Middleware to verify JWT token
const verifyToken = (req: Request, res: Response, next: Function) => {
  const token = req.headers['authorization'];
  if (!token) return res.status(403).json({ message: 'Token is required' });

  jwt.verify(
    token.split(' ')[1],
    process.env.JWT_SECRET || 'your-secret',
    (err, decoded) => {
      if (err) return res.status(401).json({ message: 'Invalid token' });

      (req as any).user = decoded; // Temporary typing workaround
      next();
    }
  );
};

// Login route
router.post('/login', (req: Request, res: Response) => {
  const { username, password } = req.body;

  const users = [
    { id: 1, username: 'admin', password: 'admin' },
    { id: 2, username: 'user2', password: 'password2' },
  ];

  const user = users.find(
    (u) => u.username === username && u.password === password
  );

  if (user) {
    const token = jwt.sign(
      { id: user.id, username: user.username },
      process.env.JWT_SECRET || 'your-secret',
      { expiresIn: '1h' }
    );
    res.json({ token });
  } else {
    res.status(401).json({ message: 'Invalid credentials' });
  }
});

// Server status route
router.get('/status', (req: Request, res: Response) => {
  res.json({ status: 'Server is running' });
});

// Route for uploading photos
router.post(
  '/upload',
  upload.single('photo'),
  (req: Request, res: Response) => {
    if (!req.file) {
      return res.status(400).send('No file was uploaded.');
    }
    res.json({ filePath: `/uploads/${req.file.filename}` });
  }
);

// Function to generate matchId and ensure all matches have a unique ID
const generateMatchId = (match: any) => {
  if (!match.matchId) {
    match.matchId = uuidv4();
  }
  return match;
};

// Route to get live and finished matches
router.get('/matches', async (req: Request, res: Response) => {
  const matchHistoryDir =
    process.env.MATCH_HISTORY_DIR ||
    'C:\\cs2-ds\\game\\bin\\win64\\data\\match_history';

  try {
    const files = await fsPromises.readdir(matchHistoryDir);
    const matches = await Promise.all(
      files
        .filter((file) => file.endsWith('.json'))
        .map(async (file) => {
          const content = await fsPromises.readFile(
            path.join(matchHistoryDir, file),
            'utf8'
          );
          const match = JSON.parse(content);
          return generateMatchId(match); // Generate a matchId if it doesn't exist
        })
    );

    const liveMatches = matches.filter(
      (match: any) => match.Status === 'Ao Vivo'
    );
    const finishedMatches = matches.filter(
      (match: any) => match.Status === 'Finalizada'
    );

    res.json({ liveMatches, finishedMatches });
  } catch (error) {
    console.error('Error loading matches:', error);
    res.status(500).json({ error: 'Error loading matches' });
  }
});

// Route to get details of a specific match by `matchId`
router.get('/matches/:matchId', async (req: Request, res: Response) => {
  const matchHistoryDir =
    process.env.MATCH_HISTORY_DIR ||
    'C:\\cs2-ds\\game\\bin\\win64\\data\\match_history';
  const { matchId } = req.params;

  try {
    const files = await fsPromises.readdir(matchHistoryDir);
    const matchFiles = await Promise.all(
      files
        .filter((file) => file.endsWith('.json'))
        .map(async (file) => {
          const content = await fsPromises.readFile(
            path.join(matchHistoryDir, file),
            'utf8'
          );
          return JSON.parse(content);
        })
    );

    // Check each match by matchId
    const match = matchFiles.find((m) => m.matchId === matchId);

    if (match) {
      return res.json(match);
    }

    res.status(404).json({ message: 'Match not found' });
  } catch (error) {
    console.error('Error loading match:', error);
    res.status(500).json({ error: 'Error loading match' });
  }
});

// Route to save teams in a JSON file
router.post('/teams', verifyToken, async (req: Request, res: Response) => {
  const teamsFilePath =
    process.env.TEAMS_FILE || 'C:\\cs2-ds\\game\\bin\\win64\\data\\teams.json';
  const newTeam = req.body;

  const teams = await readJSON(teamsFilePath);
  teams.push(newTeam);

  await writeJSON(teamsFilePath, teams);

  res.status(200).json({ message: 'Team saved successfully!' });
});

// Route to get teams from the JSON file
router.get('/teams', async (req: Request, res: Response) => {
  const teamsFilePath =
    process.env.TEAMS_FILE || 'C:\\cs2-ds\\game\\bin\\win64\\data\\teams.json';
  const teams = await readJSON(teamsFilePath);
  res.json(teams);
});

// Route to get player statistics, associating them with team photos
router.get('/player-stats', async (req: Request, res: Response) => {
  const playerStatsPath =
    process.env.PLAYER_STATS_FILE ||
    'C:\\cs2-ds\\game\\bin\\win64\\data\\stats\\player_stats.json';
  const teamsFilePath =
    process.env.TEAMS_FILE || 'C:\\cs2-ds\\game\\bin\\win64\\data\\teams.json';

  let playerStats = await readJSON(playerStatsPath);
  const teams = await readJSON(teamsFilePath);

  if (typeof playerStats === 'object' && !Array.isArray(playerStats)) {
    playerStats = Object.values(playerStats);
  }

  playerStats.forEach((player: any) => {
    const matchingTeam = teams.find((team: any) =>
      team.players.some((p: any) => p.steamId === player.SteamID)
    );

    if (matchingTeam) {
      const matchingPlayer = matchingTeam.players.find(
        (p: any) => p.steamId === player.SteamID
      );
      if (matchingPlayer) {
        if (
          matchingPlayer.photo &&
          matchingPlayer.photo.startsWith('/uploads')
        ) {
          player.photo = matchingPlayer.photo;
        } else if (matchingPlayer.photo) {
          player.photo = `/uploads/${matchingPlayer.photo}`;
        } else {
          player.photo = '/uploads/default-player-photo.jpg';
        }
      } else {
        player.photo = '/uploads/default-player-photo.jpg';
      }
    } else {
      player.photo = '/uploads/default-player-photo.jpg';
    }
  });

  res.json(playerStats);
});

//
// Added /tournaments endpoints
//

// Rota para obter detalhes de um jogador específico pelo SteamID
router.get('/player-stats/:steamId', async (req: Request, res: Response) => {
  const { steamId } = req.params;
  const playerStatsPath =
    process.env.PLAYER_STATS_FILE ||
    'C:\\cs2-ds\\game\\bin\\win64\\data\\stats\\player_stats.json';
  
  try {
    let playerStats = await readJSON(playerStatsPath);
    
    // Se playerStats for um objeto, converter para array
    if (typeof playerStats === 'object' && !Array.isArray(playerStats)) {
      playerStats = Object.values(playerStats);
    }
    
    const player = playerStats.find((p: any) => p.SteamID === steamId);
    
    if (player) {
      res.json(player);
    } else {
      res.status(404).json({ message: 'Jogador não encontrado' });
    }
  } catch (error) {
    console.error('Erro ao buscar jogador:', error);
    res.status(500).json({ error: 'Erro interno do servidor' });
  }
});


// Path to the tournaments JSON file
const tournamentsFilePath =
  process.env.TOURNAMENTS_FILE ||
  'C:\\cs2-ds\\game\\bin\\win64\\data\\tournaments.json';

// Function to read tournaments data
const readTournaments = async (): Promise<any[]> => {
  try {
    const data = await fsPromises.readFile(tournamentsFilePath, 'utf8');
    return JSON.parse(data);
  } catch (error) {
    console.error('Error reading tournaments file:', error);
    return [];
  }
};

// Function to write tournaments data
const writeTournaments = async (data: any[]): Promise<void> => {
  try {
    await fsPromises.writeFile(
      tournamentsFilePath,
      JSON.stringify(data, null, 2),
      'utf8'
    );
  } catch (error) {
    console.error('Error writing tournaments file:', error);
  }
};

// Endpoint to get tournaments
router.get('/tournaments', async (req: Request, res: Response) => {
  try {
    const tournaments = await readTournaments();
    res.json(tournaments);
  } catch (error) {
    console.error('Error fetching tournaments:', error);
    res.status(500).json({ error: 'Error fetching tournaments' });
  }
});

// Endpoint to create a new tournament
router.post('/tournaments', verifyToken, async (req: Request, res: Response) => {
  try {
    const newTournament = req.body;
    const tournaments = await readTournaments();
    tournaments.push(newTournament);
    await writeTournaments(tournaments);
    res.status(201).json({ message: 'Tournament created successfully' });
  } catch (error) {
    console.error('Error saving tournament:', error);
    res.status(500).json({ error: 'Error saving tournament' });
  }
});

export default router;
