'use strict';

// Shared full-name validation — used by registration (auth.controller.js) and
// child profile create/edit (parent.controller.js) so the character rule lives in
// exactly one place. Profanity checking itself is delegated to
// ProfanityFilterService, shared with every other free-text field. Mirrored
// server-side in Services/NameValidator.cs.
angular.module('learnSphereApp')
.service('NameValidationService', ['ProfanityFilterService', function (ProfanityFilterService) {
  var self = this;

  var NAME_PATTERN = /^[\p{L}\s.'-]+$/u;

  // Returns an error string, or '' if the name is valid.
  self.validate = function (rawName) {
    var name = (rawName || '').trim();
    if (!name) return '';
    if (name.length < 2 || name.length > 60) {
      return 'Name must be between 2 and 60 characters.';
    }
    if (!NAME_PATTERN.test(name)) {
      return 'Name can only contain letters, spaces, hyphens, and apostrophes — no numbers or special characters.';
    }
    if (ProfanityFilterService.containsProfanity(name)) {
      return 'Please enter a valid, appropriate name.';
    }
    return '';
  };
}]);
