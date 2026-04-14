(function () {
  "use strict";

  var $ = GameApp.$;
  var state = GameApp.state;
  var hasGuessed = false;
  var isDrawing = false;
  var canvas, ctx;
  var lastPoint = null;
  var strokeBuffer = [];
  var flushInterval = null;

  var els = {
    promptRound: $("#sd-prompt-round"),
    promptText: $("#sd-prompt-text"),
    planRound: $("#sd-plan-round"),
    planPrompt: $("#sd-plan-prompt"),
    planTimer: $("#sd-plan-timer"),
    drawTimer: $("#sd-draw-timer"),
    canvas: $("#sd-canvas"),
    guessRound: $("#sd-guess-round"),
    guessTimer: $("#sd-guess-timer"),
    guessChoices: $("#sd-guess-choices"),
    correctLabel: $("#sd-correct-label"),
    resultsList: $("#sd-results-list"),
    standings: $("#sd-standings"),
  };

  canvas = els.canvas;
  if (canvas) ctx = canvas.getContext("2d");

  function setupDrawListeners() {
    if (!canvas) return;
    canvas.addEventListener("touchstart", onDrawStart, { passive: false });
    canvas.addEventListener("touchmove", onDrawMove, { passive: false });
    canvas.addEventListener("touchend", onDrawEnd);
    canvas.addEventListener("mousedown", onMouseStart);
    canvas.addEventListener("mousemove", onMouseMove);
    canvas.addEventListener("mouseup", onDrawEnd);
  }

  function onDrawStart(e) {
    e.preventDefault();
    var t = e.touches[0]; if (!t) return;
    var r = canvas.getBoundingClientRect();
    lastPoint = { x: (t.clientX - r.left) / r.width, y: (t.clientY - r.top) / r.height };
    isDrawing = true;
  }

  function onDrawMove(e) {
    e.preventDefault();
    if (!isDrawing) return;
    var t = e.touches[0]; if (!t) return;
    var r = canvas.getBoundingClientRect();
    var p = { x: (t.clientX - r.left) / r.width, y: (t.clientY - r.top) / r.height };
    drawSegment(lastPoint, p);
    strokeBuffer.push(lastPoint.x, lastPoint.y, p.x, p.y);
    lastPoint = p;
  }

  function onMouseStart(e) {
    var r = canvas.getBoundingClientRect();
    lastPoint = { x: (e.clientX - r.left) / r.width, y: (e.clientY - r.top) / r.height };
    isDrawing = true;
  }

  function onMouseMove(e) {
    if (!isDrawing || e.buttons === 0) return;
    var r = canvas.getBoundingClientRect();
    var p = { x: (e.clientX - r.left) / r.width, y: (e.clientY - r.top) / r.height };
    drawSegment(lastPoint, p);
    strokeBuffer.push(lastPoint.x, lastPoint.y, p.x, p.y);
    lastPoint = p;
  }

  function onDrawEnd() {
    isDrawing = false;
    lastPoint = null;
    flushStrokes();
  }

  function drawSegment(a, b) {
    if (!ctx) return;
    var w = canvas.width, h = canvas.height;
    ctx.beginPath();
    ctx.moveTo(a.x * w, a.y * h);
    ctx.lineTo(b.x * w, b.y * h);
    ctx.strokeStyle = "#ffffff";
    ctx.lineWidth = 3;
    ctx.lineCap = "round";
    ctx.stroke();
  }

  function flushStrokes() {
    if (strokeBuffer.length === 0) return;
    GameApp.sendMessage({ type: "draw_stroke", points: strokeBuffer, color: "#ffffff" });
    strokeBuffer = [];
  }

  function startFlushing() {
    stopFlushing();
    flushInterval = setInterval(flushStrokes, 100);
  }

  function stopFlushing() {
    if (flushInterval) { clearInterval(flushInterval); flushInterval = null; }
    flushStrokes();
  }

  function clearCanvas() {
    if (ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
  }

  GameApp.registerGameModule("speed_draw", {
    handleGameState: function (msg) {
      var roundLabel = GameApp.renderRoundLabel(msg);

      switch (msg.state) {
        case "ShowPrompt":
          hasGuessed = false;
          clearCanvas();
          els.promptRound.textContent = roundLabel;
          els.promptText.textContent = msg.prompt;
          GameApp.showScreen("sd-prompt");
          break;

        case "Plan":
          els.planRound.textContent = roundLabel;
          els.planPrompt.textContent = msg.prompt;
          GameApp.showScreen("sd-plan");
          GameApp.startTimer(msg.timer, els.planTimer);
          break;

        case "Draw":
          clearCanvas();
          setupDrawListeners();
          startFlushing();
          GameApp.showScreen("sd-draw");
          GameApp.startTimer(msg.timer, els.drawTimer);
          break;

        case "Guess":
          stopFlushing();
          if (hasGuessed) {
            GameApp.showScreen("sd-guess-wait");
          } else {
            els.guessRound.textContent = roundLabel;
            GameApp.renderChoiceList(els.guessChoices, msg.labels || [], {
              getLabel: function (l) { return l.text; },
              onSelect: function (l, i) {
                hasGuessed = true;
                GameApp.sendMessage({ type: "draw_guess", choiceIndex: i });
              },
            });
            GameApp.showScreen("sd-guess");
            GameApp.startTimer(msg.timer, els.guessTimer);
          }
          break;

        case "RoundResults":
          stopFlushing();
          renderDrawResults(msg);
          GameApp.showScreen("sd-results");
          break;

        case "GameOver":
          stopFlushing();
          GameApp.renderStandings(els.standings, msg.players || []);
          GameApp.showScreen("sd-gameover");
          break;
      }
    },

    handleMessage: function (msg) {
      switch (msg.type) {
        case "draw_stroke":
          renderRemoteStroke(msg);
          break;
        case "guess_received":
          hasGuessed = true;
          GameApp.showScreen("sd-guess-wait");
          break;
      }
    },

    cleanup: function () {
      stopFlushing();
      hasGuessed = false;
      isDrawing = false;
      GameApp.stopTimer();
    },
  });

  function renderRemoteStroke(msg) {
    if (!ctx || !msg.points) return;
    var pts = msg.points;
    var w = canvas.width, h = canvas.height;
    ctx.strokeStyle = msg.color || "#ffffff";
    ctx.lineWidth = 3;
    ctx.lineCap = "round";
    for (var i = 0; i + 3 < pts.length; i += 4) {
      ctx.beginPath();
      ctx.moveTo(pts[i] * w, pts[i + 1] * h);
      ctx.lineTo(pts[i + 2] * w, pts[i + 3] * h);
      ctx.stroke();
    }
  }

  function renderDrawResults(msg) {
    var correctText = "";
    if (msg.labels) {
      for (var i = 0; i < msg.labels.length; i++) {
        if (msg.labels[i].isCorrect) { correctText = msg.labels[i].text; break; }
      }
    }
    els.correctLabel.textContent = "It was: " + correctText;
    GameApp.renderStandings(els.resultsList, msg.players || []);
  }
})();
