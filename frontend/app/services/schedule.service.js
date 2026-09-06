'use strict';

angular.module('learnSphereApp')
.factory('ScheduleService', ['$http', '$q', 'API_URL', 'AuthService', function ($http, $q, API_URL, AuthService) {
  var _blocked = {}; // { tutorId: [{ id, start, end }, ...] }  (both stored as YYYY-MM-DD strings)
  var h = function () { return { headers: AuthService.authHeader() }; };

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
    // Synchronous — reads whatever's currently cached. Returns the SAME array
    // reference every time for a given tutorId, so callers that stash it
    // (e.g. vm.blockedRanges = ScheduleService.getBlocked(id)) keep seeing
    // updates made via loadBlocked/addBlock/removeBlock without re-assigning.
    getBlocked: function (tutorId) {
      if (!_blocked[tutorId]) _blocked[tutorId] = [];
      return _blocked[tutorId];
    },

    // Fetches this tutor's persisted blocked ranges from the backend and
    // populates the cache in place (see getBlocked note above). Call this
    // once when the tutor's own profile loads — previously nothing did, so
    // ScheduleService's in-memory object always started empty on refresh and
    // the tutor's blocked ranges appeared to just vanish.
    loadBlocked: function (tutorId) {
      var cache = this.getBlocked(tutorId);
      return $http.get(API_URL + '/tutors/' + tutorId + '/blocked-dates', h()).then(function (res) {
        cache.length = 0;
        (res.data || []).forEach(function (b) { cache.push({ id: b.id, start: b.start, end: b.end }); });
        return cache;
      });
    },

    addBlock: function (tutorId, start, end) {
      var cache = this.getBlocked(tutorId);
      return $http.post(API_URL + '/tutors/' + tutorId + '/blocked-dates',
        { startDate: toYMD(start), endDate: toYMD(end) }, h()).then(function (res) {
        cache.push({ id: res.data.id, start: res.data.start, end: res.data.end });
        return res.data;
      });
    },

    removeBlock: function (tutorId, idx) {
      var cache = this.getBlocked(tutorId);
      var block = cache[idx];
      if (!block) return $q.resolve();
      return $http.delete(API_URL + '/tutors/' + tutorId + '/blocked-dates/' + block.id, h()).then(function () {
        cache.splice(idx, 1);
      });
    },

    isBlocked: function (tutorId, dateStr) {
      if (!tutorId || !dateStr) return false;
      var d = toDay(dateStr);
      return (_blocked[tutorId] || []).some(function (r) {
        return d >= toDay(r.start) && d <= toDay(r.end);
      });
    }
  };
}]);
