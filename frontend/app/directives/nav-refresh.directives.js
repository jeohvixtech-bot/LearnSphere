'use strict';

// Clicking a sidebar link to a DIFFERENT route works as normal navigation —
// ngRoute only fires $routeChangeSuccess (which is what re-runs a page's
// controller and re-fetches its data) when the path actually changes. Since
// every sidebar nav link is a plain <a href="#!/..."> with no ng-click,
// clicking the already-active tab was a complete no-op: same path, no route
// change, nothing re-fetches. This restores "click the active tab to refresh
// it" by forcing $route.reload() whenever the clicked link's own href already
// matches the current path.
//
// Applied via class (restrict: 'C') rather than an attribute added to every
// link, since these two class names are used consistently, and only, on
// sidebar nav links across every tutor/parent/admin page (nav-item for admin,
// bs-sidebar-link for tutor/parent) — no template changes needed anywhere.
function activeTabRefreshLink($location, $route) {
  return function (scope, element, attrs) {
    if (!attrs.href) return;
    element.on('click', function () {
      var href = attrs.href;
      var hashIdx = href.indexOf('#!');
      var path = hashIdx >= 0 ? href.slice(hashIdx + 2) : href;
      if (path && $location.path() === path) {
        scope.$apply(function () { $route.reload(); });
      }
    });
  };
}

angular.module('learnSphereApp')
.directive('navItem', ['$location', '$route', function ($location, $route) {
  return { restrict: 'C', link: activeTabRefreshLink($location, $route) };
}])
.directive('bsSidebarLink', ['$location', '$route', function ($location, $route) {
  return { restrict: 'C', link: activeTabRefreshLink($location, $route) };
}]);
