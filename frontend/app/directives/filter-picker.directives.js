'use strict';

// Shared "styled popup picker" shell for the search-page filter bar (Subject,
// Mode, Experience, Rating — see views/parent/search.html) — replaces a plain
// <select> with a trigger button that opens a centered modal styled as a
// divided list of rows (icon + title [+ subtitle] + chevron). A genuine
// modal (fixed backdrop + centered card), not a small panel anchored under
// the trigger — same pattern as .ov-modal-backdrop/.ov-modal in
// tutor/overview.html: backdrop and card are siblings, both position:fixed,
// backdrop click closes, card stops that click from bubbling to it.
//
// One directive drives all four filters. Subject supplies a multi-entry
// `stack` (drill down through exam type -> subject -> level, pushing a new
// step per pick — see parent.controller.js's buildSubjectExamTypeStep /
// buildSubjectNameStep / buildSubjectLevelStep); Mode/Experience/Rating each
// supply a single-entry stack (so the back arrow never shows for them). This
// directive has no idea what a "subject" or a "rating" is — it only renders
// whatever step is on top of the stack and calls row.onSelect() on click, so
// a 5th filter later needs no directive changes, just a new stack-builder.
angular.module('learnSphereApp')
.directive('filterPicker', [function () {
  return {
    restrict: 'E',
    scope: {
      display: '@',    // trigger button text, e.g. display="{{ vm.subjectPickerLabel() }}"
      isOpen: '<',      // whether the popup is currently shown
      stack: '=',        // [{title, rows:[{icon, iconBg, iconColor, title, subtitle, hasChevron, selected, onSelect}]}] — last entry is the visible step
      onToggle: '&',    // called when the trigger button is clicked
      onClose: '&'      // called when × is clicked, or the backdrop is clicked
    },
    template:
      '<div class="filter-picker">' +
        '<button type="button" class="filter-picker-trigger" ng-click="onToggle()" ' +
          'ng-class="{open: isOpen}" aria-haspopup="listbox" aria-expanded="{{ isOpen }}">' +
          '<span class="filter-picker-trigger-label">{{ display }}</span>' +
          '<i class="ti ti-chevron-down" aria-hidden="true"></i>' +
        '</button>' +
        '<div class="filter-picker-backdrop" ng-if="isOpen" ng-click="onClose()"></div>' +
        '<div class="filter-picker-modal" ng-if="isOpen" ng-click="$event.stopPropagation()">' +
          '<div class="filter-picker-modal-header">' +
            '<button type="button" class="filter-picker-back" ng-if="stack.length > 1" ng-click="back()" title="Back">' +
              '<i class="ti ti-chevron-left" aria-hidden="true"></i>' +
            '</button>' +
            '<span class="filter-picker-modal-title">{{ currentStep().title }}</span>' +
            '<button type="button" class="filter-picker-close" ng-click="onClose()" aria-label="Close">' +
              '<i class="ti ti-x" aria-hidden="true"></i>' +
            '</button>' +
          '</div>' +
          '<div class="filter-picker-row-list">' +
            '<button type="button" class="filter-picker-row" ng-repeat="row in currentStep().rows track by $index" ' +
              'ng-class="{selected: row.selected}" ng-click="row.onSelect()">' +
              '<span class="filter-picker-row-icon" ng-if="row.icon" ' +
                'ng-style="row.iconBg ? {background: row.iconBg, color: row.iconColor} : {}">' +
                '<i class="ti" ng-class="row.icon" aria-hidden="true"></i>' +
              '</span>' +
              '<span class="filter-picker-row-text">' +
                '<span class="filter-picker-row-title">{{ row.title }}</span>' +
                '<span class="filter-picker-row-subtitle" ng-if="row.subtitle">{{ row.subtitle }}</span>' +
              '</span>' +
              '<i class="ti ti-chevron-right filter-picker-row-chevron" ng-if="row.hasChevron" aria-hidden="true"></i>' +
            '</button>' +
          '</div>' +
        '</div>' +
      '</div>',
    link: function (scope) {
      scope.currentStep = function () {
        return (scope.stack && scope.stack[scope.stack.length - 1]) || { title: '', rows: [] };
      };

      // Purely local — the stack is a shared array reference (= binding), so
      // popping it here is visible to the parent controller too. No callback
      // needed: this directive doesn't need to know WHY a step was pushed,
      // just that going back means showing the previous entry.
      scope.back = function () {
        if (scope.stack && scope.stack.length > 1) scope.stack.pop();
      };
    }
  };
}]);
