(function () {
  "use strict";

  var $ = GameApp.$;
  var state = GameApp.state;
  var hasVoted = false;

  var els = {
    imageRound: $("#cc-image-round"),
    imageDisplay: $("#cc-image-display"),
    captionRound: $("#cc-caption-round"),
    captionImage: $("#cc-caption-image"),
    captionTimer: $("#cc-caption-timer"),
    inputCaption: $("#input-caption"),
    btnCaption: $("#btn-caption"),
    voteRound: $("#cc-vote-round"),
    voteImage: $("#cc-vote-image"),
    voteTimer: $("#cc-vote-timer"),
    voteCaptions: $("#cc-vote-captions"),
    resultsList: $("#cc-results-list"),
    standings: $("#cc-standings"),
  };

  var captionSubmit = GameApp.wireSubmit(
    els.inputCaption, els.btnCaption,
    function () {
      var caption = els.inputCaption.value.trim();
      return caption ? { type: "submit_caption", caption: caption } : null;
    },
    "caption_received", "cc-caption-wait"
  );

  GameApp.registerGameModule("caption_contest", {
    handleGameState: function (msg) {
      var roundLabel = GameApp.renderRoundLabel(msg);
      var imgSrc = msg.imageUrl || "";

      switch (msg.state) {
        case "ShowImage":
          hasVoted = false;
          captionSubmit.reset();
          els.imageRound.textContent = roundLabel;
          if (els.imageDisplay) els.imageDisplay.src = imgSrc;
          GameApp.showScreen("cc-image");
          break;

        case "WriteCaption":
          if (captionSubmit.isSubmitted()) {
            GameApp.showScreen("cc-caption-wait");
          } else {
            els.captionRound.textContent = roundLabel;
            if (els.captionImage) els.captionImage.src = imgSrc;
            captionSubmit.reset();
            GameApp.showScreen("cc-caption");
            GameApp.startTimer(msg.timer, els.captionTimer);
            els.inputCaption.focus();
          }
          break;

        case "Vote":
          if (hasVoted) {
            GameApp.showScreen("cc-vote-wait");
          } else {
            els.voteRound.textContent = roundLabel;
            if (els.voteImage) els.voteImage.src = imgSrc;
            GameApp.renderChoiceList(els.voteCaptions, msg.captions || [], {
              getLabel: function (c) { return c.text; },
              isOwn: function (c) { return c.id === state.playerId; },
              onSelect: function (c) {
                hasVoted = true;
                GameApp.sendMessage({ type: "caption_vote", captionId: c.id });
              },
            });
            GameApp.showScreen("cc-vote");
            GameApp.startTimer(msg.timer, els.voteTimer);
          }
          break;

        case "RoundResults":
          renderResults(msg.results || []);
          GameApp.showScreen("cc-results");
          break;

        case "GameOver":
          GameApp.renderStandings(els.standings, msg.players || []);
          GameApp.showScreen("cc-gameover");
          break;
      }
    },

    handleMessage: function (msg) {
      if (captionSubmit.handleAck(msg.type)) return;
      if (msg.type === "caption_vote_received") {
        hasVoted = true;
        GameApp.showScreen("cc-vote-wait");
      }
    },

    cleanup: function () {
      hasVoted = false;
      captionSubmit.reset();
      GameApp.stopTimer();
    },
  });

  function renderResults(data) {
    els.resultsList.innerHTML = "";
    data.forEach(function (r, idx) {
      var row = document.createElement("div");
      row.className = "result-row";
      if (idx === 0 && r.votes > 0) row.classList.add("winner");
      row.innerHTML =
        "<div>" +
          '<div class="result-name">' + GameApp.escapeHtml(r.name) + "</div>" +
          '<div class="result-answer">"' + GameApp.escapeHtml(r.caption) + '"</div>' +
        "</div><div>" +
          '<div class="result-score">+' + r.score + "</div>" +
          '<div class="result-votes">' + r.votes + (r.votes === 1 ? " vote" : " votes") + "</div>" +
        "</div>";
      els.resultsList.appendChild(row);
    });
  }
})();
