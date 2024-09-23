import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { io } from 'socket.io-client';
import { debounce } from 'lodash';
import { motion, AnimatePresence } from 'framer-motion'; // Importando AnimatePresence
import styles from './IndexPage.module.css';
import { API_BASE_URL, DEFAULT_LOGO } from './config';

// Mapeamento de imagens dos mapas
const mapImages: { [key: string]: string } = {
  de_dust2: 'https://img.redbull.com/images/c_crop,w_1620,h_1080,x_157,y_0,f_auto,q_auto/c_scale,w_1500/redbullcom/2020/7/15/mdlhtjaz85gjhahyakce/csgo-dust-2-map',
  de_inferno: 'https://example.com/maps/de_inferno.jpg',
  de_mirage: 'https://example.com/maps/de_mirage.jpg',
  de_nuke: 'https://example.com/maps/de_nuke.jpg',
  de_train: 'https://example.com/maps/de_train.jpg',
  de_overpass: 'https://example.com/maps/de_overpass.jpg',
  de_vertigo: 'https://example.com/maps/de_vertigo.jpg',
  default: 'https://via.placeholder.com/300x200', // Imagem padrão
};

const getMapImage = (mapName: string) => {
  return mapImages[mapName] || mapImages['default'];
};

const IndexPage: React.FC = () => {
  const [playerStats, setPlayerStats] = useState<any[]>([]);
  const [liveMatches, setLiveMatches] = useState<any[]>([]);
  const [finishedMatches, setFinishedMatches] = useState<any[]>([]);
  const [scrolled, setScrolled] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false); // Estado para abrir/fechar o menu

  const socket = useMemo(() => io(API_BASE_URL), []);

  const handleScroll = useCallback(
    debounce(() => {
      setScrolled(window.scrollY > 10);
    }, 100),
    []
  );

  useEffect(() => {
    window.addEventListener('scroll', handleScroll);
    return () => {
      window.removeEventListener('scroll', handleScroll);
    };
  }, [handleScroll]);

  useEffect(() => {
    fetch(`${API_BASE_URL}/api/player-stats`)
      .then((response) => response.json())
      .then((data) => setPlayerStats(Object.values(data)))
      .catch((error) => console.error('Erro ao buscar estatísticas dos jogadores:', error));
  }, []);

  useEffect(() => {
    fetch(`${API_BASE_URL}/api/matches`)
      .then((response) => {
        if (!response.ok) throw new Error('Falha ao carregar partidas');
        return response.json();
      })
      .then((data) => {
        setLiveMatches(data.liveMatches);
        setFinishedMatches(data.finishedMatches);
      })
      .catch((error) => console.error('Erro ao buscar partidas:', error));

    socket.on('match-update', (updatedMatch) => {
      if (updatedMatch.Status === 'Ao Vivo') {
        setLiveMatches((prevMatches) => {
          const matchIndex = prevMatches.findIndex((match) => match.id === updatedMatch.id);
          if (matchIndex !== -1) {
            const updatedMatches = [...prevMatches];
            updatedMatches[matchIndex] = updatedMatch;
            return updatedMatches;
          }
          return [...prevMatches, updatedMatch];
        });
      } else if (updatedMatch.Status === 'Finalizada') {
        setFinishedMatches((prevMatches) => {
          const matchIndex = prevMatches.findIndex((match) => match.id === updatedMatch.id);
          if (matchIndex !== -1) {
            const updatedMatches = [...prevMatches];
            updatedMatches[matchIndex] = updatedMatch;
            return updatedMatches;
          }
          return [...prevMatches, updatedMatch];
        });
      }
    });

    return () => {
      socket.disconnect();
    };
  }, [socket]);

  // Função para abrir/fechar o menu no modo responsivo
  const toggleMenu = () => {
    setMenuOpen(!menuOpen);
  };

  // Fecha o menu quando clica na sobreposição
  const handleBackdropClick = () => {
    setMenuOpen(false);
  };

  const liveMatchesList = useMemo(
    () =>
      liveMatches.map((match) => {
        const matchId = match.matchId;
        return (
          <li key={matchId} className={styles['match-item']}>
            <Link to={`/matchdetails/${matchId}`}>
              <div className={styles['match-image-container']}>
                <img
                  className={styles['match-image']}
                  src={getMapImage(match.Map)}
                  alt={`Mapa ${match.Map}`}
                />
                <div className={styles['gradient-overlay']}></div>
              </div>

              <div className={styles['match-info-container']}>
                <div className={styles['match-teams-info']}>
                  <img className={styles['team-logo']} src={match.Team1Logo || DEFAULT_LOGO} alt={match.Team1} />
                  <span className={styles['team-name']}>{match.Team1}</span>
                  <span className={styles['score']}>{match.Team1Score}</span>
                  <div className={styles['divider']}></div>
                  <span className={styles['score']}>{match.Team2Score}</span>
                  <span className={styles['team-name']}>{match.Team2}</span>
                  <img className={styles['team-logo']} src={match.Team2Logo || DEFAULT_LOGO} alt={match.Team2} />
                </div>
                <span className={match.Status === 'Ao Vivo' ? styles['live-badge'] : styles['finished-badge']}>
                  {match.Status === 'Ao Vivo' ? 'AO VIVO' : 'FINALIZADA'}
                </span>
              </div>
            </Link>
          </li>
        );
      }),
    [liveMatches]
  );

  const finishedMatchesList = useMemo(
    () =>
      finishedMatches.map((match) => {
        const matchId = match.matchId;
        return (
          <li key={matchId} className={styles['match-item']}>
            <Link to={`/matchdetails/${matchId}`}>
              <div className={styles['match-image-container']}>
                <img
                  className={styles['match-image']}
                  src={getMapImage(match.Map)}
                  alt={`Mapa ${match.Map}`}
                />
                <div className={styles['gradient-overlay']}></div>
              </div>

              <div className={styles['match-info-container']}>
                <div className={styles['match-teams-info']}>
                  <img className={styles['team-logo']} src={match.Team1Logo || DEFAULT_LOGO} alt={match.Team1} />
                  <span className={styles['team-name']}>{match.Team1}</span>
                  <span className={styles['score']}>{match.Team1Score}</span>
                  <div className={styles['divider']}></div>
                  <span className={styles['score']}>{match.Team2Score}</span>
                  <span className={styles['team-name']}>{match.Team2}</span>
                  <img className={styles['team-logo']} src={match.Team2Logo || DEFAULT_LOGO} alt={match.Team2} />
                </div>

                <span className={styles['finished-badge']}>FINALIZADA</span>
              </div>
            </Link>
          </li>
        );
      }),
    [finishedMatches]
  );

  const playerStatsList = useMemo(
    () =>
      playerStats.map((player) => (
        <li key={player.SteamID} className={styles['player-card']}>
          <div className={styles['player-photo-container']}>
            <img
              src={`${API_BASE_URL}/api/${player.photo || 'uploads/default-player-photo.jpg'}`}
              alt={`Foto de ${player.PlayerName}`}
              className={styles['player-photo']}
              onError={(e) => {
                e.currentTarget.src = DEFAULT_LOGO;
              }}
            />
          </div>
          <div className={styles['player-info']}>
            <h4>{player.PlayerName}</h4>
            <p className={styles['kd-stat']}>
  K/D: {player.Deaths !== 0 ? (player.Kills / player.Deaths).toFixed(2) : player.Kills}
</p>


          </div>
        </li>
      )),
    [playerStats]
  );

  // Adição de cards de notícias
  const newsList = (
    <div className={styles['news-cards']}>
      <div className={styles['news-card']}>
        <img src="https://img-cdn.hltv.org/gallerypicture/bhsTYUyExvvAE29RNtPIBI.png?auto=compress&fm=avif&ixlib=java-2.1.0&q=75&w=800&s=d0de4e700e4ae4c3032fa580f187ac96" alt="Notícia 1" />
        <div className={styles['news-content']}>
          <h3>Astralis Signs Cadian as Rifling IGL</h3>
          <p>Astralis reforça seu time com a adição de Cadian como o novo IGL...</p>
          <Link to="#" className={styles['read-more']}>Leia Mais</Link>
        </div>
      </div>
      <div className={styles['news-card']}>
        <img src="https://img-cdn.hltv.org/gallerypicture/TZ9cbEVuQ_Va3Yr4qAwPO7.png?auto=compress&fm=avif&ixlib=java-2.1.0&q=75&w=800&s=7eac08d2ce6e91d961cf328227783853" alt="Notícia 2" />
        <div className={styles['news-content']}>
          <h3>Eternal Fire Wins Over Anubis in Overtime</h3>
          <p>O time Eternal Fire vence em uma partida emocionante contra Anubis...</p>
          <Link to="#" className={styles['read-more']}>Leia Mais</Link>
        </div>
      </div>
    </div>
  );

  return (
    <>
      {/* Menu Moderno com animação */}
      <motion.nav
        className={styles.navbar}
        initial={{ y: -100 }}
        animate={{ y: 0 }}
        transition={{ type: 'spring', stiffness: 70, duration: 0.5 }}
      >
        <div className={styles.logo}>
          <Link to="/">Logo</Link>
        </div>
        
        <div
  className={styles.hamburger}
  onClick={toggleMenu}
  aria-label="Menu de navegação"
  role="button"
  tabIndex={0}
  onKeyPress={(e) => {
    if (e.key === 'Enter' || e.key === ' ') toggleMenu();
  }}
>
  <span className={styles.line}></span>
  <span className={styles.line}></span>
  <span className={styles.line}></span>
</div>

<button className={styles.hamburger} onClick={toggleMenu} aria-label="Menu de navegação">
  <span className={styles.line}></span>
  <span className={styles.line}></span>
  <span className={styles.line}></span>
</button>

        {/* Overlay Menu */}
        <AnimatePresence>
        {menuOpen && (
  <>
    {console.log("Menu aberto")} {/* Verificar se o menu está sendo renderizado */}
    <motion.div
      className={styles.backdrop}
      initial={{ opacity: 0 }}
      animate={{ opacity: 0.5 }}
      exit={{ opacity: 0 }}
      onClick={handleBackdropClick}
    />
    <motion.ul
      className={styles.menu}
      initial={{ x: '100%' }}
      animate={{ x: 0 }}
      exit={{ x: '100%' }}
      transition={{ type: 'tween', duration: 0.5 }}
    >
      <motion.li initial={{ opacity: 0, x: 50 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: 0.2 }}>
        <Link to="/partidas" onClick={() => setMenuOpen(false)}>
          Partidas
        </Link>
      </motion.li>
      <motion.li initial={{ opacity: 0, x: 50 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: 0.4 }}>
        <Link to="/jogadores" onClick={() => setMenuOpen(false)}>
          Jogadores
        </Link>
      </motion.li>
      <motion.li initial={{ opacity: 0, x: 50 }} animate={{ opacity: 1, x: 0 }} transition={{ delay: 0.6 }}>
        <Link to="/sobre" onClick={() => setMenuOpen(false)}>
          Sobre
        </Link>
      </motion.li>
    </motion.ul>
  </>
)}
        </AnimatePresence>
      </motion.nav>

      <div className={styles['layout-container']}>
        {/* Coluna Esquerda */}
        <aside className={styles['left-column']}>
          <section className={styles['news-section']}>
            <h3>Notícias Recentes</h3>
            {newsList}
          </section>
        </aside>

        {/* Coluna Central */}
        <main className={styles['center-column']}>
          <section className={styles['live-matches-section']}>
            <h2>Partidas ao Vivo</h2>
            <ul className={styles['match-list']}>
              {liveMatches.length > 0 ? liveMatchesList : <div className={styles.noMatches}>Nenhuma partida ao vivo no momento.</div>}
            </ul>
          </section>

          <section className={styles['finished-matches-section']}>
            <h2>Partidas Finalizadas</h2>
            <ul className={styles['match-list']}>
              {finishedMatches.length > 0 ? finishedMatchesList : <div className={styles.noMatches}>Nenhuma partida finalizada disponível.</div>}
            </ul>
          </section>
        </main>

        {/* Coluna Direita */}
        <aside className={styles['right-column']}>
          <section className={styles['player-stats-section']}>
            <h3>Estatísticas dos Jogadores</h3>
            <ul className={styles['player-stats-list']}>
              {playerStats.length > 0 ? playerStatsList : <div className={styles.loader}>Carregando estatísticas...</div>}
            </ul>
          </section>
        </aside>
      </div>
    </>
  );
};

export default IndexPage;
