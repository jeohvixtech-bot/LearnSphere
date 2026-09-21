'use strict';

angular.module('learnSphereApp')
.controller('AppCtrl', ['$scope', '$location', 'AuthService', 'NotificationService',
function ($scope, $location, AuthService, NotificationService) {
  var self = this;

  self.notifDrawerOpen = false;
  self.notifications = [];
  self.unreadCount = 0;
  self.mobileNavOpen = false;

  self.toggleMobileNav = function () {
    self.mobileNavOpen = !self.mobileNavOpen;
  };

  self.closeMobileNav = function () {
    self.mobileNavOpen = false;
  };

  self.isLoggedIn = function () {
    return AuthService.isLoggedIn();
  };

  self.currentUser = AuthService.getCurrentUser();

  self.logout = function () {
    AuthService.logout();
    self.currentUser = null;
    $location.path('/welcome');
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

  // Same redirect for both roles under a given type collapses to one entry;
  // student is grouped with parent since it uses the same parent-side routes
  // (see change-password.controller.js's role branch, same grouping there).
  var NOTIFICATION_ROUTES = {
    booking: { parent: '/parent/sessions', tutor: '/tutor/overview' },
    payment: { parent: '/parent/billing', tutor: '/tutor/overview' },
    message: { parent: '/parent/chat', tutor: '/tutor/chat' },
    system: { parent: '/parent/dashboard', tutor: '/tutor/overview' }
  };

  self.onNotificationClick = function (n) {
    // Navigation shouldn't wait on (or be blocked by) the mark-read network
    // call — fire it and reconcile local unread state independently once it
    // resolves, same bookkeeping markAllRead's success handler already does.
    var wasUnread = !n.isRead;
    NotificationService.markRead(n.id).then(function () {
      n.isRead = true;
      if (wasUnread) self.unreadCount = Math.max(0, self.unreadCount - 1);
    });

    self.notifDrawerOpen = false;

    var role = self.currentUser && self.currentUser.role;
    var roleKey = (role === 'tutor') ? 'tutor' : 'parent';
    var routes = NOTIFICATION_ROUTES[n.type];
    $location.path(routes ? routes[roleKey] : (roleKey === 'tutor' ? '/tutor/overview' : '/parent/dashboard'));
  };

  // Watch for route changes to refresh user state and notifications
  $scope.$on('$routeChangeSuccess', function () {
    self.currentUser = AuthService.getCurrentUser();
    self.loadNotifications();
    self.mobileNavOpen = false;
  });
}]);
