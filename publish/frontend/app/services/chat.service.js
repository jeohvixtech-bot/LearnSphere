'use strict';

angular.module('learnSphereApp')
.service('ChatService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;
  var h = function () { return { headers: AuthService.authHeader() }; };

  self.getMessages = function (tutorId, parentUserId) {
    return $http.get(API_URL + '/chat/' + tutorId + '/' + parentUserId, h());
  };

  self.send = function (data) {
    return $http.post(API_URL + '/chat', data, h());
  };

  self.getUnreadCounts = function () {
    return $http.get(API_URL + '/chat/unread-counts', h());
  };
}]);
