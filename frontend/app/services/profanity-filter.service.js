'use strict';

// Shared profanity check for free-text fields (chat, reviews, lesson reports, bio,
// learning goals, booking/counter-proposal messages, issue reports, admin notes) —
// deliberately separate from NameValidationService's character whitelist, since
// free text needs to allow digits and normal punctuation. Mirrored server-side in
// Services/ProfanityFilter.cs; kept in sync manually — PROFANITY_WORDS,
// LETTER_CLASSES, SUFFIXES and EXCEPTIONS must match there exactly.
angular.module('learnSphereApp')
.service('ProfanityFilterService', function () {
  var self = this;

  var PROFANITY_WORDS = [
    'fuck', 'shit', 'bitch', 'bastard', 'cunt', 'dick', 'piss', 'pussy', 'cock', 'slut', 'whore',
    'asshole', 'nigger', 'nigga', 'fag', 'faggot', 'retard', 'rape', 'rapist', 'porn', 'sex',
    'damn', 'hell', 'crap', 'douche', 'wanker', 'twat', 'prick', 'skank'
  ];

  // Common look-alike substitutions (f*ck, sh1t, b!tch, a$$hole, fvck). Letters not
  // listed only match themselves.
  var LETTER_CLASSES = {
    a: 'a4@*', e: 'e3*', i: 'i1!|*', o: 'o0*', u: 'uv*', s: 's5$', t: 't7'
  };

  // Inflections/compounds accepted after a listed word (fucking, shitty, bitches,
  // dickhead, douchebag). Doubled letters like shi-tt-y are covered by the per-letter
  // repeat below, not by listing "ty".
  var SUFFIXES = [
    's', 'es', 'ed', 'er', 'ers', 'ing', 'in', 'y', 'ies', 'head', 'heads',
    'face', 'hole', 'holes', 'bag', 'bags'
  ];

  // Innocent words the suffix/repeat rules would otherwise catch.
  var EXCEPTIONS = ['pricked', 'pricking', 'cocky', 'cocker', 'cockers'];

  function escapeRegex(s) { return s.replace(/[.*+?^${}()|[\]\\\-]/g, '\\$&'); }

  function letterPattern(c) {
    return LETTER_CLASSES[c] ? '[' + escapeRegex(LETTER_CLASSES[c]) + ']+' : escapeRegex(c) + '+';
  }

  // Each letter may repeat (fuckk, fuuuck, shiiit) and may be a look-alike, optionally
  // followed by one suffix. Boundaries are "no letter/digit on either side" rather
  // than \b, since look-alikes like @ $ ! * aren't word characters — and so words
  // merely containing a listed one (class, Sussex, shell, Scunthorpe) stay clean.
  var PATTERN_SOURCE = '(?<![a-z0-9])(?:' +
    PROFANITY_WORDS.map(function (w) { return w.split('').map(letterPattern).join(''); }).join('|') +
    ')(?:' + SUFFIXES.join('|') + ')?(?![a-z0-9])';

  self.containsProfanity = function (text) {
    if (!text) return false;
    var re = new RegExp(PATTERN_SOURCE, 'gi');
    var m;
    while ((m = re.exec(text)) !== null) {
      if (EXCEPTIONS.indexOf(m[0].toLowerCase()) === -1) return true;
    }
    return false;
  };

  // Returns an error string, or '' if clean.
  self.validate = function (text) {
    return self.containsProfanity(text) ? 'Please remove inappropriate language before submitting.' : '';
  };
});
