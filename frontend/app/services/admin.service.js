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

  self.getDisputes = function () {
    return $http.get(API_URL + '/admin/disputes', h());
  };

  self.resolveDispute = function (bookingId) {
    return $http.patch(API_URL + '/admin/disputes/' + bookingId + '/resolve', {}, h());
  };

  self.getArchivedDisputes = function () {
    return $http.get(API_URL + '/admin/disputes/archive', h());
  };

  self.getRemarkDisputes = function () {
    return $http.get(API_URL + '/admin/remark-disputes', h());
  };

  self.resolveRemarkDispute = function (id, approve) {
    return $http.patch(API_URL + '/admin/remark-disputes/' + id + '/resolve', { approve: approve }, h());
  };

  self.getArchivedRemarkDisputes = function () {
    return $http.get(API_URL + '/admin/remark-disputes/archive', h());
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

  // ── Platform fees (Admin → Platform Fees) ─────────────────────────────
  // Markup is charged to the parent on top of the base price; first-match commission is
  // charged to the tutor out of it. Both live on the same settings row.
  self.getPlatformFees = function () {
    return $http.get(API_URL + '/admin/commission', h());
  };

  self.updatePlatformFees = function (fees) {
    return $http.put(API_URL + '/admin/commission', fees, h());
  };

  // ── Promotional credit (Admin → Promotional Credit) ───────────────────
  // Every tutor with both balances, for the credit and adjustment pickers.
  self.getAllTutors = function () {
    return $http.get(API_URL + '/admin/tutors/all', h());
  };

  // Every parent with their wallet balance, for the wallet-grant picker.
  self.getAllParents = function () {
    return $http.get(API_URL + '/admin/parents', h());
  };

  self.grantCredit = function (tutorId, amount, reason, expiresAt) {
    return $http.post(API_URL + '/admin/credit/grant', {
      tutorId: tutorId, amount: amount, reason: reason, expiresAt: expiresAt || null
    }, h());
  };

  // Grants credit to the earliest-registered tutors who don't already hold a grant.
  // Re-runnable: anyone already granted is skipped rather than topped up twice.
  self.runCampaign = function (amount, tutorLimit, reason) {
    return $http.post(API_URL + '/admin/credit/campaign', {
      amount: amount, tutorLimit: tutorLimit, reason: reason
    }, h());
  };

  self.grantWalletCredit = function (parentUserId, amount, reason, expiresAt) {
    return $http.post(API_URL + '/admin/wallet/grant', {
      parentUserId: parentUserId, amount: amount, reason: reason, expiresAt: expiresAt || null
    }, h());
  };

  // Signed manual correction to a tutor's withdrawable balance; negative claws back.
  self.adjustLedger = function (tutorId, amount, reason) {
    return $http.post(API_URL + '/admin/ledger/adjust', {
      tutorId: tutorId, amount: amount, reason: reason
    }, h());
  };

  // ── Payout batches (Admin → Payout Batches) ───────────────────────────
  // The month-end cutoff: works out what every tutor is owed and groups it into one
  // batch for review. Period is "yyyy-MM"; omit it for the current month.
  self.runCutoff = function (period) {
    return $http.post(API_URL + '/admin/payouts/cutoff', { period: period || null }, h());
  };

  self.getPayoutBatches = function () {
    return $http.get(API_URL + '/admin/payouts/batches', h());
  };

  self.getPayoutBatch = function (id) {
    return $http.get(API_URL + '/admin/payouts/batches/' + id, h());
  };

  self.approvePayoutBatch = function (id, notes) {
    return $http.post(API_URL + '/admin/payouts/batches/' + id + '/approve', { notes: notes || null }, h());
  };

  // Records that the bank transfer went out — this is when the tutors' ledgers are debited.
  self.transferPayoutBatch = function (id, notes) {
    return $http.post(API_URL + '/admin/payouts/batches/' + id + '/transfer', { notes: notes || null }, h());
  };

  self.cancelPayoutBatch = function (id, notes) {
    return $http.post(API_URL + '/admin/payouts/batches/' + id + '/cancel', { notes: notes || null }, h());
  };
}]);
