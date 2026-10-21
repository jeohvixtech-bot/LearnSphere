'use strict';

angular.module('learnSphereApp')
.service('StudentService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;

  self.getMyStudents = function () {
    return $http.get(API_URL + '/students', { headers: AuthService.authHeader() });
  };

  self.create = function (data) {
    return $http.post(API_URL + '/students', data, { headers: AuthService.authHeader() });
  };

  self.update = function (id, data) {
    return $http.put(API_URL + '/students/' + id, data, { headers: AuthService.authHeader() });
  };

  self.delete = function (id) {
    return $http.delete(API_URL + '/students/' + id, { headers: AuthService.authHeader() });
  };

  self.archive = function (id) {
    return $http.post(API_URL + '/students/' + id + '/archive', {}, { headers: AuthService.authHeader() });
  };

  self.unarchive = function (id) {
    return $http.post(API_URL + '/students/' + id + '/unarchive', {}, { headers: AuthService.authHeader() });
  };

  self.updatePreferredModes = function (id, modes) {
    return $http.patch(API_URL + '/students/' + id + '/preferred-modes', { modes: modes }, {
      headers: AuthService.authHeader()
    });
  };
}]);
