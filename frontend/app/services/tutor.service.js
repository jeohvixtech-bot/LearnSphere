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

  self.getBusyTimes = function (id) {
    return $http.get(API_URL + '/tutors/' + id + '/busy-times');
  };

  self.getFavorites = function () {
    return $http.get(API_URL + '/tutors/favorites', { headers: AuthService.authHeader() });
  };

  self.addFavorite = function (id) {
    return $http.post(API_URL + '/tutors/' + id + '/favorite', null, { headers: AuthService.authHeader() });
  };

  self.removeFavorite = function (id) {
    return $http.delete(API_URL + '/tutors/' + id + '/favorite', { headers: AuthService.authHeader() });
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

  self.updateOnlineStatus = function (id, isOnline) {
    return $http.patch(API_URL + '/tutors/' + id + '/online-status', { isOnline: isOnline }, {
      headers: AuthService.authHeader()
    });
  };

  self.updateModes = function (id, modes) {
    return $http.patch(API_URL + '/tutors/' + id + '/modes', { modes: modes }, {
      headers: AuthService.authHeader()
    });
  };

  self.setupClass = function (id, data) {
    return $http.post(API_URL + '/tutors/' + id + '/setup-class', data, {
      headers: AuthService.authHeader()
    });
  };

  self.getMatchScores = function () {
    return $http.get(API_URL + '/tutors/match-scores');
  };

  self.getPresetSlots = function (studentId) {
    return $http.get(API_URL + '/tutors/preset-slots', {
      params: { studentId: studentId }
    });
  };

  self.deleteSlot = function (tutorId, slotId) {
    return $http.delete(API_URL + '/tutors/' + tutorId + '/slots/' + slotId, {
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
