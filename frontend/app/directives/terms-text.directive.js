'use strict';

// <terms-text content="someString"></terms-text>
// Renders plain-text Terms (see TermsService.parse) without ever using innerHTML.
angular.module('learnSphereApp')
.directive('termsText', ['TermsService', function (TermsService) {
  return {
    restrict: 'E',
    scope: { content: '=' },
    template:
      '<div class="terms-text" style="line-height:1.5;">' +
        '<div ng-repeat="b in blocks track by $index" ng-switch="b.type">' +
          '<h4 ng-switch-when="h" style="margin:16px 0 6px;"><span ng-repeat="s in b.segs track by $index">{{ s.t }}</span></h4>' +
          '<div ng-switch-when="li" style="padding-left:22px; text-indent:-14px; margin:0 0 4px;">&bull;&nbsp;<span ng-repeat="s in b.segs track by $index">' +
            '<a ng-if="s.mail" ng-href="mailto:{{ s.t }}">{{ s.t }}</a>' +
            '<strong ng-if="!s.mail && s.bold">{{ s.t }}</strong>' +
            '<span ng-if="!s.mail && !s.bold">{{ s.t }}</span></span></div>' +
          '<p ng-switch-default style="margin:0 0 10px;"><span ng-repeat="s in b.segs track by $index">' +
            '<a ng-if="s.mail" ng-href="mailto:{{ s.t }}">{{ s.t }}</a>' +
            '<strong ng-if="!s.mail && s.bold">{{ s.t }}</strong>' +
            '<span ng-if="!s.mail && !s.bold">{{ s.t }}</span></span></p>' +
        '</div>' +
      '</div>',
    link: function (scope) {
      scope.$watch('content', function (c) { scope.blocks = TermsService.parse(c); });
    }
  };
}]);
