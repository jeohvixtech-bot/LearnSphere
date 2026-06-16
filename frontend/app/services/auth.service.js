'use strict';

angular.module('learnSphereApp')
.service('AuthService', ['$http', '$q', 'API_URL', function ($http, $q, API_URL) {
  var self = this;
  var TOKEN_KEY = 'ls_token';
  var USER_KEY  = 'ls_user';

  self.login = function (email, password) {
    return $http.post(API_URL + '/auth/login', { email: email, password: password })
      .then(function (res) {
        localStorage.setItem(TOKEN_KEY, res.data.token);
        localStorage.setItem(USER_KEY, JSON.stringify(res.data));
        return res.data;
      });
  };

  self.register = function (email, password, name, role) {
    return $http.post(API_URL + '/auth/register', { email: email, password: password, name: name, role: role })
      .then(function (res) {
        localStorage.setItem(TOKEN_KEY, res.data.token);
        localStorage.setItem(USER_KEY, JSON.stringify(res.data));
        return res.data;
      });
  };

  self.logout = function () {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  };

  self.getToken = function () {
    return localStorage.getItem(TOKEN_KEY);
  };

  self.getCurrentUser = function () {
    var raw = localStorage.getItem(USER_KEY);
    return raw ? JSON.parse(raw) : null;
  };

  self.isLoggedIn = function () {
    return !!self.getToken();
  };

  self.authHeader = function () {
    return { Authorization: 'Bearer ' + self.getToken() };
  };
}]);
