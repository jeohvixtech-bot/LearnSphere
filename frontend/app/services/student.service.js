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
}]);
