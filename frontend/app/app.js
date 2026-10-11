'use strict';

angular.module('learnSphereApp', ['ngRoute'])

// Where the API lives, in order of precedence:
//   1. window.LEARNSPHERE_API_URL — set per environment in config.js (never cached, never bundled)
//   2. the local dev split (frontend on :3000, API on :5000)
//   3. same origin + /api — the production shape, with the reverse proxy in front of both
// Hard-coding 127.0.0.1 here made every deployed copy call the *visitor's* machine.
.constant('API_URL', (function () {
  if (window.LEARNSPHERE_API_URL) return String(window.LEARNSPHERE_API_URL).replace(/\/+$/, '');
  if (/^(localhost|127\.0\.0\.1)$/.test(window.location.hostname) && window.location.port === '3000') {
    return 'http://127.0.0.1:5000/api';
  }
  return window.location.origin + '/api';
})())

.config(['$routeProvider', '$locationProvider', function ($routeProvider, $locationProvider) {
  $locationProvider.hashPrefix('!');

  $routeProvider
    .when('/welcome', {
      // Cache-busted: the entry page must never be served from a stale cached
      // response (e.g. a leftover redirect from a previous local dev server).
      templateUrl: function () { return 'views/welcome.html?_=' + Date.now(); },
      controller: 'WelcomeCtrl',
      controllerAs: 'vm'
    })
    .when('/login', {
      templateUrl: function () { return 'views/login.html?_=' + Date.now(); },
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
    .when('/parent/wallet', {
      templateUrl: 'views/parent/wallet.html',
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
    .when('/tutor/wallet', {
      templateUrl: 'views/tutor/wallet.html',
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
    .when('/admin/reschedule-queue', {
      templateUrl: 'views/admin/reschedule-queue.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    .when('/admin/archive', {
      templateUrl: 'views/admin/archive.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    .when('/admin/payment-gateway', {
      templateUrl: 'views/admin/payment-gateway.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    .when('/admin/commission', {
      templateUrl: 'views/admin/commission.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    .when('/admin/promotional-credit', {
      templateUrl: 'views/admin/promotional-credit.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    .when('/admin/payouts', {
      templateUrl: 'views/admin/payouts.html',
      controller: 'AdminCtrl',
      controllerAs: 'vm',
      resolve: { auth: authGuard('admin') }
    })
    // Unknown address: a signed-in user goes to their own home, not to /welcome —
    // the landing page deliberately ends the session, so a typo or stale bookmark
    // used to log people out.
    .otherwise({ redirectTo: function () { return homeFor(readStoredUser()); } });

  // Mirrors AuthService's storage (that service can't be injected into redirectTo).
  function readStoredUser() {
    try { return JSON.parse(sessionStorage.getItem('ls_user')); } catch (e) { return null; }
  }

  function homeFor(user) {
    if (!user) return '/welcome';
    if (user.mustChangePassword) return '/change-password';
    if (user.role === 'admin') return '/admin/overview';
    if (user.role === 'tutor') return '/tutor/overview';
    return '/parent/dashboard';
  }

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
        // Signed out → landing page. Signed in but wrong role → their own home,
        // keeping the session intact.
        $location.path(homeFor(user));
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
        $location.path('/welcome');
        deferred.reject('Unauthorized');
      }
      return deferred.promise;
    }];
  }
}])

.run(['$rootScope', '$location', '$window', 'AuthService', function ($rootScope, $location, $window, AuthService) {
  $rootScope.$on('$routeChangeError', function () {
    // The guard has already pointed $location at the right place; only fall back to
    // /welcome when it hasn't (e.g. a template failed to load).
    var user = AuthService.getCurrentUser();
    var target = $location.path();
    var home = !user ? '/welcome'
      : user.mustChangePassword ? '/change-password'
      : user.role === 'admin' ? '/admin/overview'
      : user.role === 'tutor' ? '/tutor/overview' : '/parent/dashboard';
    if (target !== home && target !== '/change-password') $location.path(home);
  });

  // A scroll position left over from one page (e.g. a long tutor overview)
  // otherwise bleeds into the next route, landing the user mid-page instead
  // of at the top.
  $rootScope.$on('$routeChangeSuccess', function () {
    $window.scrollTo(0, 0);
  });
}]);
