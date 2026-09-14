'use strict';

angular.module('learnSphereApp')
.controller('ChangePasswordCtrl', ['$location', 'AuthService', function ($location, AuthService) {
  var self = this;
  var user = AuthService.getCurrentUser();
  self.user = user;

  self.form = { currentPassword: '', newPassword: '', confirmPassword: '' };
  self.errorMsg = '';
  self.loading = false;

  self.submit = function () {
    self.errorMsg = '';
    if (self.form.newPassword.length < 8) {
      self.errorMsg = 'New password must be at least 8 characters.';
      return;
    }
    if (self.form.newPassword !== self.form.confirmPassword) {
      self.errorMsg = 'Passwords do not match.';
      return;
    }
    self.loading = true;
    AuthService.changePassword(self.form.currentPassword, self.form.newPassword)
      .then(function () {
        if (user.role === 'parent' || user.role === 'student') $location.path('/parent/dashboard');
        else if (user.role === 'tutor') $location.path('/tutor/overview');
        else if (user.role === 'admin') $location.path('/admin/overview');
        else $location.path('/welcome');
      })
      .catch(function (err) {
        self.errorMsg = (err.data && err.data.message) || 'Could not change password. Please try again.';
      })
      .finally(function () {
        self.loading = false;
      });
  };

  self.logout = function () {
    AuthService.logout();
    $location.path('/welcome');
  };
}]);
