'use strict';

angular.module('learnSphereApp')
.controller('AuthCtrl', ['$location', 'AuthService', function ($location, AuthService) {
  var self = this;

  // Redirect if already logged in
  if (AuthService.isLoggedIn()) {
    var user = AuthService.getCurrentUser();
    if (user.mustChangePassword) $location.path('/change-password');
    else redirectByRole(user.role);
  }

  self.mode = 'login';  // login | register
  self.loginData  = { email: 'sarah.tan@example.com', password: 'Parent@123' };
  self.registerData = { email: '', password: '', confirmPassword: '', name: '', role: 'parent', agreedToTerms: false };
  self.showTerms = false;
  self.errorMsg = '';
  self.loading = false;

  // Forgot password
  self.showForgotPassword = false;
  self.forgotEmail = '';
  self.forgotLoading = false;
  self.forgotMsg = '';
  self.forgotErrorMsg = '';

  self.openForgotPassword = function () {
    self.showForgotPassword = true;
    self.forgotEmail = self.loginData.email || '';
    self.forgotMsg = '';
    self.forgotErrorMsg = '';
  };

  self.closeForgotPassword = function () {
    self.showForgotPassword = false;
  };

  self.submitForgotPassword = function () {
    self.forgotMsg = '';
    self.forgotErrorMsg = '';
    self.forgotLoading = true;
    AuthService.forgotPassword(self.forgotEmail)
      .then(function (result) {
        self.forgotMsg = 'A temporary password has been sent to your email. Use it to log in, then you can change your password.';
      })
      .catch(function () {
        self.forgotErrorMsg = 'Something went wrong. Please try again.';
      })
      .finally(function () {
        self.forgotLoading = false;
      });
  };

  self.login = function () {
    self.errorMsg = '';
    self.loading = true;
    AuthService.login(self.loginData.email, self.loginData.password)
      .then(function (user) {
        if (!user || !user.role) {
          self.errorMsg = 'Unexpected response from server. Please try again.';
          return;
        }
        if (user.mustChangePassword) $location.path('/change-password');
        else redirectByRole(user.role);
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
    }).catch(function (err) {
      if (err && err.status === 400 && err.data && err.data.message) {
        self.errorMsg = err.data.message;
      } else if (err && err.status === 0) {
        self.errorMsg = 'Cannot reach server. Is the backend running on http://127.0.0.1:5000?';
      } else if (err && err.status) {
        self.errorMsg = 'Server error ' + err.status + ': ' + (err.data && err.data.message ? err.data.message : JSON.stringify(err.data));
      } else {
        self.errorMsg = 'Unknown error: ' + JSON.stringify(err);
      }
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
