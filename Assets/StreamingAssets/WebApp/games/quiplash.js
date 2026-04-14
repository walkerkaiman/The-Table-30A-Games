(function () {
  "use strict";

  var $ = GameApp.$;
  var state = GameApp.state;

  var hasAnswered = false;
  var hasVoted = false;

  var els = {
    promptRound: $("#prompt-round"),
    promptText: $("#prompt-text"),
    answerRound: $("#answer-round"),
    answerPrompt: $("#answer-prompt"),
    answerTimer: $("#answer-timer"),
    inputAnswer: $("#input-answer"),
    btnAnswer: $("#btn-answer"),
    voteRound: $("#vote-round"),
    votePrompt: $("#vote-prompt"),
    voteTimer: $("#vote-timer"),
    voteAnswers: $("#vote-answers"),
    resultsList: $("#results-list"),
    finalStandings: $("#final-standings"),
  };

  var answerSubmit = GameApp.wireSubmit(
    els.inputAnswer, els.btnAnswer,
    function () {
      var answer = els.inputAnswer.value.trim();
      return answer ? { type: "submit_answer", answer: answer } : null;
    },
    "answer_received", "answer-wait"
  );

  GameApp.registerGameModule("quiplash", {
    handleGameState: function (msg) {
      var roundLabel = GameApp.renderRoundLabel(msg);

      switch (msg.state) {
        case "ShowPrompt":
          hasAnswered = false;
          hasVoted = false;
          answerSubmit.reset();
          els.promptRound.textContent = roundLabel;
          els.promptText.textContent = msg.prompt;
          GameApp.showScreen("prompt");
          break;

        case "Answer":
          if (answerSubmit.isSubmitted()) {
            GameApp.showScreen("answer-wait");
          } else {
            els.answerRound.textContent = roundLabel;
            els.answerPrompt.textContent = msg.prompt;
            answerSubmit.reset();
            GameApp.showScreen("answer");
            GameApp.startTimer(msg.timer, els.answerTimer);
            els.inputAnswer.focus();
          }
          break;

        case "Voting":
          if (hasVoted) {
            GameApp.showScreen("vote-wait");
          } else {
            els.voteRound.textContent = roundLabel;
            els.votePrompt.textContent = msg.prompt;
            GameApp.renderChoiceList(els.voteAnswers, msg.answers || [], {
              getLabel: function (a) { return a.text; },
              isOwn: function (a) { return a.id === state.playerId; },
              onSelect: function (a) {
                hasVoted = true;
                GameApp.sendMessage({ type: "vote", answerId: a.id });
              },
            });
            GameApp.showScreen("voting");
            GameApp.startTimer(msg.timer, els.voteTimer);
          }
          break;

        case "RoundResults":
          renderRoundResults(msg.results || []);
          GameApp.showScreen("results");
          break;

        case "GameOver":
          GameApp.renderStandings(els.finalStandings, msg.players || []);
          GameApp.showScreen("gameover");
          break;
      }
    },

    handleMessage: function (msg) {
      if (answerSubmit.handleAck(msg.type)) { hasAnswered = true; return; }
      if (msg.type === "vote_received") {
        hasVoted = true;
        GameApp.showScreen("vote-wait");
      }
    },

    cleanup: function () {
      hasAnswered = false;
      hasVoted = false;
      answerSubmit.reset();
      GameApp.stopTimer();
    },
  });

  function renderRoundResults(results) {
    els.resultsList.innerHTML = "";
    results.forEach(function (r, idx) {
      var row = document.createElement("div");
      row.className = "result-row";
      if (idx === 0 && r.votes > 0) row.classList.add("winner");
      row.innerHTML =
        "<div>" +
          '<div class="result-name">' + GameApp.escapeHtml(r.name) + "</div>" +
          '<div class="result-answer">"' + GameApp.escapeHtml(r.answer) + '"</div>' +
        "</div><div>" +
          '<div class="result-score">+' + r.score + "</div>" +
          '<div class="result-votes">' + r.votes + (r.votes === 1 ? " vote" : " votes") + "</div>" +
        "</div>";
      els.resultsList.appendChild(row);
    });
  }
})();
