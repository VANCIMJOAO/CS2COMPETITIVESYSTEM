import express from 'express';
import cors from 'cors';
import dotenv from 'dotenv';
import { createServer } from 'http';
import { Server } from 'socket.io';
import fs from 'fs';
import path from 'path';
import apiRoutes from './routes/api';

dotenv.config();

const app = express();
const port = process.env.PORT || 3001;

app.use(cors());
app.use(express.json());

const server = createServer(app);
const io = new Server(server, {
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});

io.on('connection', (socket) => {
  console.log('Novo cliente conectado!');
  socket.on('disconnect', () => {
    console.log('Cliente desconectado!');
  });
});

const matchHistoryDir = process.env.MATCH_HISTORY_DIR || 'C:\\cs2-ds\\game\\bin\\win64\\data\\match_history';

fs.watch(matchHistoryDir, (eventType, filename) => {
  if (filename && (eventType === 'change' || eventType === 'rename')) {  // Verifica se filename não é null
    console.log(`Arquivo ${filename} foi alterado, emitindo atualização...`);

    const filePath = path.join(matchHistoryDir, filename);
    if (fs.existsSync(filePath)) {
      const content = fs.readFileSync(filePath, 'utf8');
      const matchData = JSON.parse(content);
      
      io.emit('match-update', matchData);
    }
  }
});

app.use('/api', apiRoutes);

server.listen(port, () => {
  console.log(`Server running on port ${port}`);
});
