'use strict';

// Parent wallet: non-withdrawable credit that can settle any LearnSphere invoice.
//
// Distinct from PayoutService, which is the TUTOR's money. The two are deliberately
// separate services because they are separate ledgers with different rules — a parent's
// credit can never be cashed out, and a tutor's withdrawable balance is never spendable
// on an invoice.
angular.module('learnSphereApp')
.service('WalletService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;

  var h = function () { return { headers: AuthService.authHeader() }; };

  // { available, expiringSoon, nextExpiryAt }
  self.getBalance = function () {
    return $http.get(API_URL + '/wallet/balance', h());
  };

  // Every entry behind the balance, newest first — what makes the number explainable.
  self.getStatement = function () {
    return $http.get(API_URL + '/wallet/statement', h());
  };

  // Spend credit on an unpaid invoice. Omitting amount (or passing 0) means "cover as
  // much of this bill as the wallet can". Returns { applied, settled, cashDue,
  // invoiceStatus } — when settled is true the invoice is fully paid and needs no card.
  self.apply = function (invoiceId, amount) {
    return $http.post(API_URL + '/wallet/apply', {
      invoiceId: invoiceId,
      amount: amount || 0
    }, h());
  };
}]);
