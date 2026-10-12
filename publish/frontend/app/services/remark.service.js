'use strict';

angular.module('learnSphereApp')
.service('RemarkService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;
  var h = function () { return { headers: AuthService.authHeader() }; };

  // Parent — create/edit/delete a remark on a specific completed class instance.
  self.create = function (bookingClassId, data) {
    return $http.post(API_URL + '/bookingclasses/' + bookingClassId + '/remarks', data, h());
  };

  self.update = function (remarkId, data) {
    return $http.put(API_URL + '/remarks/' + remarkId, data, h());
  };

  self.remove = function (remarkId) {
    return $http.delete(API_URL + '/remarks/' + remarkId, h());
  };

  // Parent — like a published remark (once, no unlike).
  self.like = function (remarkId) {
    return $http.post(API_URL + '/remarks/' + remarkId + '/like', {}, h());
  };

  // Tutor — request a published remark be hidden.
  self.dispute = function (remarkId, reason) {
    return $http.post(API_URL + '/remarks/' + remarkId + '/dispute', { reason: reason }, h());
  };

  // Any logged-in parent — published remarks for a tutor (catalog, AI Speed Match).
  self.getForTutor = function (tutorId) {
    return $http.get(API_URL + '/tutors/' + tutorId + '/remarks', h());
  };

  // Tutor — every remark regardless of status, for their own Bulletin Board.
  self.getMineForTutor = function (tutorId) {
    return $http.get(API_URL + '/tutors/' + tutorId + '/remarks/mine', h());
  };
}]);
