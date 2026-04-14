(function () {
  "use strict";

  var $ = GameApp.$;
  var state = GameApp.state;
  var hasBluffed = false;
  var hasVoted = false;

  var els = {
    promptRound: $("#fib-prompt-round"),
    promptText: $("#fib-prompt-text"),
    bluffRound: $("#fib-bluff-round"),
    bluffPrompt: $("#fib-bluff-prompt"),
    bluffTimer: $("#fib-bluff-timer"),
    inputBluff: $("#input-bluff"),
    btnBluff: $("#btn-bluff"),
    voteRound: $("#fib-vote-round"),
    votePrompt: $("#fib-vote-prompt"),
    voteTimer: $("#fib-vote-timer"),
    voteChoices: $("#fib-vote-choices"),
    truthReveal: $("#fib-truth-reveal"),
    resultsList: $("#fib-results-list"),
    standings: $("#fib-standings"),
  };

  var bluffSubmit = GameApp.wireSubmit(
    els.inputBluff, els.btnBluff,
    function () {
      var bluff = els.inputBluff.value.trim();
      return bluff ? { type: "submit_bluff", bluff: bluff } : null;
    },
    "bluff_received", "fib-bluff-wait"
  );

  GameApp.registerGameModule("fibbage", {
    handleGameState: function (msg) {
      var roundLabel = GameApp.renderRoundLabel(msg);

      switch (msg.state) {
        case "ShowPrompt":
          hasBluffed = false;
          hasVoted = false;
          bluffSubmit.reset();
          els.promptRound.textContent = roundLabel;
          els.promptText.textContent = msg.prompt;
          GameApp.showScreen("fib-prompt");
          break;

        case "WriteBluff":
          if (bluffSubmit.isSubmitted()) {
            GameApp.showScreen("fib-bluff-wait");
          } else {
            els.bluffRound.textContent = roundLabel;
            els.bluffPrompt.textContent = msg.prompt;
            bluffSubmit.reset();
            GameApp.showScreen("fib-bluff");
            GameApp.startTimer(msg.timer, els.bluffTimer);
            els.inputBluff.focus();
          }
          break;

        case "Vote":
          if (hasVoted) {
            GameApp.showScreen("fib-vote-wait");
          } else {
            els.voteRound.textContent = roundLabel;
            els.votePrompt.textContent = msg.prompt;
            GameApp.renderChoiceList(els.voteChoices, msg.choices || [], {
              getLabel: function (c) { return c.text; },
              onSelect: function (c, i) {
                hasVoted = true;
                GameApp.sendMessage({ type: "fibbage_vote", choiceIndex: i });
              },
            });
            GameApp.showScreen("fib-vote");
            GameApp.startTimer(msg.timer, els.voteTimer);
          }
          break;

        case "RoundResults":
          renderRoundResults(msg);
          GameApp.showScreen("fib-results");
          break;

        case "GameOver":
          GameApp.renderStandings(els.standings, msg.players || []);
          GameApp.showScreen("fib-gameover");
          break;
      }
    },

    handleMessage: function (msg) {
      if (bluffSubmit.handleAck(msg.type)) { hasBluffed = true; return; }
      if (msg.type === "fibbage_vote_received") {
        hasVoted = true;
        GameApp.showScreen("fib-vote-wait");
      }
    },

    cleanup: function () {
      hasBluffed = false;
      hasVoted = false;
      bluffSubmit.reset();
      GameApp.stopTimer();
    },
  });

  function renderRoundResults(msg) {
    var truth = "";
    if (msg.choices) {
      for (var i = 0; i < msg.choices.length; i++) {
        if (msg.choices[i].isTruth) { truth = msg.choices[i].text; break; }
      }
    }
    els.truthReveal.textContent = "The truth: " + truth;

    els.resultsList.innerHTML = "";
    (msg.results || []).forEach(function (r) {
      var row = document.createElement("div");
      row.className = "result-row";
      if (r.pickedTruth) row.classList.add("winner");
      var detail = r.pickedTruth ? "Found the truth!" : "Fooled " + r.fooledCount + " player(s)";
      row.innerHTML =
        "<div>" +
          '<div class="result-name">' + GameApp.escapeHtml(r.name) + "</div>" +
          '<div class="fib-result-bluff">"' + GameApp.escapeHtml(r.bluff || "—") + '"</div>' +
          '<div class="fib-result-detail">' + detail + "</div>" +
        "</div>" +
        '<div class="result-score">+' + r.pointsThisRound + "</div>";
      els.resultsList.appendChild(row);
    });
  }
})();
