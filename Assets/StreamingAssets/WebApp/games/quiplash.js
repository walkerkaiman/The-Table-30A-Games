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

  // ─── Submit answer button ─────────────────
  if (els.btnAnswer) {
    els.btnAnswer.addEventListener("click", function () {
      var answer = els.inputAnswer.value.trim();
      if (!answer) return;
      els.btnAnswer.disabled = true;
      GameApp.sendMessage({ type: "submit_answer", answer: answer });
    });
  }

  if (els.inputAnswer) {
    els.inputAnswer.addEventListener("keydown", function (e) {
      if (e.key === "Enter" && !e.shiftKey) {
        e.preventDefault();
        els.btnAnswer.click();
      }
    });
  }

  // ─── Module ───────────────────────────────
  GameApp.registerGameModule("quiplash", {
    handleGameState: function (msg) {
      var roundLabel = "Round " + msg.round + " of " + msg.totalRounds;

      switch (msg.state) {
        case "ShowPrompt":
          hasAnswered = false;
          hasVoted = false;
          els.promptRound.textContent = roundLabel;
          els.promptText.textContent = msg.prompt;
          GameApp.showScreen("prompt");
          break;

        case "Answer":
          if (hasAnswered) {
            GameApp.showScreen("answer-wait");
          } else {
            els.answerRound.textContent = roundLabel;
            els.answerPrompt.textContent = msg.prompt;
            els.inputAnswer.value = "";
            els.btnAnswer.disabled = false;
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
            renderVotingAnswers(msg.answers || []);
            GameApp.showScreen("voting");
            GameApp.startTimer(msg.timer, els.voteTimer);
          }
          break;

        case "RoundResults":
          renderResults(msg.results || [], msg.players || [], false);
          GameApp.showScreen("results");
          break;

        case "GameOver":
          renderResults(msg.results || [], msg.players || [], true);
          GameApp.showScreen("gameover");
          break;
      }
    },

    handleMessage: function (msg) {
      switch (msg.type) {
        case "answer_received":
          hasAnswered = true;
          GameApp.showScreen("answer-wait");
          break;
        case "vote_received":
          hasVoted = true;
          GameApp.showScreen("vote-wait");
          break;
      }
    },

    cleanup: function () {
      hasAnswered = false;
      hasVoted = false;
      GameApp.stopTimer();
    },
  });

  // ─── Rendering ────────────────────────────
  function renderVotingAnswers(answers) {
    els.voteAnswers.innerHTML = "";
    answers.forEach(function (a) {
      var card = document.createElement("div");
      card.className = "answer-card";
      card.textContent = a.text;
      card.dataset.answerId = a.id;

      if (a.id === state.playerId) {
        card.classList.add("own-answer");
        card.textContent += " (yours)";
      } else {
        card.addEventListener("click", function () {
          if (hasVoted) return;
          els.voteAnswers.querySelectorAll(".answer-card").forEach(function (c) {
            c.classList.remove("selected");
          });
          card.classList.add("selected");
          hasVoted = true;
          GameApp.sendMessage({ type: "vote", answerId: a.id });
        });
      }
      els.voteAnswers.appendChild(card);
    });
  }

  function renderResults(results, players, isFinal) {
    var container = isFinal ? els.finalStandings : els.resultsList;
    container.innerHTML = "";

    if (isFinal) {
      var sorted = (players || []).slice().sort(function (a, b) { return b.score - a.score; });
      sorted.forEach(function (p, idx) {
        var row = document.createElement("div");
        row.className = "result-row rank-" + (idx + 1);
        if (idx === 0) row.classList.add("winner");
        row.innerHTML =
          '<div><span class="rank-number">#' + (idx + 1) + "</span>" +
          '<span class="result-name">' + GameApp.escapeHtml(p.name) + "</span></div>" +
          '<div class="result-score">' + p.score + " pts</div>";
        container.appendChild(row);
      });
    } else {
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
        container.appendChild(row);
      });
    }
  }
})();
