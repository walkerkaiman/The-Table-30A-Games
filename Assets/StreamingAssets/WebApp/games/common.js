(function () {
  "use strict";

  /**
   * Shared rendering helpers for game modules.
   * Loaded before any game-specific JS so all modules can call these.
   */

  // ─── Round Label ───────────────────────────
  GameApp.renderRoundLabel = function (msg) {
    return "Round " + msg.round + " of " + msg.totalRounds;
  };

  // ─── Final Standings ───────────────────────
  GameApp.renderStandings = function (container, players) {
    if (!container) return;
    container.innerHTML = "";
    var sorted = (players || []).slice().sort(function (a, b) { return b.score - a.score; });
    sorted.forEach(function (p, idx) {
      var row = document.createElement("div");
      row.className = "result-row" + (idx === 0 ? " winner" : "");
      row.innerHTML =
        '<div><span class="rank-number">#' + (idx + 1) + "</span>" +
        '<span class="result-name">' + GameApp.escapeHtml(p.name) + "</span></div>" +
        '<div class="result-score">' + p.score + " pts</div>";
      container.appendChild(row);
    });
  };

  // ─── Choice List (vote / pick UI) ──────────
  // items: array of objects
  // opts.getLabel(item, index) -> display text
  // opts.isOwn(item) -> true if this item belongs to the current player (disabled)
  // opts.onSelect(item, index) -> called once when player picks
  GameApp.renderChoiceList = function (container, items, opts) {
    if (!container) return;
    container.innerHTML = "";
    var selected = false;

    items.forEach(function (item, i) {
      var card = document.createElement("div");
      card.className = "answer-card";
      card.textContent = opts.getLabel(item, i);

      if (opts.isOwn && opts.isOwn(item)) {
        card.classList.add("own-answer");
        card.textContent += " (yours)";
      } else {
        card.addEventListener("click", function () {
          if (selected) return;
          container.querySelectorAll(".answer-card").forEach(function (el) {
            el.classList.remove("selected");
          });
          card.classList.add("selected");
          selected = true;
          if (opts.onSelect) opts.onSelect(item, i);
        });
      }
      container.appendChild(card);
    });
  };

  // ─── Wire Submit (input + button + ack) ────
  // Wires an input field and button so that:
  //   - Clicking button or pressing Enter sends the message
  //   - Button is disabled after send
  //   - When ackType message arrives, shows waitScreen
  // Returns an object with { handleAck, reset } for the module to use.
  GameApp.wireSubmit = function (inputEl, buttonEl, getPayload, ackType, waitScreen) {
    var submitted = false;

    function doSubmit() {
      if (submitted) return;
      var payload = getPayload();
      if (!payload) return;
      submitted = true;
      if (buttonEl) buttonEl.disabled = true;
      GameApp.sendMessage(payload);
    }

    if (buttonEl) {
      buttonEl.addEventListener("click", doSubmit);
    }
    if (inputEl) {
      inputEl.addEventListener("keydown", function (e) {
        if (e.key === "Enter" && !e.shiftKey) {
          e.preventDefault();
          doSubmit();
        }
      });
    }

    return {
      handleAck: function (msgType) {
        if (msgType === ackType) {
          submitted = true;
          GameApp.showScreen(waitScreen);
          return true;
        }
        return false;
      },
      isSubmitted: function () { return submitted; },
      reset: function () {
        submitted = false;
        if (buttonEl) buttonEl.disabled = false;
        if (inputEl) inputEl.value = "";
      },
    };
  };
})();
