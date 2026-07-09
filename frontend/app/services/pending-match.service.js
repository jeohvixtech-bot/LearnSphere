'use strict';

angular.module('learnSphereApp')
.service('PendingMatchService', [function () {
  var self = this;
  var tutorId = null;

  self.setTutor = function (id) { tutorId = id; };
  self.consumeTutor = function () {
    var id = tutorId;
    tutorId = null;
    return id;
  };
}]);
