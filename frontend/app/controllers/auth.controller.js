'use strict';

angular.module('learnSphereApp')
.controller('AuthCtrl', ['$location', 'AuthService', function ($location, AuthService) {
  var self = this;

  // Redirect if already logged in
  if (AuthService.isLoggedIn()) {
    var user = AuthService.getCurrentUser();
    redirectByRole(user.role);
  }

  self.mode = 'login';  // login | register
  self.loginData  = { email: 'sarah.tan@example.com', password: 'Parent@123' };
  self.registerData = { email: '', password: '', confirmPassword: '', name: '', role: 'parent', agreedToTerms: false };
  self.showTerms = false;
  self.errorMsg = '';
  self.loading = false;

  self.login = function () {
    self.errorMsg = '';
    self.loading = true;
    AuthService.login(self.loginData.email, self.loginData.password)
      .then(function (user) {
        if (!user || !user.role) {
          self.errorMsg = 'Unexpected response from server. Please try again.';
          return;
        }
        redirectByRole(user.role);
      })
      .catch(function (err) {
        if (err && err.status === 401) {
          self.errorMsg = 'Invalid email or password.';
        } else if (err && err.status === 0) {
          self.errorMsg = 'Cannot reach server. Is the backend running?';
        } else if (err && err.status) {
          self.errorMsg = 'Server error (' + err.status + '). Please try again.';
        } else {
          self.errorMsg = 'Login error: ' + (err && err.message ? err.message : 'unknown');
        }
      })
      .finally(function () {
        self.loading = false;
      });
  };

  self.register = function () {
    self.errorMsg = '';
    if (self.registerData.password !== self.registerData.confirmPassword) {
      self.errorMsg = 'Passwords do not match.';
      return;
    }
    self.loading = true;
    AuthService.register(
      self.registerData.email,
      self.registerData.password,
      self.registerData.name,
      self.registerData.role
    ).then(function (user) {
      redirectByRole(user.role);
    }).catch(function () {
      self.errorMsg = 'Registration failed. Email may already be in use.';
    }).finally(function () {
      self.loading = false;
    });
  };

  function redirectByRole(role) {
    if (role === 'parent' || role === 'student') $location.path('/parent/dashboard');
    else if (role === 'tutor')                   $location.path('/tutor/overview');
    else if (role === 'admin')                   $location.path('/admin/overview');
    else                                         $location.path('/login');
  }
}]);
