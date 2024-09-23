import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import styles from './Menu.module.css';

const Menu: React.FC = () => {
  const [isOpen, setIsOpen] = useState(false);

  const toggleMenu = () => {
    setIsOpen(!isOpen);
  };

  // Variantes para animações do Framer Motion
  const menuVariants = {
    open: {
      opacity: 1,
      x: 0,
      transition: {
        type: 'spring',
        stiffness: 100,
        damping: 20,
      },
    },
    closed: {
      opacity: 0,
      x: '-100%',
      transition: {
        type: 'spring',
        stiffness: 50,
        damping: 20,
      },
    },
  };

  const linkVariants = {
    open: (i: number) => ({
      opacity: 1,
      x: 0,
      transition: {
        delay: i * 0.1, // Delay entre links para criar efeito cascata
        stiffness: 50,
      },
    }),
    closed: {
      opacity: 0,
      x: '-50px',
      transition: {
        stiffness: 50,
      },
    },
  };

  return (
    <header className={styles.header}>
      {/* Barra de navegação para desktop */}
      <nav className={styles.navbar}>
        <div className={styles.logo}>
          <Link to="/">
            <img src="/imgs/logo.png" alt="Logo" />
          </Link>
        </div>

        {/* Links de navegação para desktop */}
        <ul className={styles.navLinks}>
          <li><Link to="/">Home</Link></li>
          <li><Link to="/matches">Partidas</Link></li>
          <li><Link to="/players">Jogadores</Link></li>
          <li><Link to="/about">Sobre</Link></li>
        </ul>

        {/* Botão de menu para mobile */}
        <div className={styles.hamburger} onClick={toggleMenu}>
          <div className={isOpen ? styles.open : ''}></div>
          <div className={isOpen ? styles.open : ''}></div>
          <div className={isOpen ? styles.open : ''}></div>
        </div>
      </nav>

      {/* Menu deslizante para mobile */}
      <motion.nav
        className={styles.mobileMenu}
        variants={menuVariants}
        animate={isOpen ? 'open' : 'closed'}
      >
        <ul>
          {['Home', 'Partidas', 'Jogadores', 'Sobre'].map((link, i) => (
            <motion.li
              key={i}
              variants={linkVariants}
              custom={i}
              onClick={toggleMenu}
            >
              <Link to={`/${link.toLowerCase()}`}>{link}</Link>
            </motion.li>
          ))}
        </ul>
      </motion.nav>
    </header>
  );
};

export default Menu;
