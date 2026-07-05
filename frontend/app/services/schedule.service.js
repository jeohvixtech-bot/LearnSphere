'use strict';

angular.module('learnSphereApp')
.factory('ScheduleService', function () {
  var _blocked = {}; // { tutorId: [{ start, end }, ...] }  (both stored as YYYY-MM-DD strings)

  // Robustly parse any date value to a local-midnight Date.
  // Handles: Date objects, "YYYY-MM-DD", "D/MM/YYYY", "DD/MM/YYYY", "DD-MM-YYYY".
  function toDay(val) {
    if (!val) return new Date(NaN);
    if (val instanceof Date) return new Date(val.getFullYear(), val.getMonth(), val.getDate());
    var s = String(val).trim();
    // DD-MM-YYYY (user text input format)
    var dmyh = s.match(/^(\d{1,2})-(\d{1,2})-(\d{4})$/);
    if (dmyh) return new Date(+dmyh[3], +dmyh[2] - 1, +dmyh[1]);
    // DD/MM/YYYY or D/M/YYYY (API format)
    var dmy = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
    if (dmy) return new Date(+dmy[3], +dmy[2] - 1, +dmy[1]);
    // YYYY-MM-DD
    var ymd = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (ymd) return new Date(+ymd[1], +ymd[2] - 1, +ymd[3]);
    // fallback
    var d = new Date(s); d.setHours(0, 0, 0, 0); return d;
  }

  // Convert any date value to a "YYYY-MM-DD" string for consistent storage.
  function toYMD(val) {
    var d = toDay(val);
    var mm = (d.getMonth() + 1 < 10 ? '0' : '') + (d.getMonth() + 1);
    var dd = (d.getDate()       < 10 ? '0' : '') + d.getDate();
    return d.getFullYear() + '-' + mm + '-' + dd;
  }

  return {
    getBlocked: function (tutorId) {
      if (!_blocked[tutorId]) _blocked[tutorId] = [];
      return _blocked[tutorId];
    },
    addBlock: function (tutorId, start, end) {
      if (!_blocked[tutorId]) _blocked[tutorId] = [];
      _blocked[tutorId].push({ start: toYMD(start), end: toYMD(end) });
    },
    removeBlock: function (tutorId, idx) {
      if (_blocked[tutorId]) _blocked[tutorId].splice(idx, 1);
    },
    isBlocked: function (tutorId, dateStr) {
      if (!tutorId || !dateStr) return false;
      var d = toDay(dateStr);
      return (_blocked[tutorId] || []).some(function (r) {
        return d >= toDay(r.start) && d <= toDay(r.end);
      });
    }
  };
});
