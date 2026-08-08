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

  self.getScoringWeightages = function () {
    return $http.get(API_URL + '/admin/scoring-weightages');
  };

  self.updateScoringWeightages = function (weightages) {
    return $http.put(API_URL + '/admin/scoring-weightages', { weightages: weightages }, h());
  };

  self.getRejectionReasons = function () {
    return $http.get(API_URL + '/tutors/rejection-reasons', h());
  };

  self.reviewDocument = function (tutorId, docId, status, note) {
    return $http.patch(
      API_URL + '/tutors/' + tutorId + '/documents/' + docId + '/review',
      { status: status, note: note },
      h()
    );
  };

  self.confirmVerification = function (tutorId) {
    return $http.post(API_URL + '/tutors/' + tutorId + '/confirm-verification', null, h());
  };

  self.rejectVerification = function (tutorId) {
    return $http.post(API_URL + '/tutors/' + tutorId + '/reject-verification', null, h());
  };
}]);
