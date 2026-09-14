'use strict';

angular.module('learnSphereApp')
.service('ParentProfileService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;

  self.getProfile = function () {
    return $http.get(API_URL + '/parents', { headers: AuthService.authHeader() });
  };

  self.update = function (id, data) {
    return $http.put(API_URL + '/parents/' + id, data, { headers: AuthService.authHeader() });
  };

  self.close = function (id) {
    return $http.delete(API_URL + '/parents/' + id, { headers: AuthService.authHeader() });
  };
}]);
