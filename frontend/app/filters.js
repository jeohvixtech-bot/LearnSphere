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
});
