(function () {
  "use strict";

  var $ = GameApp.$;

  // ─── Module state ─────────────────────────
  var assignment = null;           // { shape, anchors:[ax,ay,bx,by] } from Unity
  var playerColor = "#64ffda";     // From server (player palette)
  var phase = null;
  var submittedLock = false;       // Prevent double-tap on Done

  // The player's contribution is a SINGLE polyline whose ends are locked to the
  // two anchor dots. While dragging, points are appended at the finger position
  // (throttled by minimum distance). Each new touch RESTARTS the polyline so the
  // player can retry by simply dragging again.
  //
  // userPoints[i] = { x, y, t }   (x,y normalized 0..1, t = ms timestamp)
  var userPoints = [];
  var isDragging = false;
  var dragStartTimeMs = 0;
  var dragEndTimeMs = 0;

  // Minimum normalized-distance between consecutive added points. Filters out
  // jitter from a stationary finger; smaller = denser polyline = smoother curve.
  var MIN_POINT_DISTANCE = 0.012;

  // ─── DOM ─────────────────────────────────
  var els = {
    drawScreen: $("#screen-wt-draw"),
    drawCanvas: $("#wt-draw-canvas"),
    drawBtn: $("#wt-draw-btn"),
    endBtn: $("#wt-end-btn"),
    progress: $("#wt-draw-progress"),
    endScreen: $("#screen-wt-end"),
    endLink: $("#wt-end-link"),
    endUrl: $("#wt-end-url"),
  };

  var ctx = els.drawCanvas ? els.drawCanvas.getContext("2d") : null;

  // ─── Canvas sizing (DPR-aware) ─────────────
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

  function clearCanvas() {
    if (!ctx || !els.drawCanvas) return;
    ctx.clearRect(0, 0, els.drawCanvas.width, els.drawCanvas.height);
  }

  function brushWidth() {
    if (!els.drawCanvas) return 12;
    return Math.max(4, Math.round(els.drawCanvas.width / 28));
  }

  function anchorRadius() {
    if (!els.drawCanvas) return 12;
    return Math.max(8, Math.round(els.drawCanvas.width / 40));
  }

  function nowMs() {
    return (window.performance && performance.now) ? performance.now() : Date.now();
  }

  // ─── Anchor helpers ───────────────────────
  function anchorA() {
    if (!assignment || !assignment.anchors || assignment.anchors.length < 4) return null;
    return { x: assignment.anchors[0], y: assignment.anchors[1] };
  }
  function anchorB() {
    if (!assignment || !assignment.anchors || assignment.anchors.length < 4) return null;
    return { x: assignment.anchors[2], y: assignment.anchors[3] };
  }

  // Returns the polyline that will be drawn / sent: [anchor, ...userPoints, anchor],
  // oriented so the FIRST user point is closest to the first anchor. This lets the
  // player drag in either direction and have the path follow naturally.
  function buildOrientedPolyline() {
    var a = anchorA(), b = anchorB();
    if (!a || !b) return [];
    if (userPoints.length === 0) {
      // No drag yet — just a straight line between the two anchors.
      return [
        { x: a.x, y: a.y, t: 0 },
        { x: b.x, y: b.y, t: 0 }
      ];
    }
    var first = userPoints[0];
    var distFA = (first.x - a.x) * (first.x - a.x) + (first.y - a.y) * (first.y - a.y);
    var distFB = (first.x - b.x) * (first.x - b.x) + (first.y - b.y) * (first.y - b.y);
    var startAnchor = distFA <= distFB ? a : b;
    var endAnchor   = distFA <= distFB ? b : a;

    var poly = [];
    poly.push({ x: startAnchor.x, y: startAnchor.y, t: dragStartTimeMs });
    for (var i = 0; i < userPoints.length; i++) poly.push(userPoints[i]);
    poly.push({ x: endAnchor.x, y: endAnchor.y, t: dragEndTimeMs || nowMs() });
    return poly;
  }

  // ─── Tile underlay (border + anchor dots) ─
  function drawUnderlay() {
    if (!ctx || !els.drawCanvas || !assignment) return;
    sizeCanvasToDisplay(els.drawCanvas);
    clearCanvas();

    var w = els.drawCanvas.width;
    var h = els.drawCanvas.height;

    // Tile border (subtle)
    ctx.strokeStyle = "#233554";
    ctx.lineWidth = Math.max(2, Math.round(w / 200));
    ctx.strokeRect(1, 1, w - 2, h - 2);

    // Anchor dots in player color
    if (assignment.anchors && assignment.anchors.length >= 4) {
      var r = anchorRadius();
      ctx.fillStyle = playerColor;
      for (var i = 0; i + 1 < assignment.anchors.length; i += 2) {
        var nx = assignment.anchors[i];
        var ny = assignment.anchors[i + 1];
        var x = nx * w;
        var y = ny * h;
        ctx.beginPath();
        ctx.arc(x, y, r, 0, Math.PI * 2);
        ctx.fill();
      }
    }
  }

  // Redraw underlay + the full polyline preview.
  function redrawPath() {
    drawUnderlay();
    if (!ctx || !els.drawCanvas) return;
    var w = els.drawCanvas.width, h = els.drawCanvas.height;
    var poly = buildOrientedPolyline();
    if (poly.length < 2) return;

    ctx.strokeStyle = playerColor;
    ctx.lineWidth = brushWidth();
    ctx.lineCap = "round";
    ctx.lineJoin = "round";
    ctx.beginPath();
    ctx.moveTo(poly[0].x * w, poly[0].y * h);
    for (var i = 1; i < poly.length; i++) ctx.lineTo(poly[i].x * w, poly[i].y * h);
    ctx.stroke();

    // Re-draw the anchor dots ON TOP so they're never hidden under the line.
    if (assignment && assignment.anchors && assignment.anchors.length >= 4) {
      var r = anchorRadius();
      ctx.fillStyle = playerColor;
      for (var k = 0; k + 1 < assignment.anchors.length; k += 2) {
        ctx.beginPath();
        ctx.arc(assignment.anchors[k] * w, assignment.anchors[k + 1] * h, r, 0, Math.PI * 2);
        ctx.fill();
      }
    }
  }

  // ─── Drawing input ────────────────────────
  var listenersAttached = false;
  function attachDrawListeners() {
    if (listenersAttached || !els.drawCanvas) return;
    listenersAttached = true;
    els.drawCanvas.addEventListener("touchstart", onTouchStart, { passive: false });
    els.drawCanvas.addEventListener("touchmove", onTouchMove, { passive: false });
    els.drawCanvas.addEventListener("touchend", onDragEnd);
    els.drawCanvas.addEventListener("touchcancel", onDragEnd);
    els.drawCanvas.addEventListener("mousedown", onMouseStart);
    els.drawCanvas.addEventListener("mousemove", onMouseMove);
    els.drawCanvas.addEventListener("mouseup", onDragEnd);
  }

  // Prevent any scroll/bounce on the draw screen container
  if (els.drawScreen) {
    els.drawScreen.addEventListener("touchmove", function (e) { e.preventDefault(); }, { passive: false });
  }

  function beginDrag(x, y) {
    userPoints = [];
    dragStartTimeMs = nowMs();
    dragEndTimeMs = 0;
    isDragging = true;
    appendPoint(x, y);
    redrawPath();
  }

  function continueDrag(x, y) {
    if (!isDragging) return;
    appendPoint(x, y);
    redrawPath();
  }

  function endDrag() {
    if (!isDragging) return;
    dragEndTimeMs = nowMs();
    isDragging = false;
    redrawPath();
  }

  function appendPoint(x, y) {
    // Clamp into [0,1] so a flick that overshoots the canvas doesn't break the polyline.
    x = Math.max(0, Math.min(1, x));
    y = Math.max(0, Math.min(1, y));

    if (userPoints.length > 0) {
      var last = userPoints[userPoints.length - 1];
      var dx = x - last.x, dy = y - last.y;
      if (dx * dx + dy * dy < MIN_POINT_DISTANCE * MIN_POINT_DISTANCE) return;
    }
    userPoints.push({ x: x, y: y, t: nowMs() });
  }

  function canvasPoint(clientX, clientY) {
    var r = els.drawCanvas.getBoundingClientRect();
    return { x: (clientX - r.left) / r.width, y: (clientY - r.top) / r.height };
  }

  function onTouchStart(e) {
    e.preventDefault();
    var t = e.touches[0]; if (!t) return;
    var p = canvasPoint(t.clientX, t.clientY);
    beginDrag(p.x, p.y);
  }
  function onTouchMove(e) {
    e.preventDefault();
    if (!isDragging) return;
    var t = e.touches[0]; if (!t) return;
    var p = canvasPoint(t.clientX, t.clientY);
    continueDrag(p.x, p.y);
  }
  function onMouseStart(e) {
    var p = canvasPoint(e.clientX, e.clientY);
    beginDrag(p.x, p.y);
  }
  function onMouseMove(e) {
    if (!isDragging || e.buttons === 0) return;
    var p = canvasPoint(e.clientX, e.clientY);
    continueDrag(p.x, p.y);
  }
  function onDragEnd() {
    endDrag();
  }

  // ─── Submit ───────────────────────────────
  //
  // The polyline is sent as one batched draw_stroke immediately followed by
  // submit_drawing. Encoding matches the existing protocol: each pair of
  // consecutive vertices becomes a 4-float segment in `points`. Timestamps[i]
  // corresponds to vertex i so the Unity-side renderer can derive per-segment
  // thickness from speed.
  function submitPolyline() {
    // Always produces at least a 2-vertex straight line between the anchors,
    // even if the player never dragged. That's a legal contribution.
    var poly = buildOrientedPolyline();
    if (poly.length < 2) return false;

    var pointsFlat = [];
    var timestamps = [];
    timestamps.push(poly[0].t);
    for (var i = 1; i < poly.length; i++) {
      pointsFlat.push(poly[i - 1].x, poly[i - 1].y, poly[i].x, poly[i].y);
      timestamps.push(poly[i].t);
    }

    GameApp.sendMessage({
      type: "draw_stroke",
      points: pointsFlat,
      timestamps: timestamps,
      color: playerColor
    });
    GameApp.sendMessage({ type: "submit_drawing" });
    return true;
  }

  // ─── Module registration ───────────────────
  GameApp.registerGameModule("wangtiles", {
    handleGameState: function (msg) {
      phase = msg.state;

      switch (msg.state) {
        case "Painting":
          if (msg.playerColor) playerColor = msg.playerColor;
          assignment = msg.assignment || null;
          submittedLock = false;
          userPoints = [];
          isDragging = false;
          attachDrawListeners();
          GameApp.showScreen("wt-draw");
          // Defer one frame so the screen has real layout when we measure.
          requestAnimationFrame(function () {
            sizeCanvasToDisplay(els.drawCanvas);
            redrawPath(); // shows underlay + straight line between anchors
            updateHostControls();
            if (els.drawBtn) els.drawBtn.disabled = false;
          });
          break;

        case "Ending":
          if (els.endBtn) els.endBtn.disabled = true;
          break;

        case "ShowingQR":
          showEndScreen(msg.imageUrl || "");
          break;
      }
    },

    handleMessage: function (msg) {
      switch (msg.type) {
        case "drawing_received":
          // Server acknowledged the submission. Clear local state; the next
          // Painting state will redraw the underlay with the new assignment.
          submittedLock = false;
          userPoints = [];
          isDragging = false;
          if (els.drawBtn) els.drawBtn.disabled = false;
          clearCanvas();
          break;
      }
    },

    cleanup: function () {
      assignment = null;
      phase = null;
      submittedLock = false;
      isDragging = false;
      userPoints = [];
      if (els.endBtn) { els.endBtn.hidden = true; els.endBtn.disabled = false; }
      if (els.drawBtn) els.drawBtn.disabled = false;
    },
  });

  // ─── End / Host controls ───────────────────
  function showEndScreen(url) {
    if (els.endLink) {
      if (url) {
        els.endLink.href = url;
        els.endLink.style.display = "inline-block";
      } else {
        els.endLink.style.display = "none";
      }
    }
    if (els.endUrl) els.endUrl.textContent = url || "";
    GameApp.showScreen("wt-end");
  }

  function updateHostControls() {
    if (!els.endBtn) return;
    var isHost = !!(GameApp.state && GameApp.state.isHost);
    els.endBtn.hidden = !isHost;
  }

  if (els.drawBtn) {
    els.drawBtn.addEventListener("click", function () {
      if (submittedLock) return;
      submittedLock = true;
      els.drawBtn.disabled = true;
      var sent = submitPolyline();
      if (!sent) {
        // No drag yet — re-enable the button so the user can try again.
        submittedLock = false;
        if (els.drawBtn) els.drawBtn.disabled = false;
      }
    });
  }

  if (els.endBtn) {
    els.endBtn.addEventListener("click", function () {
      if (els.endBtn.disabled) return;
      els.endBtn.disabled = true;
      GameApp.sendMessage({ type: "end_game" });
    });
  }

  // Re-evaluate host visibility when the player list / host id arrives.
  setInterval(function () {
    if (phase === "Painting") updateHostControls();
  }, 750);
})();
