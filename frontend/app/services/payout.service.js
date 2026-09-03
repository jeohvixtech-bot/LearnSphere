'use strict';

// Tutor wallet + payouts. The balance comes from the server-side ledger
// (TutorLedgerEntry) rather than being summed in the browser, so the figure shown here is
// the same one a payout request is validated against — there is no second definition of
// "balance" that could drift from the authoritative one.
angular.module('learnSphereApp')
.service('PayoutService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;

  var h = function () { return { headers: AuthService.authHeader() }; };

  // { withdrawable, credit, total } — withdrawable is cashable, credit is platform value
  // that can offset charges but never be withdrawn.
  self.getBalance = function () {
    return $http.get(API_URL + '/payouts/balance', h());
  };

  // Every entry behind the balance, newest first — what makes the number explainable.
  self.getStatement = function () {
    return $http.get(API_URL + '/payouts/statement', h());
  };

  self.getAll = function () {
    return $http.get(API_URL + '/payouts', h());
  };

  self.request = function (amount) {
    return $http.post(API_URL + '/payouts', { amount: amount }, h());
  };
}]);
