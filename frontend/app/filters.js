'use strict';

angular.module('learnSphereApp')
.directive('fpDate', function () {
  // Shared by $render (below) and the minDateFrom watch — parses a Date
  // object or any of the string formats this directive/the rest of the app
  // hands around (DD-MM-YYYY, D/MM/YYYY, YYYY-MM-DD) into a Date, or null.
  function parseFlexibleDate(v) {
    if (!v) return null;
    if (v instanceof Date) return isNaN(v.getTime()) ? null : v;
    var s = String(v).trim();
    var d;
    var dmyh = s.match(/^(\d{1,2})-(\d{1,2})-(\d{4})$/);
    if (dmyh) { d = new Date(+dmyh[3], +dmyh[2] - 1, +dmyh[1]); }
    else {
      var slsh = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
      if (slsh) { d = new Date(+slsh[3], +slsh[2] - 1, +slsh[1]); }
      else {
        var ymd = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (ymd) { d = new Date(+ymd[1], +ymd[2] - 1, +ymd[3]); }
      }
    }
    return d && !isNaN(d.getTime()) ? d : null;
  }

  return {
    restrict: 'A',
    require: 'ngModel',
    link: function (scope, element, attrs, ngModel) {
      var fp = flatpickr(element[0], {
        dateFormat: 'd-m-Y',
        allowInput: true,
        minDate: 'today',
        onChange: function (selectedDates, dateStr) {
          scope.$apply(function () {
            ngModel.$setViewValue(dateStr || '');
            if (attrs.ngChange) scope.$eval(attrs.ngChange);
          });
        },
        // Starting view only, not a hard bound — jumps the calendar to the
        // linked field's month when opened, but navigating to a later month
        // and picking a date there is still fine (minDate is the real bound).
        onOpen: function () {
          if (!attrs.minDateFrom) return;
          var linked = parseFlexibleDate(scope.$eval(attrs.minDateFrom));
          if (linked) fp.jumpToDate(linked);
        }
      });

      ngModel.$render = function () {
        var d = parseFlexibleDate(ngModel.$viewValue);
        fp.setDate(d || '', false);
      };

      // e.g. fp-date min-date-from="vm.blockForm.startDate" on the end-date
      // field — its minDate tracks the currently-selected start date instead
      // of the fixed 'today' the start field itself keeps.
      if (attrs.minDateFrom) {
        scope.$watch(attrs.minDateFrom, function (newVal, oldVal) {
          var linked = parseFlexibleDate(newVal);
          fp.set('minDate', linked || 'today');

          if (newVal === oldVal) return; // initial $watch firing — not a real change yet
          var currentEnd = parseFlexibleDate(ngModel.$viewValue);
          if (linked && currentEnd && currentEnd < linked) {
            ngModel.$setViewValue('');
            ngModel.$render();
            if (attrs.ngChange) scope.$eval(attrs.ngChange);
          }
        });
      }

      // e.g. fp-date view-month-from="vm.calYear + '-' + vm.calMonth" on the
      // block-range Start Date field — its calendar view tracks whichever
      // month the main calendar grid is currently showing (Prev/Next), not
      // just on open but live for as long as this field exists, since a
      // watch re-fires on every digest where the expression's value changed.
      // jumpToDate only ever changes what's DISPLAYED, never a bound — it's
      // harmless (and a no-op re-render) to call while the picker is closed.
      if (attrs.viewMonthFrom) {
        scope.$watch(attrs.viewMonthFrom, function (val) {
          if (!val) return;
          var parts = String(val).split('-');
          var year = parseInt(parts[0], 10), month = parseInt(parts[1], 10);
          if (!isNaN(year) && !isNaN(month)) fp.jumpToDate(new Date(year, month, 1));
        });
      }

      scope.$on('$destroy', function () { fp.destroy(); });
    }
  };
})
// Shows a zone's .reset-tip child only once the cursor has stopped moving —
// like a native tooltip, not a cursor-attached label. Every mousemove hides
// it and restarts the show-delay; only a genuine pause reveals it at that
// resting position. Visibility is the "reset-tip--visible" class (toggled
// here); main.css just supplies the fade transition, no :hover involved.
.directive('resetZone', function () {
  var SHOW_DELAY = 350;
  return {
    restrict: 'A',
    link: function (scope, element) {
      var tip = element[0].querySelector('.reset-tip');
      if (!tip) return;
      var showTimer = null;

      function hide() {
        if (showTimer) { clearTimeout(showTimer); showTimer = null; }
        tip.classList.remove('reset-tip--visible');
      }

      element.on('mousemove', function (e) {
        hide();
        // mousemove bubbles from any child (buttons, day cells, the whole
        // bs-cal-grid-wrap) up to this zone's own listener — without this
        // check the tip would show/follow while hovering exactly the
        // children clicking here already never resets (onCalendarAreaClick
        // in tutor.controller.js checks the same target === currentTarget).
        // Only the zone's own exposed background schedules a show.
        if (e.target !== element[0]) return;
        var rect = element[0].getBoundingClientRect();
        var x = e.clientX - rect.left;
        var y = e.clientY - rect.top;
        showTimer = setTimeout(function () {
          tip.style.left = x + 'px';
          tip.style.top = y + 'px';
          tip.classList.add('reset-tip--visible');
        }, SHOW_DELAY);
      });

      element.on('mouseleave', hide);
      scope.$on('$destroy', hide);
    }
  };
})
.filter('dmy', function () {
  return function (val) {
    if (!val) return '';
    // Date object
    if (val instanceof Date) {
      var dd = (val.getDate()       < 10 ? '0' : '') + val.getDate();
      var mm = (val.getMonth() + 1  < 10 ? '0' : '') + (val.getMonth() + 1);
      return dd + '-' + mm + '-' + val.getFullYear();
    }
    var s = String(val);
    // YYYY-MM-DD
    var ymd = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (ymd) return ymd[3] + '-' + ymd[2] + '-' + ymd[1];
    // D/MM/YYYY or DD/MM/YYYY  →  DD-MM-YYYY
    var dmy = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
    if (dmy) {
      var d2 = dmy[1].length === 1 ? '0' + dmy[1] : dmy[1];
      var m2 = dmy[2].length === 1 ? '0' + dmy[2] : dmy[2];
      return d2 + '-' + m2 + '-' + dmy[3];
    }
    return s;
  };
})
// Normalizes any stored clock time (or "H:MM AM/PM - H:MM AM/PM" range) into a
// consistent zero-padded "hh:mm AM/PM" display, regardless of how it was originally
// entered/stored (e.g. "4:00 PM" or "4:00PM" both render as "04:00 PM").
.filter('hhmma', function () {
  function normalizeOne(raw) {
    var m = String(raw || '').trim().match(/^(\d{1,2}):(\d{2})\s*(AM|PM)$/i);
    if (!m) return raw;
    var h = parseInt(m[1], 10), min = parseInt(m[2], 10);
    if (h < 1 || h > 12 || min < 0 || min > 59) return raw;
    var hh = (h < 10 ? '0' : '') + h;
    var mm = (min < 10 ? '0' : '') + min;
    return hh + ':' + mm + ' ' + m[3].toUpperCase();
  }
  return function (val) {
    if (!val) return '';
    return String(val).split(/\s*-\s*/).map(normalizeOne).join(' - ');
  };
})
// Display label for a TutorDocument.documentType value (verification section).
.filter('verifDocLabel', function () {
  var labels = {
    identity_photo: 'Identity photo',
    identity_id: 'ID number',
    profile_photo: 'Profile photo',
    o_level: 'O-Level / SPM',
    a_level: 'A-Level / STPM / Diploma',
    degree: "Bachelor's degree",
    postgrad: "Master's / PhD",
    nie_cert: 'NIE / DPLI / MOE certificate',
    intro_video: 'Introduction video',
    specialist_cert: 'Specialist certificate'
  };
  return function (type) {
    return labels[type] || type || '';
  };
})
.filter('capitalize', function () {
  return function (s) {
    if (!s) return '';
    return s.charAt(0).toUpperCase() + s.slice(1).replace(/_/g, ' ');
  };
})
// Truncates to `len` characters (default 60), appending an ellipsis only when
// actually truncated — used for the welcome page's featured-remark snippet.
.filter('truncate', function () {
  return function (s, len) {
    if (!s) return '';
    len = len || 60;
    return s.length > len ? s.slice(0, len).trim() + '…' : s;
  };
});
