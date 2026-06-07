'use strict';

angular.module('learnSphereApp')
.controller('AdminCtrl', ['$location', '$timeout', 'AuthService', 'AdminService',
function ($location, $timeout, AuthService, AdminService) {
  var self = this;
  self.user = AuthService.getCurrentUser();

  self.stats = null;
  self.unverifiedTutors = [];
  self.disputes = [];
  self.systemLogs = [];

  function init() {
    AdminService.getStats().then(function (res) { self.stats = res.data; });
    AdminService.getUnverifiedTutors().then(function (res) { self.unverifiedTutors = res.data; });
    AdminService.getDisputes().then(function (res) { self.disputes = res.data; });
  }
  init();

  self.verifyTutor = function (tutor) {
    AdminService.verifyTutor(tutor.id).then(function () {
      tutor.isVerified = true;
      self.unverifiedTutors = self.unverifiedTutors.filter(function (t) { return t.id !== tutor.id; });
      self.systemLogs.unshift('Approved tutor: ' + tutor.name + ' (Just now)');
      AdminService.getStats().then(function (res) { self.stats = res.data; });
    });
  };

  self.resolveDispute = function (dispute) {
    AdminService.resolveDispute(dispute.id).then(function () {
      self.disputes = self.disputes.filter(function (d) { return d.id !== dispute.id; });
      self.systemLogs.unshift('Conflict resolved for class: #' + dispute.id + ' (Just now)');
    });
  };
}]);
