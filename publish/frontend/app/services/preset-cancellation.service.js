'use strict';

angular.module('learnSphereApp')
.service('PresetCancellationService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;
  var h = function () { return { headers: AuthService.authHeader() }; };

  self.getMine = function () {
    return $http.get(API_URL + '/preset-cancellations/mine', h());
  };

  self.accept = function (decisionId) {
    return $http.post(API_URL + '/preset-cancellations/' + decisionId + '/accept', {}, h());
  };

  self.reject = function (decisionId) {
    return $http.post(API_URL + '/preset-cancellations/' + decisionId + '/reject', {}, h());
  };

  self.acknowledge = function (decisionId) {
    return $http.post(API_URL + '/preset-cancellations/' + decisionId + '/acknowledge', {}, h());
  };

  // Admin queue (see AdminController.GetPendingCancellations/ResolvePresetCancellation)
  self.getAdminQueue = function () {
    return $http.get(API_URL + '/admin/preset-cancellations', h());
  };

  self.resolveAdmin = function (decisionId, adminNote) {
    return $http.post(API_URL + '/admin/preset-cancellations/' + decisionId + '/resolve', { adminNote: adminNote }, h());
  };
}]);
