(function () {
  "use strict";

  var $ = GameApp.$;

  var els = {
    itemRound: $("#pic-item-round"),
    itemTitle: $("#pic-item-title"),
    itemDesc: $("#pic-item-desc"),
    itemImage: $("#pic-item-image"),
    itemVideo: $("#pic-item-video"),
    itemUnit: $("#pic-item-unit"),
    guessRound: $("#pic-guess-round"),
    guessTitle: $("#pic-guess-title"),
    guessTimer: $("#pic-guess-timer"),
    guessUnit: $("#pic-guess-unit"),
    inputGuess: $("#input-guess"),
    btnGuess: $("#btn-guess"),
    revealPrice: $("#pic-reveal-price"),
    revealList: $("#pic-reveal-list"),
    standings: $("#pic-standings"),
  };

  var guessSubmit = GameApp.wireSubmit(
    els.inputGuess, els.btnGuess,
    function () {
      var val = parseFloat(els.inputGuess.value);
      return isNaN(val) ? null : { type: "submit_guess", guess: val };
    },
    "guess_received", "pic-guess-wait"
  );

  GameApp.registerGameModule("price_is_close", {
    handleGameState: function (msg) {
      var roundLabel = GameApp.renderRoundLabel(msg);
      var unit = msg.unit || "$";

      switch (msg.state) {
        case "ShowItem":
          guessSubmit.reset();
          els.itemRound.textContent = roundLabel;
          els.itemTitle.textContent = msg.title || "";
          els.itemDesc.textContent = msg.description || "";
          els.itemUnit.textContent = "Guess the price in " + unit;
          showMedia(els.itemImage, els.itemVideo, msg.imageUrl, msg.videoUrl);
          GameApp.showScreen("pic-item");
          break;

        case "Guess":
          if (guessSubmit.isSubmitted()) {
            GameApp.showScreen("pic-guess-wait");
          } else {
            els.guessRound.textContent = roundLabel;
            els.guessTitle.textContent = msg.title || "";
            els.guessUnit.textContent = "Enter your guess (" + unit + ")";
            guessSubmit.reset();
            GameApp.showScreen("pic-guess");
            GameApp.startTimer(msg.timer, els.guessTimer);
            els.inputGuess.focus();
          }
          break;

        case "Reveal":
        case "RoundResults":
          els.revealPrice.textContent = unit + msg.correctPrice;
          renderGuessResults(msg.results || [], unit);
          GameApp.showScreen("pic-reveal");
          break;

        case "GameOver":
          GameApp.renderStandings(els.standings, msg.players || []);
          GameApp.showScreen("pic-gameover");
          break;
      }
    },

    handleMessage: function (msg) {
      guessSubmit.handleAck(msg.type);
    },

    cleanup: function () {
      guessSubmit.reset();
      GameApp.stopTimer();
    },
  });

  function showMedia(imgEl, vidEl, imageUrl, videoUrl) {
    if (vidEl) {
      if (videoUrl) {
        vidEl.src = videoUrl;
        vidEl.style.display = "";
        if (imgEl) imgEl.style.display = "none";
        return;
      }
      vidEl.style.display = "none";
    }
    if (imgEl) {
      imgEl.src = imageUrl || "";
      imgEl.style.display = imageUrl ? "" : "none";
    }
  }

  function renderGuessResults(results, unit) {
    els.revealList.innerHTML = "";
    results.forEach(function (r, idx) {
      var diff = Math.abs(r.guess - r.correctPrice);
      var row = document.createElement("div");
      row.className = "result-row" + (idx === 0 ? " winner" : "");
      row.innerHTML =
        "<div>" +
          '<div class="result-name">' + GameApp.escapeHtml(r.name) + "</div>" +
          '<div class="pic-guess-diff">Guessed: ' + unit + r.guess + " (off by " + unit + diff.toFixed(2) + ")</div>" +
        "</div>" +
        '<div class="result-score">+' + r.score + "</div>";
      els.revealList.appendChild(row);
    });
  }
})();
