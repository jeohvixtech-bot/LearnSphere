'use strict';

angular.module('learnSphereApp', ['ngRoute'])

.constant('API_URL', 'http://localhost:5000/api')

.config(['$routeProvider', '$locationProvider', function ($routeProvider, $locationProvider) {
  $locationProvider.hashPrefix('!');

  $routeProvider
    .when('/login', {
      templateUrl: 'views/login.html',
      controller: 'AuthCtrl',
      controllerAs: 'auth'
    })
    .when('/parent/dashboard', {
      templateUrl: 'views/parent/dashboard.html',
      controller: 'ParentCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('parent') }
    })
    .when('/parent/students', {
      templateUrl: 'views/parent/students.html',
      controller: 'ParentCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('parent') }
    })
    .when('/parent/search', {
      templateUrl: 'views/parent/search.html',
      controller: 'ParentCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('parent') }
    })
    .when('/parent/sessions', {
      templateUrl: 'views/parent/sessions.html',
      controller: 'ParentCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('parent') }
    })
    .when('/parent/billing', {
      templateUrl: 'views/parent/billing.html',
      controller: 'ParentCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('parent') }
    })
    .when('/parent/chat', {
      templateUrl: 'views/parent/chat.html',
      controller: 'ParentCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('parent') }
    })
    .when('/tutor/overview', {
      templateUrl: 'views/tutor/overview.html',
      controller: 'TutorCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('tutor') }
    })
    .when('/tutor/chat', {
      templateUrl: 'views/tutor/chat.html',
      controller: 'TutorCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('tutor') }
    })
    .when('/admin/overview', {
      templateUrl: 'views/admin/overview.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    .when('/admin/vetting', {
      templateUrl: 'views/admin/vetting.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    .when('/admin/disputes', {
      templateUrl: 'views/admin/disputes.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    .otherwise({ redirectTo: '/login' });

  function authGuard(requiredRole) {
    return ['$q', '$location', 'AuthService', function ($q, $location, AuthService) {
      var deferred = $q.defer();
      var user = AuthService.getCurrentUser();
      if (user && (!requiredRole || user.role === requiredRole)) {
        deferred.resolve(user);
      } else {
        $location.path('/login');
        deferred.reject('Unauthorized');
      }
      return deferred.promise;
    }];
  }
}])

.run(['$rootScope', '$location', 'AuthService', function ($rootScope, $location, AuthService) {
  $rootScope.$on('$routeChangeError', function () {
    $location.path('/login');
  });
}]);
