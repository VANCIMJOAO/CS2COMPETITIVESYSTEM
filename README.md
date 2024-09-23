# Rapid Fire Champion Plugin

**Versão**: 1.7.0  
**Autor**: João Victor Vancim

## Descrição

O **Rapid Fire Champion Plugin** é um plugin desenvolvido para servidores de Counter-Strike 2 utilizando a API **CounterStrikeSharp**. Ele é voltado para gerenciar campeonatos, rounds de faca, estatísticas de jogadores e times, e transições entre estados de aquecimento e jogo competitivo. O plugin inclui funcionalidades de gerenciamento de jogadores, atribuição de lados (CT/TR), sistema de prontos (!ready), e criação de histórico de partidas.

## Funcionalidades

### 1. Gerenciamento de Jogadores e Times
- **Associação automática de jogadores a times** com base nos IDs Steam, carregados a partir de um arquivo JSON de times.
- **Atribuição de lados** após o round de faca, permitindo que o time vencedor escolha se começa como CT ou TR.
- **Sistema de status de prontidão**, onde os jogadores precisam digitar `!ready` ou `!r` para marcar sua prontidão antes do início da partida.

### 2. Gerenciamento de Partidas
- **Round de faca**: Após todos os jogadores estarem prontos, o round de faca é executado, e o time vencedor escolhe seu lado.
- **Trocando de lados**: Quando a soma dos rounds ganhos atingir 12, os lados são trocados automaticamente.
- **Criação de histórico de partidas**: Todas as estatísticas da partida são salvas em um arquivo JSON, incluindo dados detalhados de cada round, estatísticas de jogadores e kills.

### 3. Estatísticas de Jogadores
- O plugin coleta estatísticas detalhadas de cada jogador, incluindo:
  - **Kills**, **deaths**, **headshots**, **assists**, e **pontuação geral**.
  - **Estatísticas de arma**, como qual arma foi utilizada para cada kill.
  - **Tempo de vida por round** e **objetivos cumpridos**.
- As estatísticas são atualizadas dinamicamente durante a partida e salvas no arquivo JSON `player_stats.json`.

### 4. Funções Administrativas
- **Sistema de permissões para administradores**:
  - Administradores podem executar comandos avançados como reiniciar o servidor, trocar de mapa, ou monitorar o progresso da partida.
  - Diferentes níveis de permissão: `SuperAdmin`, `Moderator` e `Viewer`.

### 5. Transição de Estados do Jogo
- **Estados suportados**:
  - `Warmup`: Aquecimento, onde os jogadores se preparam e marcam prontidão.
  - `KnifeRound`: Round de faca para decidir quem escolhe o lado.
  - `ReadyForSideChoice`: Estado em que o time vencedor do round de faca escolhe se começa como CT ou TR.
  - `Competitive`: Partida competitiva com contagem de rounds e overtime.
  - `PostGame`: Estado após o término da partida.

### 6. Histórico de Partidas
- Criação automática de arquivos JSON para armazenar o histórico de partidas.
- Armazena detalhes como **pontuação dos times**, **estatísticas de cada jogador**, **kills por round**, e **informações sobre o mapa**.

## Requisitos
- **CounterStrikeSharp API**: O plugin utiliza a API para interagir com o servidor CS2.
- **.NET Core 8.0**: O plugin é compilado e executado com o .NET Core 8.0.
  
## Instalação

1. **Clonar o repositório**:
   git clone https://github.com/VANCIMJOAO/CS2COMPETITIVESYSTEM.git
2. **Instalar as dependências**:
  Certifique-se de que o CounterStrikeSharp API está instalado e configurado corretamente no seu servidor de CS2.
3. **Configurar arquivos JSON**:
  Certifique-se de que o arquivo teams.json está presente na pasta data/teams.json, contendo as associações de SteamIDs aos times.
  O plugin também gera arquivos JSON de estatísticas e histórico de partidas automaticamente.
4. **Executar o plugin**:
  Copie o arquivo compilado do plugin (.dll) para a pasta de plugins do seu servidor CS2.
  Configure o plugin para ser carregado automaticamente no seu servidor.

**Como Utilizar**
  Comandos Disponíveis
  !ready ou !r: Marca o jogador como pronto.
  !ct: O time vencedor do round de faca escolhe começar como CT.
  !tr: O time vencedor do round de faca escolhe começar como TR.
  
**Gerenciamento de Partidas**
  O plugin gerencia automaticamente o progresso da partida, trocando os lados quando necessário e atualizando as estatísticas dos jogadores e times em tempo real.
  
**Estrutura de Arquivos**
  RapidFireChampionPlugin.cs: Código principal do plugin.
  data/player_stats.json: Estatísticas dos jogadores.
  data/teams.json: Arquivo de configuração de times.
  data/match_history/: Diretório onde os arquivos de histórico de partidas são salvos.
  
**Contato**
  Caso tenha dúvidas ou problemas, entre em contato com João Victor Vancim no GitHub ou por e-mail: jvancim@gmail.com.

Esse plugin foi projetado para ajudar na gestão de campeonatos CS2 e na coleta de estatísticas detalhadas. Feedbacks e contribuições são bem-vindos!
