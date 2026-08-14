'use strict';

angular.module('learnSphereApp')
.controller('WelcomeCtrl', ['$scope', '$location', '$interval', '$timeout', '$window', 'AuthService', 'TutorService', 'PendingMatchService',
function ($scope, $location, $interval, $timeout, $window, AuthService, TutorService, PendingMatchService) {
  var self = this;

  // Navigating to the landing page while signed in ends the session — this is the
  // public entry point, not a dashboard shortcut. Log out and show it plainly.
  if (AuthService.isLoggedIn()) {
    AuthService.logout();
  }

  // Clicking a tutor card while signed out stashes the tutor so that once the visitor
  // signs in (or registers), they land straight on that tutor's booking page instead
  // of a generic dashboard — see AuthCtrl.redirectByRole and ParentCtrl.init().
  self.goToLogin = function (tutor) {
    if (tutor) PendingMatchService.setTutor(tutor.id);
    $location.path('/login');
  };

  self.tutors = [];
  self.scrollPaused = false;

  self.pauseScroll = function () {
    self.scrollPaused = true;
  };

  self.resumeScroll = function () {
    self.scrollPaused = false;
  };

  TutorService.getAll().then(function (res) {
    self.tutors = res.data;
    // Wait for the tutor grid to render/paint before measuring scroll height
    $timeout(startAutoScroll, 300);
  });

  function startAutoScroll() {
    var scrollTimer = $interval(function () {
      if (self.scrollPaused) return;
      var doc = $window.document.documentElement;
      var atBottom = $window.innerHeight + $window.scrollY >= doc.scrollHeight - 2;
      if (atBottom) {
        $interval.cancel(scrollTimer);
        return;
      }
      $window.scrollBy(0, 1);
    }, 30);

    $scope.$on('$destroy', function () { $interval.cancel(scrollTimer); });
  }
}]);
