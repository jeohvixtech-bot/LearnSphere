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
  self.setTutor = function (id, groupId, studId) { tutorId = id; presetGroupId = groupId || null; studentId = studId || null; };
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
}]);
