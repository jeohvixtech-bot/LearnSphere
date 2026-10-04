'use strict';

angular.module('learnSphereApp')
.controller('StaticPageCtrl', ['$location', 'AuthService',
function ($location, AuthService) {
  var self = this;

  // Contact/About are reachable from both the logged-out welcome page and
  // every logged-in dashboard's new profile menu, across three different
  // roles — rather than guess which dashboard route to send a logged-in
  // parent/tutor/admin back to, just use browser history. Only the
  // logged-out case needs an explicit destination.
  self.isLoggedIn = AuthService.isLoggedIn();

  self.goBack = function () {
    if (self.isLoggedIn) {
      window.history.back();
    } else {
      $location.path('/welcome');
    }
  };
}]);
