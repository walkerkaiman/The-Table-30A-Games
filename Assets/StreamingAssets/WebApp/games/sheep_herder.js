(function () {
  "use strict";

  var $ = GameApp.$;
  var state = GameApp.state;

  // Stick vector in [-1, 1]. Sent on a fixed interval rather than every touchmove so we don't
  // flood the websocket at whatever rate the phone's touch sensor fires (some devices go 120Hz+).
  var stick = { x: 0, y: 0 };
  var sendInterval = null;
  var activeTouchId = null;

  var els = {
    countdownTimer: $("#sh-countdown-timer"),
    playingRoot: $("#screen-sh-playing"),
    status: $("#sh-status"),
    remaining: $("#sh-remaining"),
    teamScore: $("#sh-team-score"),
    stickArea: $("#sh-stick-area"),
    stickKnob: $("#sh-stick-knob"),
    gameoverScore: $("#sh-gameover-score"),
    standings: $("#sh-standings"),
  };

  // ─── Virtual joystick ──────────────────────
  function setupJoystick() {
    if (!els.stickArea) return;
    els.stickArea.addEventListener("touchstart", onTouchStart, { passive: false });
    els.stickArea.addEventListener("touchmove", onTouchMove, { passive: false });
    els.stickArea.addEventListener("touchend", onTouchEnd);
    els.stickArea.addEventListener("touchcancel", onTouchEnd);

    // Mouse fallback so you can test from a laptop.
    els.stickArea.addEventListener("mousedown", onMouseDown);
    window.addEventListener("mousemove", onMouseMove);
    window.addEventListener("mouseup", onMouseUp);
  }

  function getRect() {
    return els.stickArea.getBoundingClientRect();
  }

  function updateStickFromClient(clientX, clientY) {
    var r = getRect();
    var cx = r.left + r.width / 2;
    var cy = r.top + r.height / 2;
    // Phone is held portrait but the table is top-down: y-up on the stick should mean
    // "forward in the world" which we map on the Unity side. Invert here so up = positive.
    var dx = (clientX - cx) / (r.width / 2);
    var dy = -(clientY - cy) / (r.height / 2);
    var mag = Math.sqrt(dx * dx + dy * dy);
    if (mag > 1) {
      dx /= mag;
      dy /= mag;
      mag = 1;
    }
    stick.x = dx;
    stick.y = dy;
    renderKnob(dx, dy);
  }

  function renderKnob(dx, dy) {
    if (!els.stickKnob) return;
    // Knob travels within its parent — half the base radius of the stick area.
    var pct = 40;
    els.stickKnob.style.left = (50 + dx * pct) + "%";
    els.stickKnob.style.top = (50 - dy * pct) + "%"; // CSS y is inverted
  }

  function onTouchStart(e) {
    e.preventDefault();
    if (activeTouchId !== null) return;
    var t = e.changedTouches[0];
    if (!t) return;
    activeTouchId = t.identifier;
    updateStickFromClient(t.clientX, t.clientY);
  }

  function onTouchMove(e) {
    e.preventDefault();
    for (var i = 0; i < e.touches.length; i++) {
      if (e.touches[i].identifier === activeTouchId) {
        updateStickFromClient(e.touches[i].clientX, e.touches[i].clientY);
        return;
      }
    }
  }

  function onTouchEnd(e) {
    for (var i = 0; i < e.changedTouches.length; i++) {
      if (e.changedTouches[i].identifier === activeTouchId) {
        activeTouchId = null;
        stick.x = 0; stick.y = 0;
        renderKnob(0, 0);
        return;
      }
    }
  }

  var mouseDown = false;
  function onMouseDown(e) {
    mouseDown = true;
    updateStickFromClient(e.clientX, e.clientY);
  }
  function onMouseMove(e) {
    if (!mouseDown) return;
    updateStickFromClient(e.clientX, e.clientY);
  }
  function onMouseUp() {
    if (!mouseDown) return;
    mouseDown = false;
    stick.x = 0; stick.y = 0;
    renderKnob(0, 0);
  }

  function startSending() {
    stopSending();
    sendInterval = setInterval(function () {
      GameApp.sendMessage({ type: "joystick_move", x: stick.x, y: stick.y });
    }, 33); // ~30Hz — matches the Pong cadence.
  }

  function stopSending() {
    if (sendInterval) {
      clearInterval(sendInterval);
      sendInterval = null;
    }
    // Send one last zero so the shepherd doesn't drift if the interval is torn down mid-push.
    GameApp.sendMessage({ type: "joystick_move", x: 0, y: 0 });
  }

  // ─── Module ────────────────────────────────
  GameApp.registerGameModule("sheep_herder", {
    handleGameState: function (msg) {
      if (els.remaining) {
        els.remaining.textContent = (msg.sheepRemaining || 0) + " / " + (msg.sheepTotal || 0);
      }
      if (els.teamScore) {
        els.teamScore.textContent = msg.teamScore != null ? msg.teamScore : 0;
      }

      switch (msg.state) {
        case "Countdown":
          GameApp.showScreen("sh-countdown");
          if (msg.timer > 0) GameApp.startTimer(msg.timer, els.countdownTimer);
          break;

        case "Playing":
          GameApp.showScreen("sh-playing");
          setupJoystick();
          startSending();
          if (els.status) {
            els.status.textContent = msg.mode === "competitive"
              ? "Herd sheep into YOUR zone!"
              : "Work together to herd the flock!";
          }
          break;

        case "GameOver":
          stopSending();
          GameApp.showScreen("sh-gameover");
          if (els.gameoverScore) els.gameoverScore.textContent = "Team Score: " + (msg.teamScore || 0);
          if (GameApp.renderStandings) GameApp.renderStandings(els.standings, msg.players || []);
          break;
      }
    },

    handleMessage: function (msg) {
      if (msg.type !== "sh_event") return;
      if (msg.eventName === "sheep_scored") {
        try { if (navigator.vibrate) navigator.vibrate(40); } catch (e) {}
      }
    },

    cleanup: function () {
      stopSending();
      stick.x = 0; stick.y = 0;
      activeTouchId = null;
      mouseDown = false;
      renderKnob(0, 0);
      GameApp.stopTimer();
    },
  });
})();
