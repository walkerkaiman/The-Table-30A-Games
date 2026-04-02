(function () {
  "use strict";

  // ─── Shared state ─────────────────────────
  var state = {
    playerId: null,
    playerName: "",
    serverHost: "",
    roomCode: "",
    currentScreen: "join",
    currentGameType: null,
    timerInterval: null,
    timerValue: 0,
    reconnectAttempts: 0,
    discoveryInterval: null,
    isRejoining: false,
    gameSelectVote: null,
    isHost: false,
  };

  var MAX_RECONNECT_ATTEMPTS = 3;
  var RECONNECT_DELAYS = [1000, 2000, 4000];
  var DISCOVERY_POLL_MS = 3000;

  var ws = null;
  var gameModules = {};
  var activeModule = null;

  // ─── DOM ──────────────────────────────────
  var $ = function (sel) { return document.querySelector(sel); };
  var screens = document.querySelectorAll(".screen");

  var els = {
    inputName: $("#input-name"),
    gameList: $("#game-list"),
    gameListStatus: $("#game-list-status"),
    joinError: $("#join-error"),
    lobbyPlayers: $("#lobby-players"),
    lobbyStatus: $("#lobby-status"),
    hostLobbyControls: $("#host-lobby-controls"),
    btnStartGame: $("#btn-start-game"),
    gameselectTimer: $("#gameselect-timer"),
    gameselectList: $("#gameselect-list"),
    hostGameselectControls: $("#host-gameselect-controls"),
    btnSkipVote: $("#btn-skip-vote"),
    btnReconnect: $("#btn-reconnect"),
  };

  // ─── Public API for game modules ──────────
  window.GameApp = {
    state: state,

    registerGameModule: function (gameType, module) {
      gameModules[gameType] = module;
    },

    sendMessage: function (obj) {
      if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify(obj));
      }
    },

    showScreen: function (name) {
      state.currentScreen = name;
      screens.forEach(function (s) {
        s.classList.toggle("active", s.id === "screen-" + name);
      });
    },

    startTimer: function (seconds, timerEl) {
      stopTimer();
      state.timerValue = Math.ceil(seconds);
      updateTimerDisplay(timerEl);
      state.timerInterval = setInterval(function () {
        state.timerValue--;
        if (state.timerValue < 0) state.timerValue = 0;
        updateTimerDisplay(timerEl);
      }, 1000);
    },

    stopTimer: stopTimer,

    escapeHtml: function (str) {
      var div = document.createElement("div");
      div.textContent = str;
      return div.innerHTML;
    },

    $: $,
  };

  // ─── Timer ────────────────────────────────
  function stopTimer() {
    if (state.timerInterval) {
      clearInterval(state.timerInterval);
      state.timerInterval = null;
    }
  }

  function updateTimerDisplay(el) {
    if (!el) return;
    el.textContent = state.timerValue + "s";
    el.classList.toggle("urgent", state.timerValue <= 10);
  }

  // ─── Screen helper ────────────────────────
  function showScreen(name) {
    GameApp.showScreen(name);
    stopTimer();
  }

  // ─── Session Persistence ──────────────────
  function saveSession() {
    try {
      localStorage.setItem("partyGameSession", JSON.stringify({
        playerId: state.playerId,
        playerName: state.playerName,
        serverHost: state.serverHost,
      }));
    } catch (e) {}
  }

  function loadSession() {
    try {
      var raw = localStorage.getItem("partyGameSession");
      if (!raw) return null;
      var data = JSON.parse(raw);
      if (data && data.playerId && data.playerName && data.serverHost) return data;
    } catch (e) {}
    return null;
  }

  function clearSession() {
    try { localStorage.removeItem("partyGameSession"); } catch (e) {}
  }

  // ─── Discovery ────────────────────────────
  function startDiscovery() {
    stopDiscovery();
    fetchGames();
    state.discoveryInterval = setInterval(fetchGames, DISCOVERY_POLL_MS);
  }

  function stopDiscovery() {
    if (state.discoveryInterval) {
      clearInterval(state.discoveryInterval);
      state.discoveryInterval = null;
    }
  }

  function fetchGames() {
    fetch("/api/games")
      .then(function (r) { return r.json(); })
      .then(function (data) { renderGameList(data.games || []); })
      .catch(function () { renderGameList([]); });
  }

  function renderGameList(games) {
    els.gameList.innerHTML = "";
    if (games.length === 0) {
      els.gameListStatus.textContent = "Searching for games...";
      els.gameListStatus.style.display = "";
      return;
    }
    els.gameListStatus.style.display = "none";
    games.forEach(function (g) {
      var card = document.createElement("div");
      card.className = "game-card";
      var stateLabel = g.state === "Lobby" ? "Lobby" : "In Progress";
      var stateClass = g.state === "Lobby" ? "" : " in-progress";
      var playerLabel = g.playerCount === 1 ? "1 player" : g.playerCount + " players";
      card.innerHTML =
        '<div class="game-card-name">' + GameApp.escapeHtml(g.gameName) + "</div>" +
        '<div class="game-card-info">' +
          '<span class="game-card-players">' + playerLabel + "</span>" +
          '<span class="game-card-state' + stateClass + '">' + stateLabel + "</span>" +
        "</div>";
      card.addEventListener("click", function () { joinGame(g.ip + ":" + g.port, g.roomCode); });
      els.gameList.appendChild(card);
    });
  }

  // ─── Connection ───────────────────────────
  function joinGame(host, roomCode) {
    var name = els.inputName.value.trim();
    if (!name) { showJoinError("Please enter your name."); return; }
    state.playerName = name;
    state.serverHost = host;
    state.roomCode = roomCode;
    state.isRejoining = false;
    state.reconnectAttempts = 0;
    els.joinError.textContent = "";
    stopDiscovery();
    connectWs(host, function () {
      GameApp.sendMessage({ type: "join", name: state.playerName, roomCode: state.roomCode });
    });
  }

  function attemptRejoin(session) {
    state.playerId = session.playerId;
    state.playerName = session.playerName;
    state.serverHost = session.serverHost;
    state.isRejoining = true;
    state.reconnectAttempts = 0;
    showScreen("reconnecting");
    connectWs(session.serverHost, function () {
      GameApp.sendMessage({ type: "rejoin", playerId: state.playerId, name: state.playerName });
    });
  }

  function connectWs(host, onOpenCallback) {
    closeWs();
    ws = new WebSocket("ws://" + host);
    ws.onopen = function () {
      state.reconnectAttempts = 0;
      if (onOpenCallback) onOpenCallback();
    };
    ws.onmessage = function (event) {
      var msg;
      try { msg = JSON.parse(event.data); } catch (e) { return; }
      handleMessage(msg);
    };
    ws.onclose = function () { handleDisconnect(); };
    ws.onerror = function () {
      if (state.currentScreen === "join") showJoinError("Could not connect to server.");
    };
  }

  function closeWs() {
    if (ws) {
      ws.onclose = null; ws.onerror = null; ws.onmessage = null; ws.onopen = null;
      try { ws.close(); } catch (e) {}
      ws = null;
    }
  }

  function handleDisconnect() {
    if (!state.playerId) {
      if (state.isRejoining) { clearSession(); showJoinScreen(); }
      return;
    }
    if (state.reconnectAttempts < MAX_RECONNECT_ATTEMPTS) {
      var delay = RECONNECT_DELAYS[state.reconnectAttempts] || 4000;
      state.reconnectAttempts++;
      setTimeout(function () {
        connectWs(state.serverHost, function () {
          GameApp.sendMessage({ type: "rejoin", playerId: state.playerId, name: state.playerName });
        });
      }, delay);
    } else {
      showScreen("disconnected");
    }
  }

  // ─── Message Router ───────────────────────
  function handleMessage(msg) {
    switch (msg.type) {
      case "welcome":
        state.playerId = msg.playerId;
        state.roomCode = msg.roomCode;
        state.isRejoining = false;
        saveSession();
        showScreen("lobby");
        break;

      case "rejoin_success":
        state.playerId = msg.playerId;
        state.isRejoining = false;
        state.reconnectAttempts = 0;
        saveSession();
        break;

      case "error":
        if (state.isRejoining) { clearSession(); showJoinScreen(); }
        else showJoinError(msg.message);
        break;

      case "player_list":
        if (msg.hostId) {
          PlayerManager_cache_hostId = msg.hostId;
          state.isHost = (msg.hostId === state.playerId);
        }
        renderPlayerList(msg.players);
        updateHostControls();
        break;

      case "game_state":
        handleGameState(msg);
        break;

      case "vote_update":
        handleVoteUpdate(msg);
        break;

      default:
        if (activeModule && activeModule.handleMessage) {
          activeModule.handleMessage(msg);
        }
        break;
    }
  }

  // ─── Game State Routing ───────────────────
  function handleGameState(msg) {
    var newGameType = msg.gameType || "lobby";

    if (newGameType !== state.currentGameType) {
      if (activeModule && activeModule.cleanup) activeModule.cleanup();
      activeModule = null;
      state.currentGameType = newGameType;
    }

    switch (newGameType) {
      case "lobby":
        handleLobbyState(msg);
        break;

      case "game_select":
        handleGameSelectState(msg);
        break;

      default:
        if (gameModules[newGameType]) {
          activeModule = gameModules[newGameType];
          if (activeModule.handleGameState) activeModule.handleGameState(msg);
        }
        break;
    }
  }

  // ─── Lobby ────────────────────────────────
  function handleLobbyState(msg) {
    showScreen("lobby");
    if (msg.players) renderPlayerList(msg.players);
    updateHostControls();
  }

  function renderPlayerList(players) {
    if (!players || !els.lobbyPlayers) return;
    els.lobbyPlayers.innerHTML = "";
    var hostId = PlayerManager_cache_hostId;
    players.forEach(function (p) {
      var chip = document.createElement("span");
      chip.className = "player-chip" + (p.id === hostId ? " host" : "");
      chip.textContent = p.name + (p.id === hostId ? " \u2605" : "");
      els.lobbyPlayers.appendChild(chip);
    });
  }

  // ─── Game Selection ───────────────────────
  function handleGameSelectState(msg) {
    showScreen("gameselect");
    state.gameSelectVote = null;

    if (msg.timer > 0) {
      GameApp.startTimer(msg.timer, els.gameselectTimer);
    }

    renderGameSelectList(msg.games || [], msg.voteCounts || []);
    updateHostControls();
  }

  function renderGameSelectList(games, voteCounts) {
    var voteMap = {};
    voteCounts.forEach(function (v) { voteMap[v.gameId] = v.count; });
    var playerCount = 0;
    if (PlayerManager_cache) playerCount = PlayerManager_cache.length;

    els.gameselectList.innerHTML = "";
    games.forEach(function (g) {
      var votes = voteMap[g.id] || 0;
      var tooFewPlayers = playerCount > 0 && playerCount < g.minPlayers;
      var isSelected = state.gameSelectVote === g.id;

      var card = document.createElement("div");
      card.className = "gameselect-card" + (tooFewPlayers ? " disabled" : "") + (isSelected ? " selected" : "");
      card.innerHTML =
        '<div class="gameselect-card-header">' +
          '<span class="gameselect-card-name">' + GameApp.escapeHtml(g.name) + '</span>' +
          '<span class="gameselect-vote-badge">' + votes + '</span>' +
        '</div>' +
        '<div class="gameselect-card-desc">' + GameApp.escapeHtml(g.description) + '</div>' +
        '<div class="gameselect-card-meta">' + g.minPlayers + '-' + g.maxPlayers + ' players</div>';

      if (!tooFewPlayers) {
        card.addEventListener("click", function () {
          state.gameSelectVote = g.id;
          GameApp.sendMessage({ type: "game_vote", gameId: g.id });
          renderGameSelectList(games, voteCounts);
        });
      }

      els.gameselectList.appendChild(card);
    });
  }

  function handleVoteUpdate(msg) {
    if (state.currentScreen !== "gameselect") return;
    var cards = els.gameselectList.querySelectorAll(".gameselect-card");
    var votes = msg.votes || [];
    var voteMap = {};
    votes.forEach(function (v) { voteMap[v.gameId] = v.count; });

    cards.forEach(function (card) {
      var badge = card.querySelector(".gameselect-vote-badge");
      var name = card.querySelector(".gameselect-card-name");
      if (badge && name) {
        for (var gid in voteMap) {
          if (card.innerHTML.indexOf(gid) !== -1) {
            // Match by stored reference is fragile — update all badges
          }
        }
      }
    });

    // Simpler: just re-render if we have the games data cached
    // The next game_state will update fully; vote_update just increments badges
    var badges = els.gameselectList.querySelectorAll(".gameselect-vote-badge");
    votes.forEach(function (v, i) {
      if (badges[i]) badges[i].textContent = v.count;
    });
  }

  var PlayerManager_cache = [];
  var PlayerManager_cache_hostId = "";

  function updatePlayerCache(players, hostId) {
    if (players) PlayerManager_cache = players;
    if (hostId !== undefined) PlayerManager_cache_hostId = hostId;
  }

  // Patch renderPlayerList to also cache
  var _origRenderPlayerList = renderPlayerList;
  renderPlayerList = function (players) {
    updatePlayerCache(players);
    _origRenderPlayerList(players);
  };

  // ─── Host Controls ─────────────────────────
  function updateHostControls() {
    if (els.hostLobbyControls) {
      els.hostLobbyControls.style.display = state.isHost ? "" : "none";
    }
    if (els.lobbyStatus) {
      els.lobbyStatus.textContent = state.isHost
        ? "You are the host"
        : "Waiting for the host to start...";
    }
    if (els.hostGameselectControls) {
      els.hostGameselectControls.style.display = state.isHost ? "" : "none";
    }
  }

  // ─── Helpers ──────────────────────────────
  function showJoinError(msg) {
    els.joinError.textContent = msg;
  }

  function showJoinScreen() {
    showScreen("join");
    startDiscovery();
  }

  // ─── Event Listeners ─────────────────────
  if (els.btnStartGame) {
    els.btnStartGame.addEventListener("click", function () {
      GameApp.sendMessage({ type: "start_game" });
    });
  }

  if (els.btnSkipVote) {
    els.btnSkipVote.addEventListener("click", function () {
      GameApp.sendMessage({ type: "start_game" });
    });
  }

  els.btnReconnect.addEventListener("click", function () {
    state.reconnectAttempts = 0;
    clearSession();
    state.playerId = null;
    showJoinScreen();
  });

  els.inputName.addEventListener("keydown", function (e) {
    if (e.key === "Enter") e.preventDefault();
  });

  // ─── Init ─────────────────────────────────
  (function init() {
    var session = loadSession();
    if (session) attemptRejoin(session);
    else showJoinScreen();
  })();
})();
