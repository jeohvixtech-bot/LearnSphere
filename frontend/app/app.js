'use strict';

angular.module('learnSphereApp', ['ngRoute'])

.constant('API_URL', 'http://103.233.3.146:1000/api')

.config(['$routeProvider', '$locationProvider', function ($routeProvider, $locationProvider) {
  $locationProvider.hashPrefix('!');

  $routeProvider
    .when('/welcome', {
      templateUrl: 'views/welcome.html',
      controller: 'WelcomeCtrl',
      controllerAs: 'vm'
    })
    .when('/login', {
      templateUrl: 'views/login.html',
      controller: 'AuthCtrl',
      controllerAs: 'auth'
    })
    .when('/change-password', {
      templateUrl: 'views/change-password.html',
      controller: 'ChangePasswordCtrl',
      controllerAs: 'vm',
      resolve: { auth: loggedInGuard() }
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
    .when('/parent/ai-match', {
      templateUrl: 'views/parent/ai-match.html',
      controller: 'ParentCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('parent') }
    })
    .when('/parent/personalize', {
      templateUrl: 'views/parent/personalize.html',
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
    .when('/admin/scoring', {
      templateUrl: 'views/admin/scoring.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    .otherwise({ redirectTo: '/welcome' });

  function authGuard(requiredRole) {
    return ['$q', '$location', 'AuthService', function ($q, $location, AuthService) {
      var deferred = $q.defer();
      var user = AuthService.getCurrentUser();
      if (user && (!requiredRole || user.role === requiredRole)) {
        if (user.mustChangePassword) {
          $location.path('/change-password');
          deferred.reject('MustChangePassword');
        } else {
          deferred.resolve(user);
        }
      } else {
        $location.path('/login');
        deferred.reject('Unauthorized');
      }
      return deferred.promise;
    }];
  }

  function loggedInGuard() {
    return ['$q', '$location', 'AuthService', function ($q, $location, AuthService) {
      var deferred = $q.defer();
      var user = AuthService.getCurrentUser();
      if (user) {
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
