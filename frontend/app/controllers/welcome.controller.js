'use strict';

angular.module('learnSphereApp')
.controller('WelcomeCtrl', ['$scope', '$location', '$interval', '$timeout', '$window', 'AuthService', 'TutorService',
function ($scope, $location, $interval, $timeout, $window, AuthService, TutorService) {
  var self = this;

  // Skip the landing page if already signed in
  if (AuthService.isLoggedIn()) {
    var user = AuthService.getCurrentUser();
    if (user.role === 'parent' || user.role === 'student') $location.path('/parent/dashboard');
    else if (user.role === 'tutor') $location.path('/tutor/overview');
    else if (user.role === 'admin') $location.path('/admin/overview');
  }

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
