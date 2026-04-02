(function () {
  "use strict";

  var $ = GameApp.$;
  var state = GameApp.state;

  var paddlePosition = 0.5;
  var sendInterval = null;
  var myLives = 0;
  var eliminated = false;

  var els = {
    countdownTimer: $("#pong-countdown-timer"),
    lives: $("#pong-lives"),
    status: $("#pong-status"),
    touchArea: $("#pong-touch-area"),
    paddleIndicator: $("#pong-paddle-indicator"),
    winnerText: $("#pong-winner-text"),
    standings: $("#pong-standings"),
  };

  // ─── Touch Input ──────────────────────────
  function setupTouch() {
    if (!els.touchArea) return;

    els.touchArea.addEventListener("touchstart", onTouch, { passive: false });
    els.touchArea.addEventListener("touchmove", onTouch, { passive: false });
    els.touchArea.addEventListener("mousedown", onMouse);
    els.touchArea.addEventListener("mousemove", onMouse);
  }

  function onTouch(e) {
    e.preventDefault();
    var touch = e.touches[0];
    if (!touch) return;
    var rect = els.touchArea.getBoundingClientRect();
    paddlePosition = Math.max(0, Math.min(1, (touch.clientX - rect.left) / rect.width));
    updatePaddleIndicator();
  }

  function onMouse(e) {
    if (e.buttons === 0 && e.type === "mousemove") return;
    var rect = els.touchArea.getBoundingClientRect();
    paddlePosition = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
    updatePaddleIndicator();
  }

  function updatePaddleIndicator() {
    if (!els.paddleIndicator) return;
    els.paddleIndicator.style.left = (paddlePosition * 100) + "%";
  }

  function startSending() {
    stopSending();
    sendInterval = setInterval(function () {
      GameApp.sendMessage({ type: "paddle_move", position: paddlePosition });
    }, 33);
  }

  function stopSending() {
    if (sendInterval) {
      clearInterval(sendInterval);
      sendInterval = null;
    }
  }

  // ─── Lives Display ────────────────────────
  function renderLives(count) {
    if (!els.lives) return;
    var hearts = "";
    for (var i = 0; i < count; i++) hearts += "\u2764 ";
    els.lives.textContent = hearts.trim();
    els.lives.classList.toggle("low", count <= 1);
  }

  // ─── Module ───────────────────────────────
  GameApp.registerGameModule("pong", {
    handleGameState: function (msg) {
      switch (msg.state) {
        case "Countdown":
          eliminated = false;
          paddlePosition = 0.5;
          updatePaddleIndicator();
          GameApp.showScreen("pong-countdown");
          if (msg.timer > 0) {
            GameApp.startTimer(msg.timer, els.countdownTimer);
          }
          break;

        case "Playing":
          if (eliminated) {
            GameApp.showScreen("pong-eliminated");
          } else {
            GameApp.showScreen("pong-playing");
            setupTouch();
            startSending();
          }
          break;

        case "GoalScored":
          // Stay on current screen, will get a new Playing state soon
          break;

        case "GameOver":
          stopSending();
          GameApp.showScreen("pong-gameover");
          renderStandings(msg.players || []);
          break;
      }
    },

    handleMessage: function (msg) {
      switch (msg.type) {
        case "pong_frame":
          handleFrame(msg);
          break;

        case "pong_event":
          handlePongEvent(msg);
          break;
      }
    },

    cleanup: function () {
      stopSending();
      eliminated = false;
      paddlePosition = 0.5;
      GameApp.stopTimer();
    },
  });

  // ─── Frame Updates ────────────────────────
  function handleFrame(msg) {
    if (!msg.paddles) return;
    for (var i = 0; i < msg.paddles.length; i++) {
      var p = msg.paddles[i];
      if (p.id === state.playerId) {
        if (p.lives !== myLives) {
          myLives = p.lives;
          renderLives(myLives);
        }
      }
    }
  }

  // ─── Events ───────────────────────────────
  function handlePongEvent(msg) {
    switch (msg.eventName) {
      case "goal":
        if (msg.scoredOn === state.playerId) {
          if (els.status) els.status.textContent = "You lost a life!";
          vibrate();
          setTimeout(function () {
            if (els.status) els.status.textContent = "Drag to move paddle";
          }, 1500);
        }
        break;

      case "eliminated":
        if (msg.playerId === state.playerId) {
          eliminated = true;
          stopSending();
          GameApp.showScreen("pong-eliminated");
          vibrate();
        }
        break;

      case "winner":
        stopSending();
        if (msg.playerId === state.playerId) {
          if (els.winnerText) els.winnerText.textContent = "You Win!";
        } else {
          if (els.winnerText) els.winnerText.textContent = "Game Over!";
        }
        break;
    }
  }

  function renderStandings(players) {
    if (!els.standings) return;
    var sorted = players.slice().sort(function (a, b) { return b.score - a.score; });
    els.standings.innerHTML = "";
    sorted.forEach(function (p, idx) {
      var row = document.createElement("div");
      row.className = "result-row" + (idx === 0 ? " winner" : "");
      row.innerHTML =
        '<div><span class="rank-number">#' + (idx + 1) + "</span>" +
        '<span class="result-name">' + GameApp.escapeHtml(p.name) + "</span></div>" +
        '<div class="result-score">' + p.score + " pts</div>";
      els.standings.appendChild(row);
    });
  }

  function vibrate() {
    try { if (navigator.vibrate) navigator.vibrate(100); } catch (e) {}
  }
})();
