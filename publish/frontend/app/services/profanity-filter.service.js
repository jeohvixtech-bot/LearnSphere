'use strict';

// Shared profanity check for free-text fields (chat, reviews, lesson reports, bio,
// learning goals, booking/counter-proposal messages, issue reports, admin notes) —
// deliberately separate from NameValidationService's character whitelist, since
// free text needs to allow digits and normal punctuation. Mirrored server-side in
// Services/ProfanityFilter.cs; kept in sync manually.
angular.module('learnSphereApp')
.service('ProfanityFilterService', function () {
  var self = this;

  var PROFANITY_WORDS = [
    'fuck', 'shit', 'bitch', 'bastard', 'cunt', 'dick', 'piss', 'pussy', 'cock', 'slut', 'whore',
    'asshole', 'nigger', 'nigga', 'fag', 'faggot', 'retard', 'rape', 'rapist', 'porn', 'sex',
    'damn', 'hell', 'crap', 'douche', 'wanker', 'twat', 'prick', 'skank'
  ];

  // Word-boundary match, not the name-check's split-on-punctuation approach — free
  // text has commas, exclamation marks, parentheses, etc. as word boundaries too.
  var PATTERN = new RegExp('\\b(' + PROFANITY_WORDS.join('|') + ')\\b', 'i');

  self.containsProfanity = function (text) {
    return !!text && PATTERN.test(text);
  };

  // Returns an error string, or '' if clean.
  self.validate = function (text) {
    return self.containsProfanity(text) ? 'Please remove inappropriate language before submitting.' : '';
  };
});
