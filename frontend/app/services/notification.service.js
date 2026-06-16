'use strict';

angular.module('learnSphereApp')
.service('NotificationService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;
  var h = function () { return { headers: AuthService.authHeader() }; };

  self.getAll = function () {
    return $http.get(API_URL + '/notifications', h());
  };

  self.markAllRead = function () {
    return $http.patch(API_URL + '/notifications/mark-all-read', {}, h());
  };
}]);
