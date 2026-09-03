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

  // Payment gateway config (Admin → Payment Gateway). The GET never returns the API key
  // or salt themselves — only masked hints and "is one saved" flags.
  self.getPaymentGateway = function () {
    return $http.get(API_URL + '/admin/payment-gateway', h());
  };

  // Leave apiKey/salt blank to keep whatever is already stored.
  self.updatePaymentGateway = function (settings) {
    return $http.put(API_URL + '/admin/payment-gateway', settings, h());
  };

  // Platform commission rate (Admin → Platform Commission).
  self.getCommission = function () {
    return $http.get(API_URL + '/admin/commission', h());
  };

  self.updateCommission = function (ratePercent) {
    return $http.put(API_URL + '/admin/commission', { ratePercent: ratePercent }, h());
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

  // decisions: [{ docId, status, note }, ...] — every currently-pending document
  // for this tutor must be covered. Applies all of them atomically and sends one
  // combined email. Replaces the old reviewDocument (per-doc, applied immediately)
  // + confirmVerification + rejectVerification split — see
  // TutorsController.ApplyVerificationDecisions.
  self.applyVerificationDecisions = function (tutorId, decisions) {
    return $http.post(
      API_URL + '/tutors/' + tutorId + '/apply-verification-decisions',
      { decisions: decisions },
      h()
    );
  };

  self.adminRemoveDocument = function (tutorId, docId) {
    return $http.delete(API_URL + '/tutors/' + tutorId + '/documents/' + docId + '/admin-remove', h());
  };
}]);
