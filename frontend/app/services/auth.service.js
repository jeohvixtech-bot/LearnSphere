'use strict';

angular.module('learnSphereApp')
.service('AuthService', ['$http', '$q', 'API_URL', 'PendingMatchService', function ($http, $q, API_URL, PendingMatchService) {
  var self = this;
  var TOKEN_KEY = 'ls_token';
  var USER_KEY  = 'ls_user';

  self.login = function (email, password) {
    return $http.post(API_URL + '/auth/login', { email: email, password: password })
      .then(function (res) {
        sessionStorage.setItem(TOKEN_KEY, res.data.token);
        sessionStorage.setItem(USER_KEY, JSON.stringify(res.data));
        return res.data;
      });
  };

  self.register = function (email, password, name, role) {
    return $http.post(API_URL + '/auth/register', { email: email, password: password, name: name, role: role })
      .then(function (res) {
        sessionStorage.setItem(TOKEN_KEY, res.data.token);
        sessionStorage.setItem(USER_KEY, JSON.stringify(res.data));
        return res.data;
      });
  };

  self.forgotPassword = function (email) {
    return $http.post(API_URL + '/auth/forgot-password', { email: email })
      .then(function (res) { return res.data; });
  };

  self.changePassword = function (currentPassword, newPassword) {
    return $http.post(API_URL + '/auth/change-password',
      { currentPassword: currentPassword, newPassword: newPassword },
      { headers: self.authHeader() }
    ).then(function (res) {
      var user = self.getCurrentUser();
      if (user) {
        user.mustChangePassword = false;
        sessionStorage.setItem(USER_KEY, JSON.stringify(user));
      }
      return res.data;
    });
  };

  self.logout = function () {
    sessionStorage.removeItem(TOKEN_KEY);
    sessionStorage.removeItem(USER_KEY);
    // Single point of truth for "logout clears any pinned/pending tutor hand-off" —
    // covers every real logout path (AppCtrl's Sign Out button, ParentCtrl's
    // account-closure flow, change-password's "Sign out instead" link, and
    // WelcomeCtrl's auto-logout-if-already-signed-in) without needing each of
    // them to remember to call PendingMatchService.clear() themselves.
    PendingMatchService.clear();
  };

  self.getToken = function () {
    return sessionStorage.getItem(TOKEN_KEY);
  };

  self.getCurrentUser = function () {
    var raw = sessionStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) : null;
  };

  self.isLoggedIn = function () {
    return !!self.getToken();
  };

  self.authHeader = function () {
    return { Authorization: 'Bearer ' + self.getToken() };
  };
}]);
