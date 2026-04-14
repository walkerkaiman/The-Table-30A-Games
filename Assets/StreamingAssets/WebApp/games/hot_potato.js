(function () {
  "use strict";

  var $ = GameApp.$;
  var state = GameApp.state;

  var els = {
    roundInfo: $("#hp-round-info"),
    strikes: $("#hp-strikes"),
    timer: $("#hp-timer"),
    ring: $("#hp-ring"),
    controls: $("#hp-controls"),
    status: $("#hp-status"),
    boomText: $("#hp-boom-text"),
    boomDetail: $("#hp-boom-detail"),
    standings: $("#hp-standings"),
    btnLeft: $("#btn-pass-left"),
    btnAcross: $("#btn-pass-across"),
    btnRight: $("#btn-pass-right"),
  };

  if (els.btnLeft) {
    els.btnLeft.addEventListener("click", function () {
      GameApp.sendMessage({ type: "potato_pass", direction: "left" });
    });
  }
  if (els.btnAcross) {
    els.btnAcross.addEventListener("click", function () {
      GameApp.sendMessage({ type: "potato_pass", direction: "across" });
    });
  }
  if (els.btnRight) {
    els.btnRight.addEventListener("click", function () {
      GameApp.sendMessage({ type: "potato_pass", direction: "right" });
    });
  }

  function setupSwipe() {
    var area = document.getElementById("screen-hp-playing");
    if (!area) return;
    var startX = 0, startY = 0;

    area.addEventListener("touchstart", function (e) {
      var t = e.touches[0]; if (!t) return;
      startX = t.clientX; startY = t.clientY;
    }, { passive: true });

    area.addEventListener("touchend", function (e) {
      var t = e.changedTouches[0]; if (!t) return;
      var dx = t.clientX - startX;
      var dy = t.clientY - startY;
      var minSwipe = 40;

      if (Math.abs(dx) < minSwipe && Math.abs(dy) < minSwipe) return;

      if (Math.abs(dy) > Math.abs(dx) && dy < 0) {
        GameApp.sendMessage({ type: "potato_pass", direction: "across" });
      } else if (dx < -minSwipe) {
        GameApp.sendMessage({ type: "potato_pass", direction: "left" });
      } else if (dx > minSwipe) {
        GameApp.sendMessage({ type: "potato_pass", direction: "right" });
      }
    }, { passive: true });
  }

  setupSwipe();

  GameApp.registerGameModule("hot_potato", {
    handleGameState: function (msg) {
      handlePotatoState(msg);
    },

    handleMessage: function (msg) {
      if (msg.type === "potato_event") {
        handlePotatoEvent(msg);
      }
    },

    cleanup: function () {
      GameApp.stopTimer();
    },
  });

  function handlePotatoState(msg) {
    switch (msg.state) {
      case "PreRound":
        els.roundInfo.textContent = "Round " + msg.round;
        renderStrikes(msg.players);
        GameApp.showScreen("hp-preround");
        break;

      case "Playing":
        renderRing(msg.players, msg.holderId);
        var iAmHolder = msg.holderId === state.playerId;
        if (els.controls) els.controls.style.display = iAmHolder ? "flex" : "none";
        if (els.status) els.status.textContent = iAmHolder ? "Pass the potato! Swipe or tap!" : "Watch out...";

        if (msg.timer > 0) GameApp.startTimer(msg.timer, els.timer);
        GameApp.showScreen("hp-playing");
        break;

      case "Exploded":
      case "RoundResults":
        GameApp.showScreen("hp-exploded");
        break;

      case "GameOver":
        renderStandings(msg.players);
        GameApp.showScreen("hp-gameover");
        break;
    }
  }

  function handlePotatoEvent(msg) {
    switch (msg.eventName) {
      case "exploded":
        var isMe = msg.playerId === state.playerId;
        if (els.boomText) els.boomText.textContent = isMe ? "YOU BLEW UP!" : "BOOM!";
        if (els.boomDetail) {
          var who = isMe ? "You" : findName(msg.playerId);
          els.boomDetail.textContent = who + " was holding the potato!";
        }
        vibrate();
        break;

      case "passed":
        if (msg.toId === state.playerId) vibrate();
        break;
    }
  }

  function renderRing(players, holderId) {
    if (!els.ring) return;
    els.ring.innerHTML = "";
    if (!players) return;
    players.forEach(function (p) {
      var seat = document.createElement("div");
      seat.className = "hp-ring-seat";
      if (p.hasPotato) seat.classList.add("has-potato");
      if (p.id === state.playerId) seat.classList.add("is-me");
      if (!p.alive) seat.classList.add("eliminated");

      var label = GameApp.escapeHtml(p.name);
      if (p.strikes > 0) label += "<br>" + "X".repeat(p.strikes);
      seat.innerHTML = label;
      els.ring.appendChild(seat);
    });
  }

  function renderStrikes(players) {
    if (!els.strikes || !players) return;
    var me = players.find(function (p) { return p.id === state.playerId; });
    if (!me) return;
    var xs = "";
    for (var i = 0; i < me.strikes; i++) xs += "X ";
    els.strikes.textContent = me.strikes > 0 ? "Strikes: " + xs.trim() : "";
  }

  function renderStandings(players) {
    if (!els.standings || !players) return;
    var sorted = players.slice().sort(function (a, b) { return (a.alive === b.alive) ? a.strikes - b.strikes : (b.alive ? 1 : -1); });
    els.standings.innerHTML = "";
    sorted.forEach(function (p, idx) {
      var row = document.createElement("div");
      row.className = "result-row" + (idx === 0 ? " winner" : "");
      row.innerHTML =
        '<div><span class="rank-number">#' + (idx + 1) + "</span>" +
        '<span class="result-name">' + GameApp.escapeHtml(p.name) + "</span></div>" +
        '<div class="result-score">' + p.strikes + " strikes</div>";
      els.standings.appendChild(row);
    });
  }

  function findName(playerId) {
    return playerId ? playerId.substring(0, 6) : "Someone";
  }

  function vibrate() {
    try { if (navigator.vibrate) navigator.vibrate(200); } catch (e) {}
  }
})();
