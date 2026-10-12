'use strict';

angular.module('learnSphereApp')
.service('PendingMatchService', [function () {
  var self = this;
  var tutorId = null;
  var presetGroupId = null;
  var studentId = null;

  // presetGroupId/studentId are optional — carry a chip selection and/or the
  // child a match was run for (e.g. from AI Speed Match) across the route change
  // into ParentCtrl.init() on the search page, so selectTutor() there can pick
  // straight up at the booking summary for the RIGHT child instead of quietly
  // defaulting to whichever child happens to be first in the parent's list.
  // Omit both for a plain "go book this tutor" jump.
  // Separate from tutorId/presetGroupId/studentId above — those are one-shot
  // (consumeTutor() etc. drain them the moment ParentCtrl.init() reads them,
  // for the AI Speed Match "View & Book" hand-off, which should only ever
  // auto-open once). pinnedTutorId instead just marks "this tutor's card
  // should show pinned/highlighted on the catalog" — it deliberately does NOT
  // get cleared on read, so it survives every ParentCtrl re-instantiation
  // (every /parent/* route change re-creates the controller) until the user
  // actually logs out (see AuthService.logout). Mirrored from setTutor() so
  // the welcome page's bare setTutor(id) call (see WelcomeCtrl.goToLogin)
  // needs no changes of its own to participate in both.
  var pinnedTutorId = null;

  self.setTutor = function (id, groupId, studId) {
    tutorId = id; presetGroupId = groupId || null; studentId = studId || null;
    pinnedTutorId = id;
  };
  self.hasPendingTutor = function () { return tutorId !== null; };
  self.consumeTutor = function () {
    var id = tutorId;
    tutorId = null;
    return id;
  };
  self.consumePresetGroupId = function () {
    var g = presetGroupId;
    presetGroupId = null;
    return g;
  };
  self.consumeStudentId = function () {
    var s = studentId;
    studentId = null;
    return s;
  };

  self.getTutor = function () { return pinnedTutorId; };
  self.clear = function () { pinnedTutorId = null; };
}]);
