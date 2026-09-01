'use strict';

angular.module('learnSphereApp')
.directive('fpDate', function () {
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
        }
      });

      ngModel.$render = function () {
        var v = ngModel.$viewValue;
        if (!v) { fp.setDate('', false); return; }
        var d;
        if (v instanceof Date) {
          d = v;
        } else {
          var s = String(v).trim();
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
        }
        fp.setDate(d && !isNaN(d.getTime()) ? d : '', false);
      };

      scope.$on('$destroy', function () { fp.destroy(); });
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
});
