'use strict';

angular.module('learnSphereApp')
.controller('WelcomeCtrl', ['$scope', '$location', '$interval', '$timeout', '$window', 'AuthService', 'TutorService',
function ($scope, $location, $interval, $timeout, $window, AuthService, TutorService) {
  var self = this;

  // Navigating to the landing page while signed in ends the session — this is the
  // public entry point, not a dashboard shortcut. Log out and show it plainly.
  if (AuthService.isLoggedIn()) {
    AuthService.logout();
  }

  self.goToLogin = function () { $location.path('/login'); };

  self.tutors = [];
  TutorService.getAll().then(function (res) {
    self.tutors = res.data;
    // Wait for the tutor grid to render/paint before measuring scroll height
    $timeout(startAutoScroll, 300);
  });

  function startAutoScroll() {
    var scrollTimer = $interval(function () {
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
