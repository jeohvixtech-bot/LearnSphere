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

  self.getSyllabusTopics = function (country, subject, level) {
    return $http.get(API_URL + '/tutors/syllabus-topics', {
      params: { country: country, subject: subject, level: level }
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

  // body (optional): { proposedDate, proposedTime, proposedEndTime } to propose a
  // reschedule for affected students instead of a straight cancel — see
  // TutorsController.DeleteSlot. $http.delete's config supports a `data` property
  // for a request body, same as post/put — but unlike post/put, $http has no
  // default Content-Type for delete, so it must be set explicitly or ASP.NET's
  // [FromBody] binding 415s before the request ever reaches the controller.
  self.deleteSlot = function (tutorId, slotId, body) {
    return $http.delete(API_URL + '/tutors/' + tutorId + '/slots/' + slotId, {
      headers: angular.extend({ 'Content-Type': 'application/json' }, AuthService.authHeader()),
      data: body || {}
    });
  };

  self.setSlotVideoLink = function (tutorId, slotId, link) {
    return $http.patch(API_URL + '/tutors/' + tutorId + '/slots/' + slotId + '/video-link',
      { videoConferenceLink: link }, { headers: AuthService.authHeader() });
  };

  self.uploadImage = function (file) {
    var fd = new FormData();
    fd.append('file', file);
    return $http.post(API_URL + '/upload/image', fd, {
      headers: angular.extend({ 'Content-Type': undefined }, AuthService.authHeader()),
      transformRequest: angular.identity
    });
  };

  self.uploadDocument = function (file, type) {
    var fd = new FormData();
    fd.append('file', file);
    return $http.post(API_URL + '/upload/document?type=' + type, fd, {
      headers: angular.extend({ 'Content-Type': undefined }, AuthService.authHeader()),
      transformRequest: angular.identity
    });
  };

  self.saveDocument = function (tutorId, data) {
    return $http.post(API_URL + '/tutors/' + tutorId + '/documents', data, {
      headers: AuthService.authHeader()
    });
  };

  self.removeDocument = function (tutorId, docId) {
    return $http.delete(API_URL + '/tutors/' + tutorId + '/documents/' + docId, {
      headers: AuthService.authHeader()
    });
  };

  self.submitVerification = function (tutorId) {
    return $http.post(API_URL + '/tutors/' + tutorId + '/submit-verification', null, {
      headers: AuthService.authHeader()
    });
  };
}]);
