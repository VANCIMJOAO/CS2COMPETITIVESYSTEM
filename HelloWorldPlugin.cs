using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RapidFireChampionPlugin
{
    public class RapidFireChampionPlugin : BasePlugin
    {
        public override string ModuleName => "Rapid Fire Champion Plugin";
        public override string ModuleVersion => "1.7.0";

        private readonly ConcurrentDictionary<ulong, bool> playerReadyStatusCache = new ConcurrentDictionary<ulong, bool>();
        private readonly ConcurrentBag<CCSPlayerController> connectedPlayers = new ConcurrentBag<CCSPlayerController>();
        private readonly ConcurrentDictionary<ulong, PlayerStats> playerStats = new ConcurrentDictionary<ulong, PlayerStats>();
        private readonly ConcurrentDictionary<ulong, string> playerTeams = new ConcurrentDictionary<ulong, string>();
        private GameState currentState = GameState.Warmup;
        private CsTeam? knifeRoundWinner;

        private readonly ConcurrentDictionary<ulong, string> adminTokens = new ConcurrentDictionary<ulong, string>();
        private readonly Dictionary<ulong, AdminRole> adminRoles = new Dictionary<ulong, AdminRole>
        {
            { 76561198012345678, AdminRole.SuperAdmin },
            { 76561198087654321, AdminRole.Moderator }
        };

        private readonly string playerStatsFilePath;
        private readonly string teamsFilePath;
        private readonly string matchHistoryDirectoryPath;
        private string? currentMatchFilePath;
        private readonly object statsLock = new object();

        private int ctWins = 0;
        private int tWins = 0;

        public RapidFireChampionPlugin()
        {
            playerStatsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "stats", "player_stats.json");
            teamsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "teams.json");
            matchHistoryDirectoryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "match_history");
            LoadTeamsData();
            LogAction("Instância do RapidFireChampionPlugin criada.", LogLevel.INFO);
        }

        public override void Load(bool hotReload)
        {
            LogAction("Método Load chamado.", LogLevel.INFO);
            ExecuteWarmupCommands();
            RegisterCommandListeners();
            RegisterEventHandlers();
            _ = AutoMessage();
        }

   private void LoadTeamsData()
{
    try
    {
        LogAction("Iniciando carregamento dos dados dos times.", LogLevel.INFO);

        if (!File.Exists(teamsFilePath))
        {
            LogAction($"Erro: Arquivo {teamsFilePath} não encontrado.", LogLevel.ERROR);
            return;
        }

        string teamsJson = File.ReadAllText(teamsFilePath);
        LogAction($"Conteúdo do JSON carregado: {teamsJson}", LogLevel.DEBUG);

        var teams = JsonSerializer.Deserialize<List<Team>>(teamsJson);

        if (teams == null || !teams.Any())
        {
            LogAction("Erro: A desserialização retornou null ou a lista de times está vazia. Verifique o formato do JSON.", LogLevel.ERROR);
            return;
        }

        LogAction($"Número de times carregados: {teams.Count}", LogLevel.INFO);
        teamNameToTeamId.Clear(); // Limpa o dicionário antes de recarregar os dados

        foreach (var team in teams)
        {
            if (string.IsNullOrWhiteSpace(team.TeamName))
            {
                LogAction("Erro: team.TeamName é nulo ou vazio após a desserialização.", LogLevel.ERROR);
                continue;
            }

            string teamName = team.TeamName.Trim();
            LogAction($"team.TeamName após trim: '{teamName}'", LogLevel.DEBUG);

            // Atribui um ID ao time, se ele ainda não tiver um
            if (!teamNameToTeamId.ContainsKey(teamName))
            {
                int teamId = teamNameToTeamId.Count + 1;
                teamNameToTeamId[teamName] = teamId;
                LogAction($"Time '{teamName}' associado ao ID {teamId}.", LogLevel.INFO);
            }

            foreach (var player in team.Players)
            {
                if (string.IsNullOrEmpty(player.SteamId))
                {
                    LogAction($"Aviso: SteamID vazio encontrado no time '{teamName}'. Ignorando jogador.", LogLevel.WARNING);
                    continue;
                }

                if (ulong.TryParse(player.SteamId.Trim(), out ulong steamID))
                {
                    playerTeams[steamID] = teamName;
                    LogAction($"Jogador com SteamID {steamID} associado ao time '{teamName}'.", LogLevel.INFO);
                }
                else
                {
                    LogAction($"Falha ao converter SteamID '{player.SteamId}' para ulong.", LogLevel.ERROR);
                }
            }
        }

        LogAction($"Dados dos times carregados com sucesso. {playerTeams.Count} jogadores associados aos seus times.", LogLevel.INFO);
    }
    catch (Exception ex)
    {
        LogAction($"Erro ao carregar dados dos times: {ex.Message}", LogLevel.ERROR, ex);
    }
}



private Dictionary<string, int> teamNameToTeamId = new Dictionary<string, int>();

// Mapping from CsTeam to team IDs (updates when sides switch)
private Dictionary<CsTeam, int> csTeamToTeamId = new Dictionary<CsTeam, int>();

        private void ExecuteWarmupCommands()
        {
            Task.Run(async () =>
            {
                try
                {
                    LogAction("Iniciando execução dos comandos de aquecimento...", LogLevel.INFO);
                    await Task.Delay(10000);

                    LogAction("Definindo senha do RCON...", LogLevel.INFO);
                    await Server.NextFrameAsync(() =>
                    {
                        Server.ExecuteCommand("rcon_password rconconfig");
                        LogAction("Senha do RCON definida.", LogLevel.DEBUG);
                    });

                    await Task.Delay(1000);

                    LogAction("Executando comandos de aquecimento diretamente...", LogLevel.INFO);
                    await Server.NextFrameAsync(() =>
                    {
                        ExecuteCommandWithConfirmation("exec warmup.cfg", "Comandos de aquecimento executados com sucesso.");
                    });
                }
                catch (Exception ex)
                {
                    LogAction($"Erro ao executar warmup.cfg: {ex.Message}", LogLevel.ERROR, ex);
                    RollbackState(GameState.Warmup);
                }
            });
        }

        private void RegisterCommandListeners()
        {
            try
            {
                LogAction("Registrando ouvintes de comandos...", LogLevel.DEBUG);
                AddCommandListener("ready", CommandListener_Ready);
                AddCommandListener("r", CommandListener_Ready);
                AddCommandListener("ct", CommandListener_ChooseCT);
                AddCommandListener("tr", CommandListener_ChooseT);
                LogAction("Ouvintes de comandos registrados.", LogLevel.INFO);
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao registrar ouvintes de comandos: {ex.Message}", LogLevel.ERROR, ex);
                RollbackState(GameState.Warmup);
            }
        }

private void RegisterEventHandlers()
{
    try
    {
        LogAction("Tentando registrar manipuladores de eventos...", LogLevel.DEBUG);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);  // Certifique-se de que este evento está registrado
        LogAction("Manipuladores de eventos registrados com sucesso.", LogLevel.INFO);
    }
    catch (Exception ex)
    {
        LogAction($"Erro ao registrar manipuladores de eventos: {ex.Message}", LogLevel.ERROR, ex);
        RollbackState(GameState.Warmup);
    }
}

     private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
{
    try
    {
        LogAction("OnPlayerConnectFull: Disparado", LogLevel.DEBUG);

        CCSPlayerController? playerController = @event.Userid?.As<CCSPlayerController>();

        if (playerController != null && playerController.IsValid)
        {
            ulong steamID64 = SteamIDConverter.ConvertToSteamID64(playerController.SteamID);
            LogAction($"OnPlayerConnectFull: Jogador {playerController.PlayerName ?? "Unknown"} conectado com SteamID64: {steamID64}", LogLevel.DEBUG);

            // Adiciona o jogador aos jogadores conectados
            connectedPlayers.Add(playerController);

            // Adiciona ou atualiza o cache de status de pronto do jogador
            playerReadyStatusCache[steamID64] = false;

            // Tenta associar o jogador a um time
            if (!playerTeams.TryGetValue(steamID64, out string? teamName))
            {
                LogAction($"Time não encontrado no cache para o jogador {playerController.PlayerName ?? "Unknown"}. Recarregando times do arquivo.", LogLevel.WARNING);
                LoadTeamsData();

                // Tenta novamente após recarregar os times
                if (playerTeams.TryGetValue(steamID64, out teamName))
                {
                    LogAction($"Jogador {playerController.PlayerName ?? "Unknown"} foi associado ao time {teamName} após recarregar.", LogLevel.INFO);
                }
                else
                {
                    LogAction($"Jogador {playerController.PlayerName ?? "Unknown"} com SteamID {steamID64} não foi encontrado mesmo após recarregar os times.", LogLevel.WARNING);
                }
            }
            else
            {
                LogAction($"Jogador {playerController.PlayerName ?? "Unknown"} já estava associado ao time {teamName} no cache.", LogLevel.INFO);
            }

            // Envia mensagem para o jogador informando seu time
            if (teamName != null)
            {
                Server.ExecuteCommand($"csay \"{playerController.PlayerName ?? "Unknown"}, você foi associado ao time {teamName}.\"");
            }
            else
            {
                Server.ExecuteCommand($"csay \"{playerController.PlayerName ?? "Unknown"}, você não está associado a nenhum time.\"");
            }

            // Mensagens de boas-vindas e status de pronto
            Server.NextFrame(() =>
            {
                try
                {
                    string welcomeMessage = $"{playerController.PlayerName ?? "Unknown"}, você está no meu campeonato de CS2 do interior!";
                    string readyMessage = "Digite !ready ou !r para marcar-se como pronto.";
                    string notReadyPlayers = GetNotReadyPlayersMessage();

                    Server.ExecuteCommand($"csay \"{welcomeMessage}\"");
                    Server.ExecuteCommand($"csay \"{readyMessage}\"");
                    Server.ExecuteCommand($"csay \"{notReadyPlayers}\"");

                    LogAction($"OnPlayerConnectFull: Mensagens enviadas para {playerController.PlayerName ?? "Unknown"}", LogLevel.DEBUG);
                }
                catch (Exception ex)
                {
                    LogAction($"Erro ao enviar mensagens para {playerController.PlayerName ?? "Unknown"}: {ex.Message}", LogLevel.ERROR, ex);
                }
            });

            return HookResult.Continue;
        }
        return HookResult.Handled;
    }
    catch (Exception ex)
    {
        LogAction($"Erro em OnPlayerConnectFull: {ex.Message}", LogLevel.ERROR, ex);
        return HookResult.Handled;
    }
}

private int GetCurrentTeamScore(CsTeam team)
{
    // Retorna o placar atual do time a partir dos dados do histórico de partida
    if (currentMatchFilePath == null || !File.Exists(currentMatchFilePath))
    {
        LogAction("Erro: Arquivo de histórico de partida não encontrado ou não foi criado.", LogLevel.ERROR);
        return 0;
    }

    string json = File.ReadAllText(currentMatchFilePath);
    var matchData = JsonSerializer.Deserialize<MatchHistory>(json);

    if (matchData == null) return 0;

    return (team == CsTeam.CounterTerrorist) ? matchData.Team1Score : matchData.Team2Score;
}

private void SwitchSides()
{
    try
    {
        LogAction("Trocando os lados dos times.", LogLevel.INFO);

        foreach (var player in connectedPlayers)
        {
            if (player.IsValid)
            {
                ulong steamID64 = SteamIDConverter.ConvertToSteamID64(player.SteamID);

                if (playerTeams.TryGetValue(steamID64, out string? playerTeamName))
                {
                    if (currentTeamSides[CsTeam.CounterTerrorist] == playerTeamName)
                    {
                        // Move time CT atual para TR
                        player.ChangeTeam(CsTeam.Terrorist);
                        LogAction($"Jogador {player.PlayerName ?? "Unknown"} movido para TR.", LogLevel.INFO);
                    }
                    else if (currentTeamSides[CsTeam.Terrorist] == playerTeamName)
                    {
                        // Move time TR atual para CT
                        player.ChangeTeam(CsTeam.CounterTerrorist);
                        LogAction($"Jogador {player.PlayerName ?? "Unknown"} movido para CT.", LogLevel.INFO);
                    }
                }
            }
        }

        // Troca os lados no dicionário de times
        string temp = currentTeamSides[CsTeam.CounterTerrorist];
        currentTeamSides[CsTeam.CounterTerrorist] = currentTeamSides[CsTeam.Terrorist];
        currentTeamSides[CsTeam.Terrorist] = temp;

        LogAction("Lados dos times trocados com sucesso.", LogLevel.INFO);
    }
    catch (Exception ex)
    {
        LogAction($"Erro ao trocar os lados dos times: {ex.Message}", LogLevel.ERROR, ex);
    }
}



 private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
{
    try
    {
        LogAction("Round start event triggered.", LogLevel.INFO);

        if (currentState == GameState.KnifeRound || currentState == GameState.Competitive)
        {
            // Incrementa o contador de rounds no início do round
            if (currentState == GameState.Competitive)
            {
                totalRoundsPlayed++;

                // Verifica se a soma dos pontos entre os dois times é igual a 12
                int totalScore = GetCurrentTeamScore(CsTeam.CounterTerrorist) + GetCurrentTeamScore(CsTeam.Terrorist);

                if (!sidesSwitched && totalScore == 12)
                {
                    LogAction("Atingido o total de 12 rounds, trocando os lados dos times.", LogLevel.INFO);
                    SwitchSides();
                    sidesSwitched = true;  // Marca que os lados já foram trocados
                }

                InitializeRoundDetails(totalRoundsPlayed);
            }

            return HookResult.Continue;
        }

        return HookResult.Continue;
    }
    catch (Exception ex)
    {
        LogAction($"Error in OnRoundStart: {ex.Message}", LogLevel.ERROR, ex);
        return HookResult.Handled;
    }
}



private void InitializeRoundDetails(int roundNumber)
{
    try
    {
        if (currentMatchFilePath == null || !File.Exists(currentMatchFilePath))
        {
            LogAction("Error: Match history file not found or not created.", LogLevel.ERROR);
            return;
        }

        string json = File.ReadAllText(currentMatchFilePath);
        var matchData = JsonSerializer.Deserialize<MatchHistory>(json);

        if (matchData != null)
        {
            var newRound = new RoundDetail
            {
                RoundNumber = matchData.RoundDetails.Count + 1,
                Team1Kills = 0,
                Team2Kills = 0,
                PlayerKills = new Dictionary<ulong, int>(),
                PlayerKillsDetails = new Dictionary<ulong, List<string>>(),
                PlayerNicknames = new Dictionary<ulong, string>(),
                ScoreTeam1 = matchData.Team1Score,
                ScoreTeam2 = matchData.Team2Score
            };

            matchData.RoundDetails.Add(newRound);

            string updatedJson = JsonSerializer.Serialize(matchData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(currentMatchFilePath, updatedJson);

            LogAction($"New round {newRound.RoundNumber} initialized in match history.", LogLevel.INFO);
        }
    }
    catch (Exception ex)
    {
        LogAction($"Error initializing round details: {ex.Message}", LogLevel.ERROR, ex);
    }
}



private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
{
    try
    {
        // Verifique se estamos no estado competitivo antes de registrar as estatísticas
        if (currentState != GameState.Competitive)
        {
            LogAction("OnPlayerDeath: Estatísticas ignoradas, pois o jogo não está no modo competitivo.", LogLevel.INFO);
            return HookResult.Continue;
        }

        // Captura os dados apenas no modo competitivo
        LogAction("OnPlayerDeath: Capturando estatísticas de mortes.", LogLevel.INFO);

        CCSPlayerController? attacker = @event.Attacker;
        CCSPlayerController? victim = @event.Userid;

        // Ignora eventos de suicídio (quando atacante e vítima são o mesmo jogador)
        if (attacker != null && victim != null && attacker.SteamID == victim.SteamID)
        {
            LogAction($"OnPlayerDeath: Suicídio ignorado para o jogador {attacker.PlayerName}.", LogLevel.INFO);
            return HookResult.Continue;
        }

        // Continua apenas se ambos o atacante e a vítima forem válidos
        if (attacker != null && victim != null && attacker.IsValid && victim.IsValid)
        {
            ulong attackerSteamID = GetSteamID(attacker);
            ulong victimSteamID = GetSteamID(victim);

            // Atualiza as estatísticas do atacante
            UpdatePlayerStats(attackerSteamID, stats =>
            {
                stats.Kills++;
                stats.Weapon = @event.Weapon;

                if (@event.Headshot)
                {
                    stats.Headshots++;
                }
            });

            // Atualiza as estatísticas da vítima
            UpdatePlayerStats(victimSteamID, stats =>
            {
                stats.Deaths++;
            });

            string attackerName = attacker.PlayerName ?? "Unknown";
            string victimName = victim.PlayerName ?? "Unknown";
            string weaponUsed = @event.Weapon ?? "arma desconhecida";

            // Atualiza o histórico da partida com os detalhes da morte
            UpdateMatchHistoryWithKill(attackerSteamID, victimSteamID, attackerName, victimName, weaponUsed);

            // Salva as estatísticas de forma segura
            SavePlayerStatsSafely();
        }

        return HookResult.Continue;
    }
    catch (Exception ex)
    {
        LogAction($"Erro em OnPlayerDeath: {ex.Message}", LogLevel.ERROR, ex);
        return HookResult.Handled;
    }
}



private void UpdateMatchHistoryWithKill(ulong attackerSteamID, ulong victimSteamID, string attackerName, string victimName, string weapon)
{
    try
    {
        if (currentMatchFilePath == null || !File.Exists(currentMatchFilePath))
        {
            LogAction("Erro: Arquivo de histórico de partida não encontrado ou não foi criado.", LogLevel.ERROR);
            return;
        }

        string json = File.ReadAllText(currentMatchFilePath);
        var matchData = JsonSerializer.Deserialize<MatchHistory>(json);

        if (matchData != null)
        {
            // Verifica se há detalhes do round atual
            var currentRound = matchData.RoundDetails.LastOrDefault();
            if (currentRound != null)
            {
                // Verifique se o SteamID do atacante já está registrado no round
                if (!currentRound.PlayerKillsDetails.ContainsKey(attackerSteamID))
                {
                    currentRound.PlayerKillsDetails[attackerSteamID] = new List<string>();
                }

                // Adiciona o evento de morte ao round atual
                currentRound.PlayerKillsDetails[attackerSteamID].Add($"{attackerName} matou {victimName} com {weapon}");

                // Atualiza o arquivo de histórico com o novo status
                string updatedJson = JsonSerializer.Serialize(matchData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(currentMatchFilePath, updatedJson);

                LogAction($"Histórico atualizado com o evento de morte: {attackerName} matou {victimName} com {weapon} no round {currentRound.RoundNumber}.", LogLevel.INFO);
            }
            else
            {
                LogAction($"Erro: Não foi possível encontrar o round atual no histórico.", LogLevel.ERROR);
            }
        }
    }
    catch (Exception ex)
    {
        LogAction($"Erro ao atualizar histórico com o evento de morte: {ex.Message}", LogLevel.ERROR, ex);
    }
}



        private ulong GetSteamID(CCSPlayerController player)
        {
            if (player.IsBot)
            {
                // Gerar um ID único e fixo para bots, baseando-se em um valor arbitrário e no índice do bot
                return (ulong)(0xFFFFFFFFFFFF0000 | (uint)player.Slot); // Ou um outro valor fixo para diferenciar bots de jogadores
            }
            else
            {
                return SteamIDConverter.ConvertToSteamID64(player.SteamID);
            }
        }

        private HookResult CommandListener_Ready(CCSPlayerController? player, CommandInfo command)
        {
            try
            {
                LogAction("CommandListener_Ready: Disparado", LogLevel.DEBUG);

                if (player != null && player.IsValid)
                {
                    playerReadyStatusCache[player.SteamID] = true;

                    string readyMessage = "{green}[Rapid Fire Plugin]{default}: {white}" + (player.PlayerName ?? "Unknown") + "{default} está pronto!";
                    string notReadyPlayers = GetNotReadyPlayersMessage();

                    Server.NextFrame(() =>
                    {
                        try
                        {
                            Server.ExecuteCommand($"csay \"{readyMessage}\"");
                            Server.ExecuteCommand($"csay \"{notReadyPlayers}\"");

                            LogAction($"CommandListener_Ready: {player.PlayerName ?? "Unknown"} marcado como pronto.", LogLevel.INFO);
                        }
                        catch (Exception ex)
                        {
                            LogAction($"Erro ao enviar mensagens de pronto para {player.PlayerName ?? "Unknown"}: {ex.Message}", LogLevel.ERROR, ex);
                        }
                    });

                    if (playerReadyStatusCache.Values.All(status => status))
                    {
                        if (IsValidTransition(currentState, GameState.KnifeRound))
                        {
                            LogAction("Todos os jogadores estão prontos. Executando comandos do round de faca.", LogLevel.INFO);
                            SetState(GameState.KnifeRound);
                            ExecuteKnifeRoundCommands();
                        }
                        else
                        {
                            LogInvalidTransition(GameState.KnifeRound);
                        }
                    }
                }
                return HookResult.Handled;
            }
            catch (Exception ex)
            {
                LogAction($"Erro em CommandListener_Ready: {ex.Message}", LogLevel.ERROR, ex);
                return HookResult.Handled;
            }
        }

      private void ExecuteKnifeRoundCommands()
{
    try
    {
        LogAction("Iniciando execução dos comandos do round de faca...", LogLevel.INFO);
        Server.NextFrame(() =>
        {
            try
            {
                ExecuteCommandWithConfirmation("exec faca.cfg", "Comandos do round de faca executados com sucesso.");
                RemoveC4FromAllPlayers();
                // Remover a criação de match history daqui
                // CreateNewMatchHistoryFile();  --> Removido daqui
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao executar comandos do round de faca: {ex.Message}", LogLevel.ERROR, ex);
                RollbackState(GameState.Warmup);
            }
        });
    }
    catch (Exception ex)
    {
        LogAction($"Erro em ExecuteKnifeRoundCommands: {ex.Message}", LogLevel.ERROR, ex);
    }
}


        private void RemoveC4FromAllPlayers()
        {
            try
            {
                LogAction("Removendo C4 de todos os jogadores.", LogLevel.DEBUG);
                foreach (var player in connectedPlayers)
                {
                    if (player.IsValid)
                    {
                        try
                        {
                            LogAction($"Removendo armas do jogador {player.PlayerName ?? "Unknown"}", LogLevel.DEBUG);
                            player.RemoveWeapons();

                            if (PlayerHasC4(player))
                            {
                                LogAction($"C4 encontrado e removido de {player.PlayerName ?? "Unknown"}", LogLevel.DEBUG);
                                player.DropActiveWeapon();
                            }

                            LogAction($"Dando faca para o jogador {player.PlayerName ?? "Unknown"}", LogLevel.DEBUG);
                            player.GiveNamedItem("weapon_knife");
                        }
                        catch (Exception ex)
                        {
                            LogAction($"Erro ao processar jogador {player.PlayerName ?? "Unknown"}: {ex.Message}", LogLevel.ERROR, ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction($"Erro em RemoveC4FromAllPlayers: {ex.Message}", LogLevel.ERROR, ex);
            }
        }

        private bool PlayerHasC4(CCSPlayerController player)
        {
            try
            {
                LogAction($"Verificando se o jogador {player.PlayerName ?? "Unknown"} tem C4.", LogLevel.DEBUG);
                string c4Name = "weapon_c4";
                string? activeWeapon = player.GetConVarValue("cl_activeweapon");
                bool hasC4 = activeWeapon != null && activeWeapon.Contains(c4Name);

                LogAction($"Jogador {player.PlayerName ?? "Unknown"} tem C4: {hasC4}", LogLevel.DEBUG);
                return hasC4;
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao verificar se o jogador {player.PlayerName ?? "Unknown"} tem C4: {ex.Message}", LogLevel.ERROR, ex);
                return false;
            }
        }

private int totalRoundsPlayed = 0;
private bool sidesSwitched = false;
private Dictionary<CsTeam, string> currentTeamSides = new Dictionary<CsTeam, string>();

private void AssignTeamSidesAfterKnifeRound(CsTeam knifeRoundWinnerTeam)
{
    try
    {
        LogAction("Assigning teams to sides after knife round...", LogLevel.INFO);

        var distinctTeams = playerTeams.Values.Distinct().ToList();
        if (distinctTeams.Count < 2)
        {
            LogAction("Error: Não há times suficientes para a atribuição.", LogLevel.ERROR);
            return;
        }

        // Verifica se os times estão no dicionário
        if (!teamNameToTeamId.ContainsKey(distinctTeams[0]) || !teamNameToTeamId.ContainsKey(distinctTeams[1]))
        {
            LogAction("Error: Um ou ambos os times não estão presentes no dicionário teamNameToTeamId.", LogLevel.ERROR);
            LoadTeamsData();

            // Verifica novamente após recarregar os dados
            if (!teamNameToTeamId.ContainsKey(distinctTeams[0]) || !teamNameToTeamId.ContainsKey(distinctTeams[1]))
            {
                LogAction("Error: Recarregar os times falhou. Não foi possível encontrar os times no dicionário.", LogLevel.ERROR);
                return;
            }
        }

        // Atribui os times aos lados CT e TR
        if (knifeRoundWinnerTeam == CsTeam.CounterTerrorist)
        {
            teamSides[CsTeam.CounterTerrorist] = distinctTeams[0];
            teamSides[CsTeam.Terrorist] = distinctTeams[1];
        }
        else if (knifeRoundWinnerTeam == CsTeam.Terrorist)
        {
            teamSides[CsTeam.CounterTerrorist] = distinctTeams[1];
            teamSides[CsTeam.Terrorist] = distinctTeams[0];
        }

        // Atualiza o dicionário csTeamToTeamId com os times e IDs corretos
        // Atualiza o dicionário csTeamToTeamId com os times e IDs corretos
        csTeamToTeamId[CsTeam.CounterTerrorist] = teamNameToTeamId[teamSides[CsTeam.CounterTerrorist]];
        csTeamToTeamId[CsTeam.Terrorist] = teamNameToTeamId[teamSides[CsTeam.Terrorist]];

        // Inicializa currentTeamSides
        currentTeamSides[CsTeam.CounterTerrorist] = teamSides[CsTeam.CounterTerrorist];
        currentTeamSides[CsTeam.Terrorist] = teamSides[CsTeam.Terrorist];

        LogAction($"Team {teamSides[CsTeam.CounterTerrorist]} assigned to CTs with Team ID {csTeamToTeamId[CsTeam.CounterTerrorist]}.", LogLevel.INFO);
        LogAction($"Team {teamSides[CsTeam.Terrorist]} assigned to Ts with Team ID {csTeamToTeamId[CsTeam.Terrorist]}.", LogLevel.INFO);
    }
    catch (Exception ex)
    {
        LogAction($"Error assigning teams to sides: {ex.Message}", LogLevel.ERROR, ex);
    }
}


private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
{
    try
    {
        LogAction("Round end event triggered.", LogLevel.INFO);

        // Check if the current state is KnifeRound
        if (currentState == GameState.KnifeRound)
        {
            // Determine the winner of the knife round
            knifeRoundWinner = (CsTeam)@event.Winner;
            LogAction($"Knife round won by: {knifeRoundWinner}", LogLevel.INFO);

            // Assign team sides based on the knife round winner
            AssignTeamSidesAfterKnifeRound(knifeRoundWinner.Value);

            // Transition to the state where teams choose sides
            SetState(GameState.ReadyForSideChoice);

            // Notify players to choose sides
            Server.ExecuteCommand($"csay \"Time vencedor do round de faca, escolha seu lado com !ct ou !tr.\"");
            return HookResult.Continue;
        }

        // Proceed with the regular round end logic
        int winnerTeamId = -1;

        // Map the winning CsTeam to a team ID
        if (@event.Winner == (int)CsTeam.CounterTerrorist && csTeamToTeamId.TryGetValue(CsTeam.CounterTerrorist, out int ctTeamId))
        {
            winnerTeamId = ctTeamId;
            LogAction($"Team ID {winnerTeamId} (CT) venceu o round.", LogLevel.INFO);
        }
        else if (@event.Winner == (int)CsTeam.Terrorist && csTeamToTeamId.TryGetValue(CsTeam.Terrorist, out int tTeamId))
        {
            winnerTeamId = tTeamId;
            LogAction($"Team ID {winnerTeamId} (TR) venceu o round.", LogLevel.INFO);
        }
        else
        {
            LogAction("Erro: Não foi possível encontrar o time vencedor do round.", LogLevel.ERROR);
            return HookResult.Handled;
        }

        // Update the match history with the winning team ID
        if (winnerTeamId != -1)
        {
            UpdateMatchHistoryStatus("Ao Vivo", false, winnerTeamId);
        }

        return HookResult.Continue;
    }
    catch (Exception ex)
    {
        LogAction($"Erro em OnRoundEnd: {ex.Message}", LogLevel.ERROR, ex);
        return HookResult.Handled;
    }
}


        private void ScheduleWarmupRestart()
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(15000);
                    SetState(GameState.Warmup);
                    ExecuteWarmupCommands();
                }
                catch (Exception ex)
                {
                    LogAction($"Erro em ScheduleWarmupRestart: {ex.Message}", LogLevel.ERROR, ex);
                }
            });
        }

     private HookResult CommandListener_ChooseCT(CCSPlayerController? player, CommandInfo command)
{
    try
    {
        if (player != null && currentState == GameState.ReadyForSideChoice && knifeRoundWinner.HasValue)
        {
            LogAction($"Jogador {player.PlayerName ?? "Unknown"} está tentando escolher o lado CT.", LogLevel.INFO);

            if (player.Team == knifeRoundWinner.Value)
            {
                if (IsValidTransition(currentState, GameState.Competitive))
                {
                    ApplyCompetitiveConfigAndRestart(CsTeam.CounterTerrorist);
                    SetState(GameState.Competitive);
                }
                else
                {
                    LogInvalidTransition(GameState.Competitive);
                }
                return HookResult.Handled;
            }
            else
            {
                Server.ExecuteCommand($"csay \"{player.PlayerName ?? "Unknown"}, apenas o time vencedor do round de faca pode escolher o lado!\"");
                LogAction($"{player.PlayerName ?? "Unknown"} tentou escolher CT, mas não é do time vencedor.", LogLevel.WARNING);
            }
        }
        else
        {
            Server.ExecuteCommand($"csay \"{player?.PlayerName ?? "Unknown"}, a escolha de lado não é permitida neste momento!\"");
            LogAction($"{player?.PlayerName ?? "Unknown"} tentou escolher CT, mas a escolha de lado não é permitida neste momento.", LogLevel.WARNING);
        }
        return HookResult.Continue;
    }
    catch (Exception ex)
    {
        LogAction($"Erro em CommandListener_ChooseCT: {ex.Message}", LogLevel.ERROR, ex);
        return HookResult.Handled;
    }
}

private HookResult CommandListener_ChooseT(CCSPlayerController? player, CommandInfo command)
{
    try
    {
        if (player != null && currentState == GameState.ReadyForSideChoice && knifeRoundWinner.HasValue)
        {
            LogAction($"Jogador {player.PlayerName ?? "Unknown"} está tentando escolher o lado TR.", LogLevel.INFO);

            if (player.Team == knifeRoundWinner.Value)
            {
                if (IsValidTransition(currentState, GameState.Competitive))
                {
                    ApplyCompetitiveConfigAndRestart(CsTeam.Terrorist);
                    SetState(GameState.Competitive);
                }
                else
                {
                    LogInvalidTransition(GameState.Competitive);
                }
                return HookResult.Handled;
            }
            else
            {
                Server.ExecuteCommand($"csay \"{player.PlayerName ?? "Unknown"}, apenas o time vencedor do round de faca pode escolher o lado!\"");
                LogAction($"{player.PlayerName ?? "Unknown"} tentou escolher TR, mas não é do time vencedor.", LogLevel.WARNING);
            }
        }
        else
        {
            Server.ExecuteCommand($"csay \"{player?.PlayerName ?? "Unknown"}, a escolha de lado não é permitida neste momento!\"");
            LogAction($"{player?.PlayerName ?? "Unknown"} tentou escolher TR, mas a escolha de lado não é permitida neste momento.", LogLevel.WARNING);
        }
        return HookResult.Continue;
    }
    catch (Exception ex)
    {
        LogAction($"Erro em CommandListener_ChooseT: {ex.Message}", LogLevel.ERROR, ex);
        return HookResult.Handled;
    }
}


 private Dictionary<CsTeam, string> teamSides = new Dictionary<CsTeam, string>();

private void ApplyCompetitiveConfigAndRestart(CsTeam chosenTeam)
{
    try
    {
        LogAction("Iniciando transição para o modo competitivo...", LogLevel.INFO);

        Server.NextFrame(async () =>
        {
            try
            {
                // Encerra o aquecimento e aplica configurações
                ExecuteCommandWithConfirmation("mp_warmup_end", "Aquecimento finalizado com sucesso.");
                ExecuteCommandWithConfirmation("mp_weapons_allow_knife 0", "Modo apenas de faca desativado.");
                ExecuteCommandWithConfirmation("mp_weapons_allow_pistols -1", "Todas as pistolas permitidas.");
                ExecuteCommandWithConfirmation("mp_roundtime 1.92", "Tempo de round definido com sucesso.");

                // Inicializa os contadores de rounds e estado de troca de lados
                totalRoundsPlayed = 0;
                sidesSwitched = false;

                // Verifica se os times estão corretamente mapeados
                if (!csTeamToTeamId.ContainsKey(CsTeam.CounterTerrorist) || !csTeamToTeamId.ContainsKey(CsTeam.Terrorist))
                {
                    LogAction("Error: Não foi possível encontrar os times no dicionário csTeamToTeamId.", LogLevel.ERROR);
                    
                    // Recarrega os dados dos times se necessário
                    LoadTeamsData();  

                    // Após recarregar, tente mapear os times novamente
                    if (!csTeamToTeamId.ContainsKey(CsTeam.CounterTerrorist) || !csTeamToTeamId.ContainsKey(CsTeam.Terrorist))
                    {
                        LogAction("Error: Ainda não foi possível encontrar os times no dicionário após recarregar.", LogLevel.ERROR);
                        return;
                    }
                }

                // Movendo jogadores para o time correto
                foreach (var player in connectedPlayers)
                {
                    if (player.IsValid)
                    {
                        ulong steamID64 = SteamIDConverter.ConvertToSteamID64(player.SteamID);
                        if (playerTeams.TryGetValue(steamID64, out string? playerTeamName))
                        {
                            if (playerTeamName == teamSides[chosenTeam])
                            {
                                Server.NextFrame(() => player.ChangeTeam(chosenTeam));
                                LogAction($"Jogador {player.PlayerName ?? "Unknown"} foi movido para {chosenTeam}.", LogLevel.INFO);
                            }
                            else
                            {
                                CsTeam oppositeTeam = (chosenTeam == CsTeam.CounterTerrorist) ? CsTeam.Terrorist : CsTeam.CounterTerrorist;
                                Server.NextFrame(() => player.ChangeTeam(oppositeTeam));
                                LogAction($"Jogador {player.PlayerName ?? "Unknown"} foi movido para {oppositeTeam}.", LogLevel.INFO);
                            }
                        }
                        else
                        {
                            LogAction($"Erro: Não foi possível encontrar o time para o jogador {player.PlayerName ?? "Unknown"}.", LogLevel.ERROR);
                        }
                    }
                }

                LogAction("Processo de mudança de time completo. Preparando para aplicar a configuração competitiva...", LogLevel.INFO);

                // Executa a configuração competitiva
                ExecuteCommandWithConfirmation("exec scrim.cfg", "Configuração competitiva executada com sucesso.");

                // Criar o arquivo de histórico de partida após a execução do scrim.cfg
                CreateNewMatchHistoryFile();

                await Task.Delay(1000);

                // Configurações adicionais para o modo competitivo
                ExecuteCommandWithConfirmation("mp_weapons_allow_knife 0", "Modo apenas de faca desativado.");
                ExecuteCommandWithConfirmation("mp_weapons_allow_pistols -1", "Todas as pistolas permitidas.");
                ExecuteCommandWithConfirmation("mp_roundtime 1.92", "Tempo de round definido com sucesso.");

                // Distribui pistolas iniciais para os jogadores
                foreach (var player in connectedPlayers)
                {
                    if (player.IsValid)
                    {
                        Server.NextFrame(() => player.GiveNamedItem("weapon_glock"));
                    }
                }
            }
            catch (Exception ex)
            {
                LogAction($"Erro durante ApplyCompetitiveConfigAndRestart: {ex.Message}", LogLevel.ERROR, ex);
                RollbackState(GameState.ReadyForSideChoice);
            }
        });
    }
    catch (Exception ex)
    {
        LogAction($"Erro em ApplyCompetitiveConfigAndRestart: {ex.Message}", LogLevel.ERROR, ex);
    }
}



        private async Task AutoMessage()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(1));

                try
                {
                    if (currentState == GameState.Warmup)
                    {
                        LogAction("AutoMessage: Enviando lembrete de prontidão.", LogLevel.DEBUG);

                        Server.NextFrame(() =>
                        {
                            try
                            {
                                Server.ExecuteCommand("csay \"Lembre-se de digitar !ready para marcar prontidão!\"");
                            }
                            catch (Exception ex)
                            {
                                LogAction($"Erro ao enviar mensagem automática: {ex.Message}", LogLevel.ERROR, ex);
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogAction($"Erro no loop AutoMessage: {ex.Message}", LogLevel.ERROR, ex);
                }
            }
        }

        private string GetNotReadyPlayersMessage()
        {
            try
            {
                LogAction("Gerando mensagem para jogadores não prontos.", LogLevel.DEBUG);

                var notReadyPlayers = playerReadyStatusCache
                    .Where(p => !p.Value)
                    .Select(p => "{red}" + (connectedPlayers.FirstOrDefault(pc => pc.SteamID == p.Key)?.PlayerName ?? "Unknown") + "{default}")
                    .ToList();

                if (notReadyPlayers.Count == 0)
                {
                    return "{green}[Rapid Fire Plugin]{default}: {lightgreen}Todos os jogadores estão prontos!{default}";
                }

                return "{green}[Rapid Fire Plugin]{default}: Jogadores que ainda não estão prontos: " + string.Join(", ", notReadyPlayers);
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao gerar mensagem de jogadores não prontos: {ex.Message}", LogLevel.ERROR, ex);
                return "{red}Erro ao gerar mensagem de jogadores não prontos{default}";
            }
        }

private void UpdatePlayerStats(ulong steamId, Action<PlayerStats> updateAction)
{
    lock (statsLock)
    {
        if (!playerStats.TryGetValue(steamId, out PlayerStats? stats))
        {
            string playerName = connectedPlayers.FirstOrDefault(p => p.SteamID == steamId)?.PlayerName ?? "Unknown";
            stats = new PlayerStats(steamId, playerName);
            playerStats.TryAdd(steamId, stats);
        }

        // Aplica a atualização desejada nas estatísticas
        updateAction(stats);

        // Atualiza o arquivo da partida atual (match stats)
        UpdateMatchStatsWithPlayerStats(steamId, stats);
    }
}

private void UpdateMatchStatsWithPlayerStats(ulong steamId, PlayerStats stats)
{
    if (currentMatchFilePath == null || !File.Exists(currentMatchFilePath))
    {
        LogAction("Erro: Arquivo de histórico de partida não encontrado ou não foi criado.", LogLevel.ERROR);
        return;
    }

    try
    {
        string json = File.ReadAllText(currentMatchFilePath);
        var matchData = JsonSerializer.Deserialize<MatchHistory>(json);

        if (matchData != null)
        {
            if (matchData.PlayerStatsPerMatch.ContainsKey(steamId.ToString())) // Conversão para string
            {
                matchData.PlayerStatsPerMatch[steamId.ToString()] = stats;
            }

            else
            {
                matchData.PlayerStatsPerMatch.Add(steamId.ToString(), stats);
            }

            string updatedJson = JsonSerializer.Serialize(matchData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(currentMatchFilePath, updatedJson);

            LogAction($"Estatísticas do jogador {stats.PlayerName} atualizadas no arquivo de partida {currentMatchFilePath}.", LogLevel.INFO);
        }
    }
    catch (Exception ex)
    {
        LogAction($"Erro ao atualizar estatísticas do jogador na partida: {ex.Message}", LogLevel.ERROR, ex);
    }
}




        private void SavePlayerStatsSafely()
        {
            lock (statsLock)
            {
                SavePlayerStats();
            }
        }

            private void ExecuteCommandWithConfirmation(string command, string successMessage)
            {
                Server.NextFrame(() =>
                {
                    try
                    {
                        Server.ExecuteCommand(command);
                        LogAction(successMessage, LogLevel.INFO);
                    }
                    catch (Exception ex)
                    {
                        LogAction($"Erro ao executar comando '{command}': {ex.Message}", LogLevel.ERROR, ex);
                    }
                });
            }

        private bool IsAdmin(CCSPlayerController player)
        {
            try
            {
                LogAction($"Verificando se o jogador {player.PlayerName ?? "Unknown"} é administrador.", LogLevel.DEBUG);
                return adminRoles.ContainsKey(player.SteamID) && adminRoles[player.SteamID] == AdminRole.SuperAdmin;
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao verificar status de administrador para o jogador {player.PlayerName ?? "Unknown"}: {ex.Message}", LogLevel.ERROR, ex);
                return false;
            }
        }

        private void LogAdminAction(CCSPlayerController player, string action)
        {
            try
            {
                string logMessage = $"{DateTime.Now} - Ação de administrador por {player.PlayerName ?? "Unknown"} (SteamID: {player.SteamID}): {action}";
                Console.WriteLine(logMessage);
                LogAction(logMessage, LogLevel.INFO);
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao registrar ação de administrador para o jogador {player.PlayerName ?? "Unknown"}: {ex.Message}", LogLevel.ERROR, ex);
            }
        }

       private bool IsValidTransition(GameState currentState, GameState nextState)
{
    try
    {
        switch (currentState)
        {
            case GameState.Warmup:
                return nextState == GameState.KnifeRound;
            case GameState.KnifeRound:
                return nextState == GameState.ReadyForSideChoice;
            case GameState.ReadyForSideChoice:
                return nextState == GameState.Competitive;
            case GameState.Competitive:
                return nextState == GameState.Warmup || nextState == GameState.KnifeRound;
            default:
                return false;
        }
    }
    catch (Exception ex)
    {
        LogAction($"Erro em IsValidTransition: {ex.Message}", LogLevel.ERROR, ex);
        return false;
    }
}


        private void LogInvalidTransition(GameState attemptedState)
        {
            try
            {
                LogAction($"Tentativa de transição de estado inválida para {attemptedState}.", LogLevel.ERROR);
                Server.ExecuteCommand($"say [ERRO]: Tentativa de transição de estado inválida para {attemptedState}. Por favor, verifique a configuração do plugin.");
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao registrar transição inválida para {attemptedState}: {ex.Message}", LogLevel.ERROR, ex);
            }
        }

       private void SavePlayerStats()
{
    lock (statsLock)
    {
        try
        {
            string? directoryPath = Path.GetDirectoryName(playerStatsFilePath);

            if (!string.IsNullOrEmpty(directoryPath))
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                    LogAction($"Diretório {directoryPath} criado para salvar as estatísticas dos jogadores.", LogLevel.INFO);
                }
            }
            else
            {
                LogAction("O caminho do diretório está vazio ou nulo, não é possível criar o diretório.", LogLevel.ERROR);
                return;
            }

            Dictionary<ulong, PlayerStats> existingStats = new Dictionary<ulong, PlayerStats>();

            if (File.Exists(playerStatsFilePath))
            {
                try
                {
                    string existingJson = File.ReadAllText(playerStatsFilePath);
                    existingStats = JsonSerializer.Deserialize<Dictionary<ulong, PlayerStats>>(existingJson) ?? new Dictionary<ulong, PlayerStats>();
                }
                catch (Exception ex)
                {
                    LogAction($"Erro ao ler as estatísticas existentes do arquivo: {ex.Message}", LogLevel.ERROR, ex);
                }
            }

            foreach (var playerStat in playerStats)
            {
                if (existingStats.ContainsKey(playerStat.Key))
                {
                    existingStats[playerStat.Key].Kills += playerStat.Value.Kills;
                    existingStats[playerStat.Key].Deaths += playerStat.Value.Deaths;
                    existingStats[playerStat.Key].Assists += playerStat.Value.Assists;
                    existingStats[playerStat.Key].Headshots += playerStat.Value.Headshots;
                    existingStats[playerStat.Key].MVPs += playerStat.Value.MVPs;
                    existingStats[playerStat.Key].Score += playerStat.Value.Score;
                }
                else
                {
                    existingStats[playerStat.Key] = playerStat.Value;
                }
            }

            try
            {
                string json = JsonSerializer.Serialize(existingStats, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(playerStatsFilePath, json);
                LogAction($"Estatísticas dos jogadores salvas em {playerStatsFilePath}", LogLevel.INFO);
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao salvar as estatísticas dos jogadores: {ex.Message}", LogLevel.ERROR, ex);
            }
        }
        catch (Exception ex)
        {
            LogAction($"Erro ao salvar as estatísticas dos jogadores: {ex.Message}", LogLevel.ERROR, ex);
        }
    }
}


        private void LogAction(string action, LogLevel level = LogLevel.INFO, Exception? ex = null)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logMessage = $"[{timestamp}] [Rapid Fire Plugin] [{level}]: {action}";

                if (ex != null)
                {
                    logMessage += $"\nExceção: {ex.Message}\nStackTrace: {ex.StackTrace}";
                }

                Console.WriteLine(logMessage);
            }
            catch (Exception ex2)
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Rapid Fire Plugin] [ERRO]: Falha ao registrar ação. Erro original: {ex?.Message}. Erro ao registrar: {ex2.Message}");
            }
        }

        private void SetState(GameState newState)
        {
            try
            {
                LogAction($"Transição de estado: {currentState} -> {newState}", LogLevel.INFO);
                currentState = newState;
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao definir novo estado: {ex.Message}", LogLevel.ERROR, ex);
            }
        }

        private void RollbackState(GameState previousState)
        {
            try
            {
                LogAction($"Rollback de estado: {currentState} -> {previousState}", LogLevel.WARNING);
                currentState = previousState;
            }
            catch (Exception ex)
            {
                LogAction($"Erro ao realizar rollback de estado: {ex.Message}", LogLevel.ERROR, ex);
            }
        }

    private void CreateNewMatchHistoryFile()
{
    try
    {
        // Garantir que o diretório existe
        if (!Directory.Exists(matchHistoryDirectoryPath))
        {
            Directory.CreateDirectory(matchHistoryDirectoryPath);
            LogAction($"Diretório de histórico de partidas criado: {matchHistoryDirectoryPath}", LogLevel.INFO);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        currentMatchFilePath = Path.Combine(matchHistoryDirectoryPath, $"match_{timestamp}.json");

        // Gerar um matchId único
        string matchId = Guid.NewGuid().ToString();

        // Verifica se há pelo menos dois times distintos
        var distinctTeams = playerTeams.Values.Distinct().ToList();
        if (distinctTeams.Count < 2)
        {
            LogAction("Erro: Não há times suficientes para iniciar a partida.", LogLevel.ERROR);
            return;
        }

        string team1Name = distinctTeams.ElementAtOrDefault(0) ?? "Team 1";
        string team2Name = distinctTeams.ElementAtOrDefault(1) ?? "Team 2";

        // Mapeia os nomes dos times para IDs
        teamNameToTeamId[team1Name] = 1;
        teamNameToTeamId[team2Name] = 2;

        // Filtra os SteamIDs dos jogadores para cada time
        var team1SteamIDs = playerTeams.Where(pt => pt.Value == team1Name).Select(pt => pt.Key).ToList();
        var team2SteamIDs = playerTeams.Where(pt => pt.Value == team2Name).Select(pt => pt.Key).ToList();

        var matchData = new MatchHistory
        {
            Date = DateTime.Now,
            Team1 = team1Name,
            Team2 = team2Name,
            Team1Id = 1,
            Team2Id = 2,
            Status = "Ao Vivo",
            Map = Server.MapName,
            PlayerSteamIDs = connectedPlayers.Select(p => SteamIDConverter.ConvertToSteamID64(p.SteamID).ToString()).ToList(),
            PlayerNicknames = connectedPlayers.ToDictionary(p => SteamIDConverter.ConvertToSteamID64(p.SteamID).ToString(), p => p.PlayerName ?? "Unknown"),
            MatchId = matchId,
            PlayerSteamIDsTeam1 = playerTeams.Where(pt => pt.Value == team1Name).Select(pt => pt.Key.ToString()).ToList(),
            PlayerSteamIDsTeam2 = playerTeams.Where(pt => pt.Value == team2Name).Select(pt => pt.Key.ToString()).ToList(),
            PlayerStatsPerMatch = playerStats.ToDictionary(ps => ps.Key.ToString(), ps => ps.Value)
        };

        // Salva o histórico da partida no arquivo JSON
        string json = JsonSerializer.Serialize(matchData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(currentMatchFilePath, json);

        LogAction($"Novo arquivo de histórico de partida criado: {currentMatchFilePath} com MatchId: {matchId}", LogLevel.INFO);
    }
    catch (Exception ex)
    {
        LogAction($"Erro ao criar arquivo de histórico de partida: {ex.Message}", LogLevel.ERROR, ex);
    }
}


        private RoundDetail? GetCurrentRoundDetail()
        {
            if (currentMatchFilePath == null || !File.Exists(currentMatchFilePath))
            {
                LogAction("Erro: Arquivo de histórico de partida não encontrado ou não foi criado.", LogLevel.ERROR);
                return null;
            }

            string json = File.ReadAllText(currentMatchFilePath);
            var matchData = JsonSerializer.Deserialize<MatchHistory>(json);

            return matchData?.RoundDetails.LastOrDefault();
        }

private void UpdateMatchHistoryStatus(string status, bool isOvertime = false, int winnerTeamId = -1)
{
    try
    {
        if (currentMatchFilePath == null || !File.Exists(currentMatchFilePath))
        {
            LogAction("Erro: Arquivo de histórico de partida não encontrado ou não foi criado.", LogLevel.ERROR);
            return;
        }

        string json = File.ReadAllText(currentMatchFilePath);
        var matchData = JsonSerializer.Deserialize<MatchHistory>(json);

        if (matchData != null)
        {
            LogAction($"Atualizando pontuação. Team ID vencedor: {winnerTeamId}", LogLevel.DEBUG);

            // Atualiza a pontuação com base no ID do time vencedor
            if (winnerTeamId == matchData.Team1Id)
            {
                matchData.Team1Score++;  // Incrementa a pontuação do Team 1
            }
            else if (winnerTeamId == matchData.Team2Id)
            {
                matchData.Team2Score++;  // Incrementa a pontuação do Team 2
            }
            else
            {
                LogAction($"Erro: Team ID '{winnerTeamId}' não corresponde ao Team1Id ou Team2Id.", LogLevel.ERROR);
            }

            matchData.Status = status;

            // Atualiza os detalhes do round atual
            var currentRound = matchData.RoundDetails.LastOrDefault();

            if (currentRound != null)
            {
                // Atualiza o round com a informação do time vencedor
                currentRound.WinnerTeamId = winnerTeamId;
                currentRound.ScoreTeam1 = matchData.Team1Score;
                currentRound.ScoreTeam2 = matchData.Team2Score;
                currentRound.WasOvertime = isOvertime;
            }
            else
            {
                LogAction("Erro: Não foi possível encontrar o round atual no histórico.", LogLevel.ERROR);
            }

            // Salva o arquivo de histórico atualizado
            string updatedJson = JsonSerializer.Serialize(matchData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(currentMatchFilePath, updatedJson);

            LogAction($"Pontuação atualizada: {matchData.Team1} ({matchData.Team1Score}) - {matchData.Team2} ({matchData.Team2Score})", LogLevel.DEBUG);
            LogAction("Histórico da partida atualizado.", LogLevel.INFO);
        }
    }
    catch (Exception ex)
    {
        LogAction($"Erro ao atualizar o status do histórico da partida: {ex.Message}", LogLevel.ERROR, ex);
    }
}



        public enum GameState
        {
            Warmup,
            KnifeRound,
            ReadyForSideChoice,
            Competitive,
            PostGame
        }

        private enum AdminRole
        {
            SuperAdmin,
            Moderator,
            Viewer
        }

        private enum LogLevel
        {
            DEBUG,
            INFO,
            WARNING,
            ERROR
        }
    }

public class Player
{
    [JsonPropertyName("steamId")]
    public string SteamId { get; set; }

    [JsonPropertyName("photo")]
    public string Photo { get; set; }

    public Player()
    {
        SteamId = string.Empty;
        Photo = string.Empty;
    }
}



public class PlayerStats
{
    public ulong SteamID { get; set; }
    public string PlayerName { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public int Headshots { get; set; }
    public int MVPs { get; set; }
    public int Score { get; set; }
    public string Weapon { get; set; }
    public int DamageGiven { get; set; }       // Dano causado
    public int DamageTaken { get; set; }       // Dano recebido
    public int ShotsFired { get; set; }        // Total de tiros disparados
    public int ShotsHit { get; set; }          // Total de tiros que acertaram o alvo
    public int EnemiesFlashed { get; set; }    // Número de inimigos cegados
    public int TimeAlive { get; set; }         // Tempo de vida em cada round (em segundos)
    public int ObjectiveScore { get; set; }    // Pontuação relacionada a objetivos

    public PlayerStats(ulong steamID, string playerName)
    {
        SteamID = steamID;
        PlayerName = playerName ?? throw new ArgumentNullException(nameof(playerName));
        Kills = 0;
        Deaths = 0;
        Assists = 0;
        Headshots = 0;
        MVPs = 0;
        Score = 0;
        Weapon = string.Empty;
        DamageGiven = 0;
        DamageTaken = 0;
        ShotsFired = 0;
        ShotsHit = 0;
        EnemiesFlashed = 0;
        TimeAlive = 0;
        ObjectiveScore = 0;
    }
}

public class Team
{
    [JsonPropertyName("teamName")]
    public string TeamName { get; set; }

    [JsonPropertyName("players")]
    public List<Player> Players { get; set; }

    public Team()
    {
        TeamName = string.Empty;
        Players = new List<Player>();
    }
}


public class MatchHistory
{
    public string? MatchId { get; set; }
    public DateTime Date { get; set; }
    public string? Team1 { get; set; }
    public string? Team2 { get; set; }
    public int Team1Id { get; set; } = 1;
    public int Team2Id { get; set; } = 2;
    public string? Status { get; set; }
    public int Team1Score { get; set; }
    public int Team2Score { get; set; }
    public string? Map { get; set; }

    // Alterando para List<string>
    public List<string> PlayerSteamIDs { get; set; } = new List<string>();
    public List<RoundDetail> RoundDetails { get; set; } = new List<RoundDetail>();
    public Dictionary<string, string> PlayerNicknames { get; set; } = new Dictionary<string, string>();

    public List<string> PlayerSteamIDsTeam1 { get; set; } = new List<string>();
    public List<string> PlayerSteamIDsTeam2 { get; set; } = new List<string>();

    public Dictionary<string, PlayerStats> PlayerStatsPerMatch { get; set; } = new Dictionary<string, PlayerStats>();
}

public class RoundDetail
{
    public int RoundNumber { get; set; }
    public bool IsRoundStart { get; set; } // Indica se é o início do round
    public int WinnerTeamId { get; set; } // ID do time vencedor
    public int Team1Kills { get; set; }
    public int Team2Kills { get; set; }
    public bool WasOvertime { get; set; }
    public int ScoreTeam1 { get; set; } // Pontuação do Team1 após este round
    public int ScoreTeam2 { get; set; } // Pontuação do Team2 após este round
    public Dictionary<ulong, int> PlayerKills { get; set; } = new Dictionary<ulong, int>();
    public Dictionary<ulong, string> PlayerNicknames { get; set; } = new Dictionary<ulong, string>();
    public Dictionary<ulong, List<string>> PlayerKillsDetails { get; set; } = new Dictionary<ulong, List<string>>();
}
}


    public static class SteamIDConverter
    {
        public static ulong ConvertToSteamID64(ulong steamID)
        {
            if (steamID > 76561197960265728UL)
            {
                return steamID;
            }

            return 76561197960265728UL + steamID;
        }
    }