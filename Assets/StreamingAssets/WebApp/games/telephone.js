(function () {
  "use strict";

  var $ = GameApp.$;
  var submitted = false;
  var currentPhase = null;
  var isDrawing = false;
  var lastPoint = null;
  var strokeBuffer = [];
  var flushInterval = null;

  var NOUNS = [
    "a cat", "a dog", "a penguin", "a robot", "a pirate", "an astronaut", "a wizard",
    "a cowboy", "a dinosaur", "a baby", "a chef", "a mermaid", "an alien", "a clown",
    "a vampire", "a ghost", "a grandma", "a ninja", "a dragon", "a chicken",
    "a unicorn", "a scientist", "a monkey", "a snowman", "a cactus", "a potato",
    "a shark", "a superhero", "a frog", "a giraffe", "an octopus", "a walrus",
    "a lumberjack", "a flamingo", "a dentist", "a sumo wrestler", "a panda",
    "a raccoon in a suit", "a tiny horse", "an angry toddler", "a sentient toaster"
  ];

  var ACTIONS = [
    "riding a skateboard", "eating spaghetti", "fighting a bear", "doing yoga",
    "flying a kite", "robbing a bank", "surfing a wave", "running from bees",
    "playing the drums", "proposing marriage", "winning the lottery", "stuck in a tree",
    "lifting weights", "painting a portrait", "bungee jumping", "juggling chainsaws",
    "riding a unicycle", "cooking breakfast", "karate chopping a watermelon",
    "breakdancing", "walking a tightrope", "delivering pizza", "scuba diving",
    "singing karaoke", "arm wrestling", "being abducted by UFOs", "on a first date",
    "taking a selfie", "in a food fight", "building a sandcastle", "at the dentist",
    "skydiving without a parachute", "teaching a class", "escaping prison",
    "doing stand-up comedy", "dunking a basketball", "chasing an ice cream truck",
    "arguing with a mirror", "lost in IKEA", "herding cats"
  ];

  function pickRandom(arr) { return arr[Math.floor(Math.random() * arr.length)]; }

  var els = {
    writeTimer: $("#tel-write-timer"),
    writeInput: $("#tel-write-input"),
    writeBtn: $("#tel-write-btn"),
    writeStep: $("#tel-write-step"),
    nounInput: $("#tel-noun-input"),
    actionInput: $("#tel-action-input"),
    nounRandom: $("#tel-noun-random"),
    actionRandom: $("#tel-action-random"),
    preview: $("#tel-preview"),
    freeformInput: $("#tel-freeform-input"),

    drawScreen: $("#screen-tel-draw"),
    drawTimer: $("#tel-draw-timer"),
    drawPrompt: $("#tel-draw-prompt"),
    drawCanvas: $("#tel-draw-canvas"),
    drawBtn: $("#tel-draw-btn"),
    drawStep: $("#tel-draw-step"),

    descTimer: $("#tel-desc-timer"),
    descCanvas: $("#tel-desc-canvas"),
    descInput: $("#tel-desc-input"),
    descBtn: $("#tel-desc-btn"),
    descStep: $("#tel-desc-step"),

    revealProgress: $("#tel-reveal-progress"),
    revealAuthor: $("#tel-reveal-author"),
    revealTextContent: $("#tel-reveal-text-content"),
    revealDrawingCanvas: $("#tel-reveal-canvas"),
    revealTextBlock: $("#tel-reveal-text-block"),
    revealDrawingBlock: $("#tel-reveal-drawing-block"),
  };

  var drawCtx = els.drawCanvas ? els.drawCanvas.getContext("2d") : null;
  var descCtx = els.descCanvas ? els.descCanvas.getContext("2d") : null;
  var revealCtx = els.revealDrawingCanvas ? els.revealDrawingCanvas.getContext("2d") : null;

  // ─── Canvas Sizing ─────────────────────────
  // Match the canvas bitmap resolution to its CSS display size so strokes
  // render sharply on high-DPI phones.

  function sizeCanvasToDisplay(cvs) {
    if (!cvs) return;
    var rect = cvs.getBoundingClientRect();
    var dpr = window.devicePixelRatio || 1;
    var w = Math.round(rect.width * dpr);
    var h = Math.round(rect.height * dpr);
    if (cvs.width !== w || cvs.height !== h) {
      cvs.width = w;
      cvs.height = h;
    }
  }

  function lineWidthForCanvas(cvs) {
    if (!cvs) return 3;
    return Math.max(2, Math.round(cvs.width / 120));
  }

  // ─── Drawing Input ─────────────────────────

  var listenersAttached = false;

  function attachDrawListeners() {
    if (listenersAttached || !els.drawCanvas) return;
    listenersAttached = true;
    els.drawCanvas.addEventListener("touchstart", onTouchStart, { passive: false });
    els.drawCanvas.addEventListener("touchmove", onTouchMove, { passive: false });
    els.drawCanvas.addEventListener("touchend", onDrawEnd);
    els.drawCanvas.addEventListener("touchcancel", onDrawEnd);
    els.drawCanvas.addEventListener("mousedown", onMouseStart);
    els.drawCanvas.addEventListener("mousemove", onMouseMove);
    els.drawCanvas.addEventListener("mouseup", onDrawEnd);
  }

  // Prevent any scroll/bounce on the draw screen container
  if (els.drawScreen) {
    els.drawScreen.addEventListener("touchmove", function (e) { e.preventDefault(); }, { passive: false });
  }

  function onTouchStart(e) {
    e.preventDefault();
    var t = e.touches[0]; if (!t) return;
    var r = els.drawCanvas.getBoundingClientRect();
    lastPoint = { x: (t.clientX - r.left) / r.width, y: (t.clientY - r.top) / r.height };
    isDrawing = true;
  }

  function onTouchMove(e) {
    e.preventDefault();
    if (!isDrawing) return;
    var t = e.touches[0]; if (!t) return;
    var r = els.drawCanvas.getBoundingClientRect();
    var p = { x: (t.clientX - r.left) / r.width, y: (t.clientY - r.top) / r.height };
    drawSegment(drawCtx, els.drawCanvas, lastPoint, p);
    strokeBuffer.push(lastPoint.x, lastPoint.y, p.x, p.y);
    lastPoint = p;
  }

  function onMouseStart(e) {
    var r = els.drawCanvas.getBoundingClientRect();
    lastPoint = { x: (e.clientX - r.left) / r.width, y: (e.clientY - r.top) / r.height };
    isDrawing = true;
  }

  function onMouseMove(e) {
    if (!isDrawing || e.buttons === 0) return;
    var r = els.drawCanvas.getBoundingClientRect();
    var p = { x: (e.clientX - r.left) / r.width, y: (e.clientY - r.top) / r.height };
    drawSegment(drawCtx, els.drawCanvas, lastPoint, p);
    strokeBuffer.push(lastPoint.x, lastPoint.y, p.x, p.y);
    lastPoint = p;
  }

  function onDrawEnd() {
    isDrawing = false;
    lastPoint = null;
    flushStrokes();
  }

  function drawSegment(context, cvs, a, b) {
    if (!context) return;
    var w = cvs.width, h = cvs.height;
    context.beginPath();
    context.moveTo(a.x * w, a.y * h);
    context.lineTo(b.x * w, b.y * h);
    context.strokeStyle = "#ffffff";
    context.lineWidth = lineWidthForCanvas(cvs);
    context.lineCap = "round";
    context.stroke();
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

  function clearCanvas(cvs, context) {
    if (context && cvs) context.clearRect(0, 0, cvs.width, cvs.height);
  }

  // ─── Stroke Rendering ─────────────────────

  function renderStrokes(context, cvs, strokes) {
    if (!context || !cvs || !strokes) return;
    sizeCanvasToDisplay(cvs);
    clearCanvas(cvs, context);
    var w = cvs.width, h = cvs.height;
    var lw = lineWidthForCanvas(cvs);
    for (var s = 0; s < strokes.length; s++) {
      var stroke = strokes[s];
      if (!stroke.points) continue;
      context.strokeStyle = stroke.color || "#ffffff";
      context.lineWidth = lw;
      context.lineCap = "round";
      for (var i = 0; i + 3 < stroke.points.length; i += 4) {
        context.beginPath();
        context.moveTo(stroke.points[i] * w, stroke.points[i + 1] * h);
        context.lineTo(stroke.points[i + 2] * w, stroke.points[i + 3] * h);
        context.stroke();
      }
    }
  }

  function stepLabel(msg) {
    return "Step " + (msg.step + 1) + " of " + msg.totalSteps;
  }

  // ─── Module Registration ───────────────────

  GameApp.registerGameModule("telephone", {
    handleGameState: function (msg) {
      if (msg.state !== currentPhase) {
        submitted = false;
        currentPhase = msg.state;
      }

      switch (msg.state) {
        case "WritePrompt":
          els.writeStep.textContent = stepLabel(msg);
          if (els.nounInput) els.nounInput.value = "";
          if (els.actionInput) els.actionInput.value = "";
          if (els.freeformInput) els.freeformInput.value = "";
          if (els.preview) els.preview.textContent = "";
          els.writeBtn.disabled = false;
          GameApp.showScreen("tel-write");
          GameApp.startTimer(msg.timer, els.writeTimer);
          break;

        case "Draw":
          if (submitted) { GameApp.showScreen("tel-wait"); break; }
          attachDrawListeners();
          GameApp.showScreen("tel-draw");
          // Size after the screen is visible so getBoundingClientRect returns real values
          sizeCanvasToDisplay(els.drawCanvas);
          clearCanvas(els.drawCanvas, drawCtx);
          startFlushing();
          els.drawStep.textContent = stepLabel(msg);
          els.drawPrompt.textContent = msg.assignment || "";
          els.drawBtn.disabled = false;
          GameApp.startTimer(msg.timer, els.drawTimer);
          break;

        case "Describe":
          if (submitted) { GameApp.showScreen("tel-wait"); break; }
          GameApp.showScreen("tel-describe");
          renderStrokes(descCtx, els.descCanvas, msg.strokes || []);
          els.descStep.textContent = stepLabel(msg);
          els.descInput.value = "";
          els.descBtn.disabled = false;
          GameApp.startTimer(msg.timer, els.descTimer);
          break;

        case "Reveal":
          stopFlushing();
          renderRevealEntry(msg);
          GameApp.showScreen("tel-reveal");
          break;

        case "RevealPause":
          els.revealProgress.textContent =
            "Chain " + (msg.chainIndex + 1) + " of " + msg.totalChains + " complete!";
          els.revealAuthor.textContent = "Next chain coming up...";
          els.revealTextBlock.style.display = "none";
          els.revealDrawingBlock.style.display = "none";
          GameApp.showScreen("tel-reveal");
          break;

        case "Done":
          stopFlushing();
          GameApp.showScreen("tel-done");
          break;
      }
    },

    handleMessage: function (msg) {
      switch (msg.type) {
        case "prompt_received":
        case "description_received":
          submitted = true;
          GameApp.showScreen("tel-wait");
          break;
        case "drawing_received":
          submitted = true;
          stopFlushing();
          GameApp.showScreen("tel-wait");
          break;
      }
    },

    cleanup: function () {
      stopFlushing();
      submitted = false;
      currentPhase = null;
      isDrawing = false;
      strokeBuffer = [];
      GameApp.stopTimer();
    },
  });

  function renderRevealEntry(msg) {
    var entry = msg.entry;
    if (!entry) return;

    els.revealProgress.textContent =
      "Chain " + (msg.chainIndex + 1) + " of " + msg.totalChains +
      " \u2014 " + (msg.entryIndex + 1) + "/" + msg.chainLength;
    els.revealAuthor.textContent = entry.playerName || "";

    if (entry.entryType === "drawing") {
      els.revealTextBlock.style.display = "none";
      els.revealDrawingBlock.style.display = "block";
      renderStrokes(revealCtx, els.revealDrawingCanvas, entry.strokes || []);
    } else {
      els.revealDrawingBlock.style.display = "none";
      els.revealTextBlock.style.display = "block";
      els.revealTextContent.textContent = entry.content || "";
    }
  }

  // ─── Ad-Lib Helpers ────────────────────────

  function updatePreview() {
    if (!els.preview) return;
    var noun = (els.nounInput ? els.nounInput.value : "").trim();
    var action = (els.actionInput ? els.actionInput.value : "").trim();
    if (noun && action) {
      var first = noun.charAt(0).toUpperCase() + noun.slice(1);
      els.preview.textContent = first + " " + action;
    } else {
      els.preview.textContent = "";
    }
  }

  function getPromptText() {
    var freeform = (els.freeformInput ? els.freeformInput.value : "").trim();
    if (freeform) return freeform;
    var noun = (els.nounInput ? els.nounInput.value : "").trim();
    var action = (els.actionInput ? els.actionInput.value : "").trim();
    if (noun && action) {
      var first = noun.charAt(0).toUpperCase() + noun.slice(1);
      return first + " " + action;
    }
    return "";
  }

  if (els.nounInput) els.nounInput.addEventListener("input", updatePreview);
  if (els.actionInput) els.actionInput.addEventListener("input", updatePreview);

  if (els.nounRandom) {
    els.nounRandom.addEventListener("click", function () {
      if (els.nounInput) { els.nounInput.value = pickRandom(NOUNS); updatePreview(); }
    });
  }
  if (els.actionRandom) {
    els.actionRandom.addEventListener("click", function () {
      if (els.actionInput) { els.actionInput.value = pickRandom(ACTIONS); updatePreview(); }
    });
  }

  // ─── Button Wiring ─────────────────────────

  if (els.writeBtn) {
    els.writeBtn.addEventListener("click", function () {
      if (submitted) return;
      var text = getPromptText();
      if (!text) return;
      submitted = true;
      els.writeBtn.disabled = true;
      GameApp.sendMessage({ type: "submit_prompt", text: text });
    });
  }
  if (els.freeformInput) {
    els.freeformInput.addEventListener("keydown", function (e) {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        if (els.writeBtn) els.writeBtn.click();
      }
    });
  }

  if (els.drawBtn) {
    els.drawBtn.addEventListener("click", function () {
      if (submitted) return;
      submitted = true;
      els.drawBtn.disabled = true;
      stopFlushing();
      GameApp.sendMessage({ type: "submit_drawing" });
    });
  }

  if (els.descBtn) {
    els.descBtn.addEventListener("click", function () {
      if (submitted) return;
      var text = (els.descInput.value || "").trim();
      if (!text) return;
      submitted = true;
      els.descBtn.disabled = true;
      GameApp.sendMessage({ type: "submit_description", text: text });
    });
  }
  if (els.descInput) {
    els.descInput.addEventListener("keydown", function (e) {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        if (els.descBtn) els.descBtn.click();
      }
    });
  }
})();
