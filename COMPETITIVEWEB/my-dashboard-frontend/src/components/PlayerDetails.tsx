import React, { useState, useEffect } from 'react';
import { Link, useParams } from 'react-router-dom'; 
import styles from './PlayerDetails.module.css'; 
import PlayerStats from './PlayerStats';  // Importa o componente PlayerStats
import { Helmet } from 'react-helmet';   // Para SEO dinâmico

const PlayerDetails: React.FC = () => {
  const { steamId } = useParams<{ steamId: string }>(); 
  const [playerData, setPlayerData] = useState<any>(null);
  const [scrolled, setScrolled] = useState(false); 
  const [showNotification, setShowNotification] = useState(false);
  const [notificationMessage, setNotificationMessage] = useState('');

  // Função para controlar a rolagem
  useEffect(() => {
    const handleScroll = () => {
      if (window.scrollY > 10) {
        setScrolled(true);
      } else {
        setScrolled(false);
      }
    };

    window.addEventListener('scroll', handleScroll);

    return () => {
      window.removeEventListener('scroll', handleScroll);
    };
  }, []);

  // Buscar os dados do jogador
  useEffect(() => {
    console.log('Buscando dados para SteamID:', steamId);
    fetch(`http://localhost:3001/api/player-stats/${steamId}`)
      .then((response) => {
        if (!response.ok) {
          throw new Error(`Erro ${response.status}: Jogador não encontrado`);
        }
        return response.json();
      })
      .then((data) => {
        console.log('Dados do jogador:', data);
        setPlayerData(data);
      })
      .catch((error) => {
        console.error('Erro ao buscar dados do jogador:', error);
      });
  }, [steamId]);

  const handleFavorite = (steamId: string) => {
    setNotificationMessage(`Jogador com SteamID ${steamId} adicionado aos favoritos!`);
    setShowNotification(true);
    setTimeout(() => setShowNotification(false), 3000); // Fecha a notificação após 3 segundos
  };

  const handleFollow = (steamId: string) => {
    setNotificationMessage(`Você agora segue o jogador com SteamID ${steamId}!`);
    setShowNotification(true);
    setTimeout(() => setShowNotification(false), 3000);
  };

  if (!playerData) {
    return (
      <div className={styles.loaderContainer}>
        <div className={styles.loader}></div>
        <p>Carregando detalhes do jogador...</p>
      </div>
    );
  }

  // Fallback para a imagem padrão, se o campo 'photo' estiver vazio
  const playerPhoto = playerData.photo
    ? `http://localhost:3001/api/uploads/${playerData.photo}`  // Inclua o caminho da imagem do jogador
    : '/imgs/default-player-photo.jpg';           // Imagem padrão

  return (
    <div className={styles['player-details-container']}>
      <Helmet>
        <title>{`Detalhes de ${playerData.PlayerName} | MyGameSite`}</title>
        <meta name="description" content={`Veja os detalhes e estatísticas de ${playerData.PlayerName}.`} />
        <meta property="og:title" content={`Perfil de ${playerData.PlayerName}`} />
        <meta property="og:description" content={`Confira as estatísticas de K/D, assistências e muito mais sobre ${playerData.PlayerName}.`} />
        <meta property="og:image" content={playerPhoto} />
        <meta property="og:url" content={`http://meusite.com/player/${steamId}`} />
      </Helmet>

      {/* Cabeçalho fixo com menu */}
      <header className={`${styles.header} ${scrolled ? styles.scrolled : ''}`}>
        <nav className={styles.nav}>
          <div className={styles.logo}>
            <img src="/imgs/logo.png" alt="Logo" />
          </div>
          <ul className={styles.menuList}>
            <li className={styles.menuItem}>
              <Link to="/">Home</Link>
            </li>
            <li className={styles.menuItem}>
              <Link to="/pages">Pages</Link>
            </li>
            <li className={styles.menuItem}>
              <Link to="/tournament">Tournament</Link>
            </li>
            <li className={styles.menuItem}>
              <Link to="/blog">Blog</Link>
            </li>
            <li className={styles.menuItem}>
              <Link to="/shop">Shop</Link>
            </li>
            <li className={styles.menuItem}>
              <Link to="/landing">Landing</Link>
            </li>
          </ul>
        </nav>
      </header>

      {/* Seção de detalhes do jogador */}
      <div className={styles['player-header']}>
        <img
          className={styles['player-header-background']}
          src={'https://images4.alphacoders.com/113/thumb-1920-1135892.png'}
          alt="Background"
        />
        <div className={styles['player-info']}>
          <img
            loading="lazy"
            src={playerPhoto}  // Usando o caminho correto da imagem do jogador ou da imagem padrão
            alt={`Foto do jogador ${playerData.PlayerName}`}
            className={styles['player-photo']}
          />
          <h1 className={styles['player-name']}>{playerData.PlayerName}</h1>
          <h3>{playerData.Team || "Sem equipe"}</h3>
        </div>
      </div>

      {/* Componente de estatísticas do jogador */}
      <PlayerStats
        kills={playerData.Kills}
        deaths={playerData.Deaths}
        assists={playerData.Assists}
        headshots={playerData.Headshots}
        kdRatio={playerData.Kills / playerData.Deaths}
      />

      {/* Botões de Ação */}

      {/* Notificação de Ação */}
      {showNotification && (
        <div className={styles['notification']}>
          {notificationMessage}
        </div>
      )}

      {/* Sobre o Jogador */}
      <div className={styles['player-about']}>
        <h2>About the Player</h2>
        <p>{playerData.description || "No description available."}</p>
      </div>
    </div>
  );
};

export default PlayerDetails;
