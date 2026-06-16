'use strict';

angular.module('learnSphereApp')
.service('AdminService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;
  var h = function () { return { headers: AuthService.authHeader() }; };

  self.getStats = function () {
    return $http.get(API_URL + '/admin/stats', h());
  };

  self.getUnverifiedTutors = function () {
    return $http.get(API_URL + '/admin/tutors/unverified', h());
  };

  self.verifyTutor = function (id) {
    return $http.patch(API_URL + '/admin/tutors/' + id + '/verify', {}, h());
  };

  self.getDisputes = function () {
    return $http.get(API_URL + '/admin/disputes', h());
  };

  self.resolveDispute = function (bookingId) {
    return $http.patch(API_URL + '/admin/disputes/' + bookingId + '/resolve', {}, h());
  };

  self.getInstitutions = function (params) {
    return $http.get(API_URL + '/admin/institutions', { params: params });
  };
}]);
