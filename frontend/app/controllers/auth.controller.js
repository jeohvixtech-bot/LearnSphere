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
  self.registerData = { email: '', password: '', name: '', role: 'parent' };
  self.errorMsg = '';
  self.loading = false;

  self.login = function () {
    self.errorMsg = '';
    self.loading = true;
    AuthService.login(self.loginData.email, self.loginData.password)
      .then(function (user) {
        redirectByRole(user.role);
      })
      .catch(function () {
        self.errorMsg = 'Invalid email or password. Please try again.';
      })
      .finally(function () {
        self.loading = false;
      });
  };

  self.register = function () {
    self.errorMsg = '';
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
    if (role === 'parent')     $location.path('/parent/dashboard');
    else if (role === 'tutor') $location.path('/tutor/overview');
    else if (role === 'admin') $location.path('/admin/overview');
    else                       $location.path('/login');
  }
}]);
