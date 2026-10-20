'use strict';

// Shown after sign-in when the current Terms version is newer than the one the user accepted.
angular.module('learnSphereApp')
.controller('AcceptTermsCtrl', ['$location', 'AuthService', 'TermsService', function ($location, AuthService, TermsService) {
  var self = this;
  self.user = AuthService.getCurrentUser();
  self.terms = null;
  self.agreed = false;
  self.loading = true;
  self.saving = false;
  self.errorMsg = '';

  TermsService.getCurrent().then(function (t) {
    self.terms = t;
  }).catch(function () {
    self.errorMsg = 'Could not load the Terms and Conditions. Please reload the page.';
  }).finally(function () {
    self.loading = false;
  });

  self.accept = function () {
    if (!self.agreed || !self.terms || self.saving) return;
    self.errorMsg = '';
    self.saving = true;
    TermsService.accept(self.terms.id).then(function () {
      AuthService.markTermsAccepted();
      if (self.user.mustChangePassword) $location.path('/change-password');
      else if (self.user.role === 'admin') $location.path('/admin/overview');
      else if (self.user.role === 'tutor') $location.path('/tutor/overview');
      else $location.path('/parent/dashboard');
    }).catch(function (err) {
      self.errorMsg = (err.data && err.data.message) || 'Could not save your acceptance. Please try again.';
    }).finally(function () {
      self.saving = false;
    });
  };

  self.logout = function () {
    AuthService.logout();
    $location.path('/welcome');
  };
}]);
