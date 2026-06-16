'use strict';

angular.module('learnSphereApp')
.controller('AppCtrl', ['$scope', '$location', 'AuthService', 'NotificationService',
function ($scope, $location, AuthService, NotificationService) {
  var self = this;

  self.notifDrawerOpen = false;
  self.notifications = [];
  self.unreadCount = 0;

  self.isLoggedIn = function () {
    return AuthService.isLoggedIn();
  };

  self.currentUser = AuthService.getCurrentUser();

  self.logout = function () {
    AuthService.logout();
    self.currentUser = null;
    $location.path('/login');
  };

  self.toggleNotifDrawer = function () {
    self.notifDrawerOpen = !self.notifDrawerOpen;
    if (self.notifDrawerOpen) {
      self.loadNotifications();
    }
  };

  self.loadNotifications = function () {
    if (!AuthService.isLoggedIn()) return;
    NotificationService.getAll().then(function (res) {
      self.notifications = res.data;
      self.unreadCount = res.data.filter(function (n) { return !n.isRead; }).length;
    });
  };

  self.markAllRead = function () {
    NotificationService.markAllRead().then(function () {
      self.notifications.forEach(function (n) { n.isRead = true; });
      self.unreadCount = 0;
      self.notifDrawerOpen = false;
    });
  };

  // Watch for route changes to refresh user state and notifications
  $scope.$on('$routeChangeSuccess', function () {
    self.currentUser = AuthService.getCurrentUser();
    self.loadNotifications();
  });
}]);
