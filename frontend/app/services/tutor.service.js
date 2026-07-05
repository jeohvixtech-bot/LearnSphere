'use strict';

angular.module('learnSphereApp')
.service('TutorService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;

  self.getAll = function (params) {
    return $http.get(API_URL + '/tutors', { params: params });
  };

  self.getById = function (id) {
    return $http.get(API_URL + '/tutors/' + id);
  };

  self.getByUser = function (userId) {
    return $http.get(API_URL + '/tutors/by-user/' + userId, {
      headers: AuthService.authHeader()
    });
  };

  self.update = function (id, data) {
    return $http.put(API_URL + '/tutors/' + id, data, {
      headers: AuthService.authHeader()
    });
  };

  self.uploadImage = function (file) {
    var fd = new FormData();
    fd.append('file', file);
    return $http.post(API_URL + '/upload/image', fd, {
      headers: angular.extend({ 'Content-Type': undefined }, AuthService.authHeader()),
      transformRequest: angular.identity
    });
  };
}]);
