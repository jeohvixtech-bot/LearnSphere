'use strict';

angular.module('learnSphereApp')
.service('BookingService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;
  var h = function () { return { headers: AuthService.authHeader() }; };

  self.getAll = function () {
    return $http.get(API_URL + '/bookings', h());
  };

  self.create = function (data) {
    return $http.post(API_URL + '/bookings', data, h());
  };

  self.updateStatus = function (id, status, counterProposal) {
    return $http.patch(API_URL + '/bookings/' + id + '/status', {
      status: status,
      counterProposal: counterProposal || null
    }, h());
  };

  self.submitLessonReport = function (id, data) {
    return $http.post(API_URL + '/bookings/' + id + '/lesson-report', data, h());
  };

  self.editLessonReport = function (id, data) {
    return $http.patch(API_URL + '/bookings/' + id + '/lesson-report', data, h());
  };

  self.reportIssue = function (id, data) {
    return $http.post(API_URL + '/bookings/' + id + '/issue', data, h());
  };
}]);
