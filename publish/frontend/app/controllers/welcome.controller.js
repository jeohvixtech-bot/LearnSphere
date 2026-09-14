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
    // Same rule as the parent catalog's filteredTutors() (tutorHasAnySlots) — a
    // tutor with no published preset class is a dead end (nothing to book once a
    // visitor clicks through), so don't feature them here either. A slot's mode
    // being set is what distinguishes a real preset-class slot from a plain
    // availability block — see publishedSlotsOnDay in tutor.controller.js.
    self.tutors = (res.data || []).filter(function (t) {
      return (t.timetable || []).some(function (s) { return !!s.mode; });
    });
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
