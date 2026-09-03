'use strict';

// Talks to the HitPay-backed payment endpoints (see PaymentsController). Nothing here
// ever handles card details — the parent is sent to HitPay's own hosted checkout page,
// so no payment credentials pass through this app at all.
angular.module('learnSphereApp')
.service('PaymentService', ['$http', '$q', 'API_URL', 'AuthService', function ($http, $q, API_URL, AuthService) {
  var self = this;

  var h = function () { return { headers: AuthService.authHeader() }; };

  // Whether a usable gateway is armed. Cached for the life of the page: it's read on
  // every payment click and only an admin can change it, so re-fetching per click would
  // add a round-trip to the critical path for no practical benefit.
  var configPromise = null;

  self.getConfig = function () {
    if (!configPromise) {
      configPromise = $http.get(API_URL + '/payments/config', h())
        .then(function (res) { return res.data; })
        .catch(function () {
          // Treat an unreachable config endpoint as "gateway off" so payment falls back
          // to the legacy path rather than blocking entirely. The backend still refuses
          // the legacy path whenever the gateway really is armed, so this cannot be used
          // to slip past a live gateway.
          return { gatewayEnabled: false, currency: 'SGD', mode: 'sandbox' };
        });
    }
    return configPromise;
  };

  // Drops the cached config — call after an admin saves new settings so the next payment
  // sees them without a full page reload.
  self.clearConfigCache = function () { configPromise = null; };

  // Creates a HitPay payment request and returns { checkoutUrl, paymentRequestId, ... }.
  // The invoice is NOT marked paid here; only a confirmed completion does that.
  self.startCheckout = function (invoiceId) {
    return $http.post(API_URL + '/payments/invoices/' + invoiceId + '/checkout', {}, h())
      .then(function (res) { return res.data; });
  };

  // Re-asks the server (which re-asks HitPay) where a payment actually stands. Safe to
  // call repeatedly — used on return from checkout and by the "check again" action.
  self.getStatus = function (invoiceId) {
    return $http.get(API_URL + '/payments/invoices/' + invoiceId + '/status', h())
      .then(function (res) { return res.data; });
  };

  // Sends the browser to HitPay's hosted checkout. Kept here rather than in the
  // controller so every caller leaves the app the same way.
  self.redirectToCheckout = function (checkoutUrl) {
    window.location.href = checkoutUrl;
  };
}]);
