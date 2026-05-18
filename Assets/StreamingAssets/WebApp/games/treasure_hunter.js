(function () {
  "use strict";

  var $ = GameApp.$;

  // ─── Virtual joystick (shared with Sheep Herder pattern) ────
  var stick = { x: 0, y: 0 };
  var sendInterval = null;
  var activeTouchId = null;
  var mouseDown = false;

  var els = {
    // Briefing phase
    briefingRoot: $("#screen-th-briefing"),
    clueList: $("#th-clue-list"),
    briefingTimer: $("#th-briefing-timer"),
    readyBtn: $("#th-ready-btn"),
    readySent: $("#th-ready-sent"),

    // Deploying phase
    deployRoot: $("#screen-th-deploy"),
    deployTimer: $("#th-deploy-timer"),

    // Playing phase
    playRoot: $("#screen-th-play"),
    stickArea: $("#th-stick-area"),
    stickKnob: $("#th-stick-knob"),
    goldDisplay: $("#th-gold"),
    downedBanner: $("#th-downed-banner"),
    reviveBar: $("#th-revive-bar"),
    escapeLabel: $("#th-escape-label"),
    escapeTimer: $("#th-escape-timer"),

    // Results phase
    resultsRoot: $("#screen-th-results"),
    resultsRows: $("#th-results-rows"),
    resultsGold: $("#th-results-gold"),
    resultsPuzzles: $("#th-results-puzzles"),
  };

  var myPlayerId = null;
  var isDowned = false;
  var briefingReadySent = false;

  // ─── Joystick ────────────────────────────────
  function setupJoystick() {
    if (!els.stickArea) return;
    els.stickArea.addEventListener("touchstart", onTouchStart, { passive: false });
    els.stickArea.addEventListener("touchmove", onTouchMove, { passive: false });
    els.stickArea.addEventListener("touchend", onTouchEnd);
    els.stickArea.addEventListener("touchcancel", onTouchEnd);
    els.stickArea.addEventListener("mousedown", onMouseDown);
    window.addEventListener("mousemove", onMouseMove);
    window.addEventListener("mouseup", onMouseUp);
  }

  function getRect() { return els.stickArea.getBoundingClientRect(); }

  function updateStickFromClient(clientX, clientY) {
    if (isDowned) return; // can't move while downed
    var r = getRect();
    var cx = r.left + r.width / 2;
    var cy = r.top + r.height / 2;
    var dx = (clientX - cx) / (r.width / 2);
    var dy = -(clientY - cy) / (r.height / 2);
    var mag = Math.sqrt(dx * dx + dy * dy);
    if (mag > 1) { dx /= mag; dy /= mag; }
    stick.x = dx; stick.y = dy;
    renderKnob(dx, dy);
  }

  function renderKnob(dx, dy) {
    if (!els.stickKnob) return;
    els.stickKnob.style.left = (50 + dx * 40) + "%";
    els.stickKnob.style.top  = (50 - dy * 40) + "%";
  }

  function onTouchStart(e) {
    e.preventDefault();
    if (activeTouchId !== null) return;
    var t = e.changedTouches[0]; if (!t) return;
    activeTouchId = t.identifier;
    updateStickFromClient(t.clientX, t.clientY);
  }
  function onTouchMove(e) {
    e.preventDefault();
    for (var i = 0; i < e.touches.length; i++)
      if (e.touches[i].identifier === activeTouchId)
        { updateStickFromClient(e.touches[i].clientX, e.touches[i].clientY); return; }
  }
  function onTouchEnd(e) {
    for (var i = 0; i < e.changedTouches.length; i++)
      if (e.changedTouches[i].identifier === activeTouchId)
        { activeTouchId = null; stick.x = 0; stick.y = 0; renderKnob(0, 0); return; }
  }
  function onMouseDown(e) { mouseDown = true; updateStickFromClient(e.clientX, e.clientY); }
  function onMouseMove(e) { if (!mouseDown) return; updateStickFromClient(e.clientX, e.clientY); }
  function onMouseUp() { if (!mouseDown) return; mouseDown = false; stick.x = 0; stick.y = 0; renderKnob(0, 0); }

  function startSending() {
    stopSending();
    sendInterval = setInterval(function () {
      GameApp.sendMessage({ type: "joystick_move", x: stick.x, y: stick.y });
    }, 33);
  }
  function stopSending() {
    if (sendInterval) { clearInterval(sendInterval); sendInterval = null; }
    GameApp.sendMessage({ type: "joystick_move", x: 0, y: 0 });
  }

  // ─── Briefing ───────────────────────────────
  function showBriefing(msg) {
    GameApp.showScreen("th-briefing");
    briefingReadySent = false;
    if (els.readyBtn) {
      els.readyBtn.style.display = "block";
      els.readyBtn.disabled = false;
    }
    if (els.readySent) els.readySent.style.display = "none";
    if (els.briefingTimer) GameApp.startTimer(msg.briefingSeconds || 30, els.briefingTimer);
    // Clues are pushed via th_briefing message — see handleMessage below.
  }

  function renderClues(clues) {
    if (!els.clueList) return;
    els.clueList.innerHTML = "";
    if (!clues || clues.length === 0) {
      els.clueList.innerHTML = "<li>No clues for you this round.</li>";
      return;
    }
    for (var i = 0; i < clues.length; i++) {
      var li = document.createElement("li");
      li.textContent = clues[i];
      els.clueList.appendChild(li);
    }
  }

  function sendReady() {
    if (briefingReadySent) return;
    briefingReadySent = true;
    GameApp.sendMessage({ type: "th_briefing_ready" });
    if (els.readyBtn) els.readyBtn.style.display = "none";
    if (els.readySent) els.readySent.style.display = "block";
  }

  // Wire ready button.
  if (els.readyBtn) els.readyBtn.addEventListener("click", sendReady);

  // ─── Playing / Escape UI ────────────────────
  function updatePlayingUI(msg) {
    myPlayerId = GameApp.state && GameApp.state.playerId ? GameApp.state.playerId : myPlayerId;

    var myExplorer = null;
    if (msg.explorers && myPlayerId) {
      for (var i = 0; i < msg.explorers.length; i++)
        if (msg.explorers[i].id === myPlayerId) { myExplorer = msg.explorers[i]; break; }
    }

    isDowned = myExplorer ? myExplorer.isDown : false;
    if (isDowned) { stick.x = 0; stick.y = 0; renderKnob(0, 0); }

    if (els.downedBanner) els.downedBanner.style.display = isDowned ? "block" : "none";
    if (els.reviveBar) {
      var prog = (myExplorer && isDowned) ? (myExplorer.reviveProgress || 0) : 0;
      els.reviveBar.style.width = Math.round(prog * 100) + "%";
    }
    if (els.goldDisplay) {
      var gold = myExplorer ? (myExplorer.gold || 0) : 0;
      els.goldDisplay.textContent = "Gold: " + gold;
    }

    // Run clock: always counts up (seconds elapsed since exploring started). We show it on
    // both Exploring and Escape so players know how long the run has been going.
    var isExploring = (msg.state === "Exploring");
    var isEscape = (msg.state === "Escape");
    if (els.escapeLabel) els.escapeLabel.style.display = isEscape ? "block" : "none";
    if (els.escapeTimer) {
      var seconds = Math.floor(msg.timer || 0);
      els.escapeTimer.textContent = formatElapsed(seconds);
      els.escapeTimer.style.display = (isExploring || isEscape) ? "inline-block" : "none";
    }
  }

  function formatElapsed(seconds) {
    if (seconds < 60) return seconds + "s";
    var m = Math.floor(seconds / 60);
    var s = seconds % 60;
    return m + ":" + (s < 10 ? "0" : "") + s;
  }

  // ─── Results ────────────────────────────────
  function showResults(msg) {
    GameApp.showScreen("th-results");
    stopSending();
    if (els.resultsGold) els.resultsGold.textContent = "Team Gold: " + (msg.goldTeamTotal || 0);
    if (els.resultsPuzzles)
      els.resultsPuzzles.textContent = "Puzzles: " + (msg.puzzlesSolved || 0) + " / " + (msg.puzzlesTotal || 0);

    if (!els.resultsRows || !msg.results) return;
    els.resultsRows.innerHTML = "";
    var sorted = (msg.results || []).slice().sort(function (a, b) { return (b.score || 0) - (a.score || 0); });
    sorted.forEach(function (r) {
      var row = document.createElement("tr");
      row.innerHTML =
        "<td>" + (r.name || r.id || "?") + "</td>" +
        "<td>" + (r.escaped ? "Escaped!" : "Trapped") + "</td>" +
        "<td>" + (r.goldCollected || 0) + "g</td>" +
        "<td>" + (r.score || 0) + "</td>";
      els.resultsRows.appendChild(row);
    });
  }

  // ─── Module registration ─────────────────────
  GameApp.registerGameModule("treasure_hunter", {
    handleGameState: function (msg) {
      switch (msg.state) {
        case "Briefing":
          showBriefing(msg);
          break;

        case "Deploying":
          GameApp.showScreen("th-deploy");
          if (els.deployTimer) GameApp.startTimer(msg.timer || 3, els.deployTimer);
          stopSending();
          break;

        case "Exploring":
          GameApp.showScreen("th-play");
          setupJoystick();
          startSending();
          updatePlayingUI(msg);
          break;

        case "Escape":
          // Stay on th-play screen, just show escape banner.
          if (els.escapeLabel) els.escapeLabel.style.display = "block";
          updatePlayingUI(msg);
          break;

        case "Results":
          showResults(msg);
          break;
      }
    },

    handleMessage: function (msg) {
      if (msg.type === "th_briefing") {
        renderClues(msg.clues);
        if (msg.briefingSeconds && els.briefingTimer)
          GameApp.startTimer(msg.briefingSeconds, els.briefingTimer);
        return;
      }

      if (msg.type !== "th_event") return;

      switch (msg.eventName) {
        case "trap_tripped":
          try { if (navigator.vibrate) navigator.vibrate([100, 50, 100]); } catch (e) {}
          isDowned = true;
          if (els.downedBanner) els.downedBanner.style.display = "block";
          stick.x = 0; stick.y = 0; renderKnob(0, 0);
          break;
        case "revived":
          try { if (navigator.vibrate) navigator.vibrate(200); } catch (e) {}
          isDowned = false;
          if (els.downedBanner) els.downedBanner.style.display = "none";
          if (els.reviveBar) els.reviveBar.style.width = "0%";
          break;
        case "gold_pickup":
          try { if (navigator.vibrate) navigator.vibrate(40); } catch (e) {}
          break;
        case "puzzle_solved":
          try { if (navigator.vibrate) navigator.vibrate([50, 50, 50]); } catch (e) {}
          break;
        case "escape_unlocked":
          try { if (navigator.vibrate) navigator.vibrate([200, 50, 200]); } catch (e) {}
          if (els.escapeLabel) els.escapeLabel.style.display = "block";
          break;
        case "escaped":
          try { if (navigator.vibrate) navigator.vibrate(500); } catch (e) {}
          break;
      }
    },

    cleanup: function () {
      stopSending();
      briefingReadySent = false;
      isDowned = false;
      stick.x = 0; stick.y = 0;
      activeTouchId = null; mouseDown = false;
      renderKnob(0, 0);
      GameApp.stopTimer();
    },
  });
})();
