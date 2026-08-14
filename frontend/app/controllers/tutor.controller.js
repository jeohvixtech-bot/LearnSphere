'use strict';

angular.module('learnSphereApp')
.controller('TutorCtrl', ['$scope', '$location', '$timeout', '$interval', 'AuthService', 'TutorService',
  'BookingService', 'ChatService', 'InvoiceService', 'ScheduleService', 'SubjectCatalog', 'TeachingModesCatalog', 'ProfanityFilterService',
function ($scope, $location, $timeout, $interval, AuthService, TutorService, BookingService, ChatService, InvoiceService, ScheduleService, SubjectCatalog, TeachingModesCatalog, ProfanityFilterService) {
  var self = this;
  var user = AuthService.getCurrentUser();
  self.user = user;
  self.tutor = null;
  self.bookings = [];
  self.invoices = [];
  self.chatMessages = [];
  self.subjectCatalog = SubjectCatalog;

  // Teaching-mode dual-pool selector (drag-and-drop between "available" and "offered").
  // Rebuilt into plain arrays only on load/change — never computed inline in the
  // template — to avoid the infinite-digest trap ng-repeat-over-a-function hits here.
  self.modesLeft = [];
  self.modesRight = [];
  self.modesError = '';
  self.offeringLockedError = '';

  function rebuildModePools(offeredModes) {
    var offered = offeredModes || [];
    self.modesLeft = TeachingModesCatalog.filter(function (m) { return offered.indexOf(m.mode) === -1; });
    self.modesRight = TeachingModesCatalog.filter(function (m) { return offered.indexOf(m.mode) !== -1; });
  }

  self.onModeDropToRight = function (item) {
    if (self.modesRight.some(function (m) { return m.mode === item.mode; })) return;
    self.modesLeft = self.modesLeft.filter(function (m) { return m.mode !== item.mode; });
    self.modesRight.push(item);
  };

  self.onModeDropToLeft = function (item) {
    if (self.modesLeft.some(function (m) { return m.mode === item.mode; })) return;
    self.modesRight = self.modesRight.filter(function (m) { return m.mode !== item.mode; });
    self.modesLeft.push(item);
    if (self.newOffering.mode === item.mode) self.newOffering.mode = '';
  };

  self.removeMode = function (mode) {
    self.onModeDropToLeft(mode);
  };

  // Tab state
  self.activeTab = 'overview';

  // Edit profile form
  self.profileForm = {};
  self.profileSuccess = false;
  self.profileError = '';
  self.uploading = false;
  self.newOffering = { country: '', selectedOption: null, qualification: '', price: null };

  // Report forms
  self.reportBooking = null;
  self.reportForm = { covered: '', performance: '', homework: '' };
  self.reportSuccess = false;

  self.editBooking = null;
  self.editForm = { covered: '', performance: '', homework: '', changesMade: '' };
  self.editSuccess = false;

  self.counterBooking = null;
  self.counterForm = { message: '', classes: [] };
  self.counterSuccess = false;

  self.rescheduleBooking = null;
  self.rescheduleForm = { classes: [] };
  self.rescheduleSuccess = false;

  self.chatText = '';
  self.selectedCalDays = [];
  var _now = new Date();
  self.calYear = _now.getFullYear();
  self.calMonth = _now.getMonth(); // 0-indexed

  function init() {
    TutorService.getByUser(user.userId).then(function (res) {
      self.tutor = res.data;
      // Pre-fill identity fields from any already-uploaded doc, so a page reload
      // doesn't blank the form back to the 'NRIC'/'' defaults.
      var existingIdDoc = (self.tutor.documents || [])
        .find(function (d) { return d.documentType === 'identity_photo'; });
      if (existingIdDoc && existingIdDoc.idType) {
        self.verif.idType = existingIdDoc.idType;
        self.verif.idNumber = existingIdDoc.idNumber || '';
      }
      self.blockedRanges = ScheduleService.getBlocked(res.data.id);
      self.profileForm = {
        imageUrl: res.data.imageUrl,
        bio: res.data.bio,
        experienceYears: res.data.experienceYears,
        offerings: res.data.offerings && res.data.offerings.length
          ? res.data.offerings.map(function(o) {
              return { country: o.country, subject: o.subject, level: o.level, mode: o.mode, qualification: o.qualification, price: o.price || 0 };
            })
          : []
      };
      rebuildModePools(res.data.modes);
      rebuildSetupClassUniqueSubjects();
      maybeInitChat();
    });
    BookingService.getAll().then(function (res) {
      self.bookings = res.data;
      maybeInitChat();
    });
    InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
  }
  init();

  // Chat — a tutor can only see one conversation per parent, not one shared thread
  // mixing every parent who's ever messaged them. Requires both the tutor profile
  // (for tutor.id) and bookings (to know which parents to list) to be loaded; whichever
  // of the two async calls above finishes last is what actually triggers this.
  self.contactableParents = [];
  self.activeParent = null;
  self.unreadCounts = {}; // { parentUserId: count } — sidebar badges, see ChatController.GetUnreadCounts

  self.loadUnreadCounts = function () {
    ChatService.getUnreadCounts().then(function (res) { self.unreadCounts = res.data; });
  };

  function computeContactableParents() {
    var seen = {};
    var list = [];
    (self.bookings || []).forEach(function (b) {
      if (!self.tutor || b.tutorId !== self.tutor.id) return;
      if ((b.status !== 'confirmed' && b.status !== 'completed') || seen[b.parentUserId]) return;
      seen[b.parentUserId] = true;
      list.push({ id: b.parentUserId, name: b.parentName, studentName: b.studentName });
    });
    self.contactableParents = list;
  }

  function maybeInitChat() {
    if ($location.path() !== '/tutor/chat' || !self.tutor) return;
    computeContactableParents();
    self.loadUnreadCounts();
    if (!self.activeParentUserId && self.contactableParents.length) {
      self.loadChat(self.contactableParents[0].id);
    }
  }

  self.loadChat = function (parentUserId) {
    self.activeParentUserId = parentUserId;
    self.activeParent = self.contactableParents.find(function (p) { return p.id === parentUserId; }) || null;
    ChatService.getMessages(self.tutor.id, parentUserId).then(function (res) {
      self.chatMessages = res.data;
      // Opening the thread just marked its unread messages read server-side
      // (see ChatController.GetMessages) — clear the badge immediately rather
      // than waiting for the next poll tick.
      self.unreadCounts[parentUserId] = 0;
      scrollChatToBottom();
    });
  };

  // Computed props
  self.pendingRequests = function () {
    if (!self.tutor) return [];
    return self.bookings.filter(function (b) { return b.tutorId === self.tutor.id && b.status === 'pending'; });
  };

  var BOOKING_STATUS_ORDER = { pending: 0, countered: 1, confirmed: 2, completed: 3 };

  self.confirmedClasses = function () {
    if (!self.tutor) return [];
    return self.bookings.filter(function (b) {
      return b.tutorId === self.tutor.id && (b.status === 'confirmed' || b.status === 'countered');
    }).sort(function (a, b) {
      return BOOKING_STATUS_ORDER[a.status] - BOOKING_STATUS_ORDER[b.status];
    });
  };

  self.completedClasses = function () {
    if (!self.tutor) return [];
    return self.bookings.filter(function (b) { return b.tutorId === self.tutor.id && b.status === 'completed'; });
  };

  self.completedThisMonth = function () {
    var now = new Date();
    return self.completedClasses().filter(function (b) {
      return (b.classes || []).some(function (c) {
        var d = parseLocalDate(c.date);
        return !isNaN(d.getTime()) && d.getFullYear() === now.getFullYear() && d.getMonth() === now.getMonth();
      });
    });
  };

  self.needsReport = function () {
    return self.completedClasses().filter(function (b) { return !b.lessonReport; });
  };

  self.hasReport = function () {
    return self.completedClasses().filter(function (b) { return !!b.lessonReport; });
  };

  // ── Malaysia + Singapore public holidays ────────────────────────────
  var _holidays = {
    // 2024
    '2024-01-01':'New Year\'s Day','2024-01-25':'Chinese New Year','2024-01-26':'Chinese New Year',
    '2024-02-01':'Federal Territory Day','2024-02-24':'Thaipusam','2024-03-29':'Good Friday',
    '2024-04-10':'Hari Raya Puasa','2024-04-11':'Hari Raya Puasa','2024-05-01':'Labour Day',
    '2024-05-22':'Vesak Day','2024-06-03':'Agong\'s Birthday','2024-06-17':'Hari Raya Haji',
    '2024-07-07':'Awal Muharram','2024-08-09':'Singapore National Day','2024-08-31':'National Day (MY)',
    '2024-09-16':'Malaysia Day','2024-09-15':'Maulidur Rasul','2024-10-31':'Deepavali',
    '2024-12-25':'Christmas Day',
    // 2025
    '2025-01-01':'New Year\'s Day','2025-01-29':'Chinese New Year','2025-01-30':'Chinese New Year',
    '2025-02-01':'Federal Territory Day','2025-02-11':'Thaipusam','2025-03-31':'Hari Raya Puasa',
    '2025-04-01':'Hari Raya Puasa','2025-04-18':'Good Friday','2025-05-01':'Labour Day',
    '2025-05-12':'Vesak Day','2025-06-02':'Agong\'s Birthday','2025-06-07':'Hari Raya Haji',
    '2025-06-27':'Awal Muharram','2025-08-09':'Singapore National Day','2025-08-31':'National Day (MY)',
    '2025-09-05':'Maulidur Rasul','2025-09-16':'Malaysia Day','2025-10-20':'Deepavali',
    '2025-12-25':'Christmas Day',
    // 2026
    '2026-01-01':'New Year\'s Day','2026-01-31':'Thaipusam','2026-02-01':'Federal Territory Day',
    '2026-02-17':'Chinese New Year','2026-02-18':'Chinese New Year',
    '2026-03-20':'Hari Raya Puasa','2026-03-21':'Hari Raya Puasa','2026-04-03':'Good Friday',
    '2026-05-01':'Labour Day','2026-05-27':'Hari Raya Haji','2026-05-31':'Vesak Day',
    '2026-06-01':'Agong\'s Birthday','2026-06-17':'Awal Muharram',
    '2026-08-09':'Singapore National Day','2026-08-26':'Maulidur Rasul',
    '2026-08-31':'National Day (MY)','2026-09-16':'Malaysia Day',
    '2026-11-08':'Deepavali','2026-12-25':'Christmas Day',
    // 2027
    '2027-01-01':'New Year\'s Day','2027-01-20':'Thaipusam','2027-02-01':'Federal Territory Day',
    '2027-02-06':'Chinese New Year','2027-02-07':'Chinese New Year',
    '2027-03-10':'Hari Raya Puasa','2027-03-11':'Hari Raya Puasa','2027-03-26':'Good Friday',
    '2027-05-01':'Labour Day','2027-05-17':'Hari Raya Haji','2027-05-20':'Vesak Day',
    '2027-06-07':'Agong\'s Birthday','2027-06-07':'Awal Muharram',
    '2027-08-09':'Singapore National Day','2027-08-16':'Maulidur Rasul',
    '2027-08-31':'National Day (MY)','2027-09-16':'Malaysia Day',
    '2027-10-29':'Deepavali','2027-12-25':'Christmas Day'
  };

  self.isPublicHoliday = function (dayNum) {
    if (!dayNum) return false;
    return !!_holidays[calDayStr(dayNum)];
  };

  self.holidayName = function (dayNum) {
    if (!dayNum) return '';
    return _holidays[calDayStr(dayNum)] || '';
  };

  var _sgHolidayKeys = {
    '2024-02-24':1,'2024-03-29':1,'2024-08-09':1,
    '2025-02-11':1,'2025-04-18':1,'2025-08-09':1,
    '2026-01-31':1,'2026-04-03':1,'2026-08-09':1,
    '2027-01-20':1,'2027-03-26':1,'2027-08-09':1
  };

  self.isSgHoliday = function (dayNum) {
    if (!dayNum) return false;
    return !!_sgHolidayKeys[calDayStr(dayNum)];
  };

  self.isStartingSoon = function (dateStr) {
    if (!dateStr) return false;
    var today = new Date();
    today.setHours(0, 0, 0, 0);
    var classDate = parseLocalDate(dateStr);
    classDate.setHours(0, 0, 0, 0);
    var diff = Math.floor((classDate - today) / 86400000);
    return diff >= 0 && diff <= 3;
  };

  // ── Date blocking ────────────────────────────────────────────────────
  self.blockForm = { startDate: '', endDate: '' };
  self.blockedRanges = [];
  self.blockConflicts = [];
  self.blockOverlapError = '';
  self.blockPresetWarning = '';

  function parseLocalDate(val) {
    if (!val) return new Date(NaN);
    if (val instanceof Date) return new Date(val.getFullYear(), val.getMonth(), val.getDate());
    var s = String(val).trim();
    var dmyh = s.match(/^(\d{1,2})-(\d{1,2})-(\d{4})$/);
    if (dmyh) return new Date(+dmyh[3], +dmyh[2] - 1, +dmyh[1]);
    var dmy = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
    if (dmy) return new Date(+dmy[3], +dmy[2] - 1, +dmy[1]);
    var ymd = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (ymd) return new Date(+ymd[1], +ymd[2] - 1, +ymd[3]);
    var d = new Date(s); d.setHours(0, 0, 0, 0); return d;
  }

  // Minimum lead time before a class can be booked/changed, to avoid urgent last-minute changes.
  var MIN_LEAD_HOURS = 6;

  function combineDateTime(dateVal, timeStr) {
    var d = parseLocalDate(dateVal);
    if (isNaN(d.getTime())) return null;
    var m = String(timeStr || '').match(/(\d{1,2}):(\d{2})\s*(AM|PM)/i);
    if (!m) return d;
    var h = parseInt(m[1], 10);
    var min = parseInt(m[2], 10);
    var ampm = m[3].toUpperCase();
    if (ampm === 'PM' && h !== 12) h += 12;
    if (ampm === 'AM' && h === 12) h = 0;
    d.setHours(h, min, 0, 0);
    return d;
  }

  self.isTooSoon = function (dateVal, timeStr) {
    var dt = combineDateTime(dateVal, timeStr);
    if (!dt || isNaN(dt.getTime())) return false;
    var minAllowed = new Date(Date.now() + MIN_LEAD_HOURS * 60 * 60 * 1000);
    return dt < minAllowed;
  };

  self.hasTooSoonCounter = function () {
    return (self.counterForm.classes || []).some(function (c) {
      return c.proposedDate && self.isTooSoon(c.proposedDate, c.proposedTime);
    });
  };

  self.hasTooSoonTutorReschedule = function () {
    return (self.rescheduleForm.classes || []).some(function (c) {
      return c.proposedDate && self.isTooSoon(c.proposedDate, c.proposedTime);
    });
  };

  // A proposed date/time must be a valid, fully-formed value — a malformed or partial
  // entry (e.g. "05:00 A"), or an out-of-range hour with an AM/PM suffix tacked on
  // (e.g. "19:00 PM" — self-contradictory, can't be safely auto-fixed) is rejected
  // outright, not silently skipped.
  function isValidClockTime(timeStr) {
    var m = String(timeStr || '').match(/^(\d{1,2}):(\d{2})\s*(AM|PM)$/i);
    if (!m) return false;
    var h = parseInt(m[1], 10), min = parseInt(m[2], 10);
    return h >= 1 && h <= 12 && min >= 0 && min <= 59;
  }

  // Converts a bare 24-hour "HH:MM" time (no AM/PM suffix) into 12-hour "H:MM AM/PM"
  // format. Leaves anything else (already-AM/PM, or unparseable) unchanged.
  function normalizeTimeToAmPm(raw) {
    var s = String(raw || '').trim();
    var m = s.match(/^(\d{1,2}):(\d{2})$/);
    if (!m) return s;
    var h = parseInt(m[1], 10), min = parseInt(m[2], 10);
    if (h < 0 || h > 23 || min < 0 || min > 59) return s;
    var ampm = h >= 12 ? 'PM' : 'AM';
    var h12 = h % 12; if (h12 === 0) h12 = 12;
    return h12 + ':' + (min < 10 ? '0' + min : min) + ' ' + ampm;
  }

  self.hasInvalidCounter = function () {
    return (self.counterForm.classes || []).some(function (c) {
      if (c.proposedDate && isNaN(parseLocalDate(c.proposedDate).getTime())) return true;
      return !isValidClockTime(c.proposedStartTime);
    });
  };

  self.hasInvalidTutorReschedule = function () {
    return (self.rescheduleForm.classes || []).some(function (c) {
      if (c.proposedDate && isNaN(parseLocalDate(c.proposedDate).getTime())) return true;
      return !isValidClockTime(c.proposedStartTime);
    });
  };

  // A booking's classes can't overlap each other — same date, and their time ranges intersect
  // (this also catches exact-duplicate date+time as the simplest case of overlap).
  function parseTimeRangeMinutes(timeStr) {
    var matches = String(timeStr || '').match(/(\d{1,2}):(\d{2})\s*(AM|PM)/gi);
    if (!matches || matches.length < 2) return null;
    function toMinutes(t) {
      var m = t.match(/(\d{1,2}):(\d{2})\s*(AM|PM)/i);
      var h = parseInt(m[1], 10), min = parseInt(m[2], 10), ampm = m[3].toUpperCase();
      if (ampm === 'PM' && h !== 12) h += 12;
      if (ampm === 'AM' && h === 12) h = 0;
      return h * 60 + min;
    }
    var start = toMinutes(matches[0]);
    var end = toMinutes(matches[1]);
    if (end <= start) end += 24 * 60;
    return { start: start, end: end };
  }

  function hasOverlappingDateTimes(items, dateKey, timeKey) {
    var list = items.filter(function (item) { return item[dateKey] && item[timeKey]; });
    for (var i = 0; i < list.length; i++) {
      var r1 = parseTimeRangeMinutes(list[i][timeKey]);
      if (!r1) continue;
      var d1 = parseLocalDate(list[i][dateKey]);
      if (isNaN(d1.getTime())) continue;
      for (var j = i + 1; j < list.length; j++) {
        var d2 = parseLocalDate(list[j][dateKey]);
        if (isNaN(d2.getTime()) || d2.getTime() !== d1.getTime()) continue;
        var r2 = parseTimeRangeMinutes(list[j][timeKey]);
        if (!r2) continue;
        if (r1.start < r2.end && r2.start < r1.end) return true;
      }
    }
    return false;
  }

  self.hasDuplicateCounter = function () {
    return hasOverlappingDateTimes(self.counterForm.classes || [], 'proposedDate', 'proposedTime');
  };

  self.hasDuplicateTutorReschedule = function () {
    return hasOverlappingDateTimes(self.rescheduleForm.classes || [], 'proposedDate', 'proposedTime');
  };

  // A reschedule must keep the same session length as the original booking — parses a
  // "04:00 PM - 05:00 PM" range into hours and compares against the booking's durationHours.
  function parseTimeRangeHours(timeStr) {
    var matches = String(timeStr || '').match(/(\d{1,2}):(\d{2})\s*(AM|PM)/gi);
    if (!matches || matches.length < 2) return null;
    function toMinutes(t) {
      var m = t.match(/(\d{1,2}):(\d{2})\s*(AM|PM)/i);
      var h = parseInt(m[1], 10), min = parseInt(m[2], 10), ampm = m[3].toUpperCase();
      if (ampm === 'PM' && h !== 12) h += 12;
      if (ampm === 'AM' && h === 12) h = 0;
      return h * 60 + min;
    }
    var diff = toMinutes(matches[1]) - toMinutes(matches[0]);
    if (diff <= 0) diff += 24 * 60;
    return diff / 60;
  }

  self.isDurationMismatch = function (timeStr, expectedHours) {
    var hrs = parseTimeRangeHours(timeStr);
    return hrs !== null && Math.abs(hrs - expectedHours) > 0.001;
  };

  self.hasDurationMismatchTutorReschedule = function () {
    if (!self.rescheduleBooking) return false;
    return (self.rescheduleForm.classes || []).some(function (c) {
      return self.isDurationMismatch(c.proposedTime, self.rescheduleBooking.durationHours);
    });
  };

  self.confirmBlock = function () {
    var s = self.blockForm.startDate, e = self.blockForm.endDate;
    if (!s || !e) return;
    var start = parseLocalDate(s);
    var end   = parseLocalDate(e);
    if (end < start) return;

    self.blockOverlapError = '';

    var overlapsExisting = self.blockedRanges.some(function (r) {
      var rs = parseLocalDate(r.start), re = parseLocalDate(r.end);
      return start <= re && rs <= end;
    });
    if (overlapsExisting) {
      self.blockOverlapError = 'This date range overlaps with a period you already blocked. Remove or adjust it first.';
      return;
    }

    var conflicts = [];
    self.confirmedClasses().forEach(function (b) {
      // For countered bookings, check proposed dates (already rescheduled) not original dates
      var classesToCheck = (b.status === 'countered' && b.counterProposal && b.counterProposal.classes && b.counterProposal.classes.length)
        ? b.counterProposal.classes.map(function (cp) {
            return { date: cp.proposedDate || cp.originalDate, time: cp.proposedTime || cp.originalTime };
          })
        : (b.classes || []);
      classesToCheck.forEach(function (c) {
        var d = parseLocalDate(c.date);
        if (d >= start && d <= end) {
          conflicts.push({ bookingId: b.id, subject: b.subject, student: b.studentName, date: c.date, time: c.time, duration: b.durationHours });
        }
      });
    });

    if (conflicts.length) {
      self.blockConflicts = conflicts;
      return;
    }

    // Preset (Flow B) slots in the newly-blocked range: one with active bookings just
    // gets a heads-up (existing students are unaffected, it just won't take new ones);
    // one nobody has booked yet is quietly removed since it'll never be filled now.
    self.blockPresetWarning = '';
    (self.tutor.timetable || []).forEach(function (slot) {
      if (!slot.mode) return; // not a preset slot
      var d = parseLocalDate(slot.day);
      if (d < start || d > end) return;
      if (slot.confirmedCount > 0 && !slot.isFull) {
        self.blockPresetWarning = 'This date has active class bookings. Blocking it will not affect existing students but no new bookings will be accepted.';
      } else if (slot.confirmedCount === 0 && !slot.isFull) {
        TutorService.deleteSlot(self.tutor.id, slot.id);
      }
    });

    self.blockConflicts = [];
    ScheduleService.addBlock(self.tutor.id, s, e); // normalises to YYYY-MM-DD strings
    self.blockForm.startDate = '';
    self.blockForm.endDate = '';
  };

  self.clearBlockConflicts = function () { self.blockConflicts = []; self.blockOverlapError = ''; self.blockPresetWarning = ''; };

  self.removeBlock = function (idx) {
    self.blockedRanges.splice(idx, 1);
  };

  self.isBlocked = function (dayNum) {
    if (!dayNum) return false;
    var d = new Date(calDayStr(dayNum));
    d.setHours(0, 0, 0, 0);
    return self.blockedRanges.some(function (r) {
      var s = new Date(r.start); s.setHours(0, 0, 0, 0);
      var e = new Date(r.end);   e.setHours(0, 0, 0, 0);
      return d >= s && d <= e;
    });
  };

  self.isPastDay = function (dayNum) {
    if (!dayNum) return false;
    var d = new Date(self.calYear, self.calMonth, dayNum);
    d.setHours(0, 0, 0, 0);
    var today = new Date();
    today.setHours(0, 0, 0, 0);
    return d < today;
  };

  // ── Monthly calendar ────────────────────────────────────────────────
  var _monthNames = ['January','February','March','April','May','June',
                     'July','August','September','October','November','December'];

  self.calMonthName = function () { return _monthNames[self.calMonth]; };

  self.prevMonth = function () {
    if (self.calMonth === 0) { self.calMonth = 11; self.calYear--; }
    else { self.calMonth--; }
    self.selectedCalDays = [];
  };

  self.nextMonth = function () {
    if (self.calMonth === 11) { self.calMonth = 0; self.calYear++; }
    else { self.calMonth++; }
    self.selectedCalDays = [];
  };

  self.calDaysArray = function () {
    var n = new Date(self.calYear, self.calMonth + 1, 0).getDate();
    var arr = [];
    for (var i = 0; i < n; i++) arr.push(i + 1);
    return arr;
  };

  self.calOffsetArray = function () {
    var d = new Date(self.calYear, self.calMonth, 1).getDay();
    var offset = d === 0 ? 6 : d - 1;
    var arr = [];
    for (var i = 0; i < offset; i++) arr.push(i);
    return arr;
  };

  function calDayStr(dayNum) {
    var m = String(self.calMonth + 1).padStart(2, '0');
    var d = String(dayNum).padStart(2, '0');
    return self.calYear + '-' + m + '-' + d;
  }

  // Multi-date selection for "Setup class" (up to 5 dates at once). Blocked/past days
  // can't be selected — same rule the single-day calendar already enforced.
  self.toggleCalDay = function (d) {
    if (!d || self.isPastDay(d) || self.isBlocked(d)) return;
    var idx = self.selectedCalDays.indexOf(d);
    if (idx > -1) { self.selectedCalDays.splice(idx, 1); return; }
    if (self.selectedCalDays.length >= 5) return;
    self.selectedCalDays.push(d);
  };

  self.isCalDaySelected = function (d) { return self.selectedCalDays.indexOf(d) > -1; };

  // Selected days in date order for the day-detail panel below the calendar —
  // returns a fresh array each call, but ng-repeat uses "track by day" on the
  // primitive value itself, so identity stays stable across digests regardless.
  self.sortedSelectedCalDays = function () {
    return self.selectedCalDays.slice().sort(function (a, b) { return a - b; });
  };

  self.resetCalDaySelection = function () { self.selectedCalDays = []; };

  self.bookingsOnDay = function (dayNum) {
    if (!self.tutor || !dayNum) return [];
    var s = calDayStr(dayNum);
    return self.bookings.filter(function (b) {
      return b.tutorId === self.tutor.id && b.status !== 'cancelled' &&
        b.classes && b.classes.some(function (c) { return c.date === s; });
    });
  };

  self.classOnDay = function (booking, dayNum) {
    if (!booking || !booking.classes || !dayNum) return null;
    var s = calDayStr(dayNum);
    return booking.classes.find(function (c) { return c.date === s; }) || null;
  };

  self.confirmedOnDay = function (dayNum) {
    return self.bookingsOnDay(dayNum).filter(function (b) { return b.status === 'confirmed'; });
  };

  // A day's dot reflects ALL bookings that day, not just the first one found —
  // any unpaid booking takes priority (signals action needed) over an all-paid day.
  self.dayHasUnpaidBooking = function (dayNum) {
    return self.bookingsOnDay(dayNum).some(function (b) { return !self.isPaid(b.id); });
  };

  self.dayAllBookingsPaid = function (dayNum) {
    var list = self.bookingsOnDay(dayNum);
    return list.length > 0 && list.every(function (b) { return self.isPaid(b.id); });
  };

  // Preset (tutor-published) class slots on a given day — these are separate from
  // actual bookings: a slot only becomes a booking once a parent books it. Sourced
  // from self.tutor.timetable, which includes every TutorTimeSlot for this tutor;
  // slot.mode set is what distinguishes a preset-class slot from a plain
  // availability block (see the same check in confirmBlock() above).
  self.publishedSlotsOnDay = function (dayNum) {
    if (!self.tutor || !self.tutor.timetable || !dayNum) return [];
    var s = calDayStr(dayNum);
    return self.tutor.timetable.filter(function (slot) {
      return slot.mode && slot.day === s;
    });
  };

  // Cancelling a published slot that already has confirmed/pending student
  // bookings needs the tutor to choose up front: propose a replacement
  // date/time (each affected family then Accepts/Rejects individually, no
  // penalty either way — see PresetCancellationsController), or cancel outright
  // with nothing offered (every affected family is credited immediately, and a
  // penalty + dispute-score hit applies — see IPresetCancellationService).
  self.cancelSlotBusy = false;
  self.cancelSlotModal = null; // the slot being cancelled, or null when closed
  self.cancelSlotForm = { mode: 'reschedule', proposedDate: '', proposedTime: '04:00 PM', proposedEndTime: '05:00 PM' };
  self.cancelSlotError = '';

  self.openCancelSlotModal = function (slot) {
    self.cancelSlotModal = slot;
    self.cancelSlotForm = { mode: 'reschedule', proposedDate: '', proposedTime: '04:00 PM', proposedEndTime: '05:00 PM' };
    self.cancelSlotError = '';
  };

  self.closeCancelSlotModal = function () {
    self.cancelSlotModal = null;
  };

  self.submitCancelSlot = function () {
    var slot = self.cancelSlotModal;
    if (!slot) return;
    self.cancelSlotError = '';

    var body = null;
    if (self.cancelSlotForm.mode === 'reschedule') {
      if (!self.cancelSlotForm.proposedDate || !self.cancelSlotForm.proposedTime || !self.cancelSlotForm.proposedEndTime) {
        self.cancelSlotError = 'Please fill in the proposed date, start time, and end time.';
        return;
      }
      // fp-date binds "DD-MM-YYYY" — convert to "YYYY-MM-DD" to match slot.Day's
      // format (same conversion used for reschedule proposals, see submitCounter).
      var d = parseLocalDate(self.cancelSlotForm.proposedDate);
      if (isNaN(d.getTime())) {
        self.cancelSlotError = 'Please enter a valid proposed date.';
        return;
      }
      var mm = (d.getMonth() + 1 < 10 ? '0' : '') + (d.getMonth() + 1);
      var dd = (d.getDate() < 10 ? '0' : '') + d.getDate();
      body = {
        proposedDate: d.getFullYear() + '-' + mm + '-' + dd,
        proposedTime: self.cancelSlotForm.proposedTime,
        proposedEndTime: self.cancelSlotForm.proposedEndTime
      };
    }

    self.cancelSlotBusy = true;
    TutorService.deleteSlot(self.tutor.id, slot.id, body).then(function () {
      self.cancelSlotBusy = false;
      self.cancelSlotModal = null;
      TutorService.getByUser(user.userId).then(function (res) {
        self.tutor = res.data;
        rebuildSetupClassUniqueSubjects();
      });
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
    }).catch(function (err) {
      self.cancelSlotBusy = false;
      self.cancelSlotError = (err.data && err.data.message) ? err.data.message : 'Failed to cancel. Please try again.';
    });
  };

  // ── Setup Class (tutor-preset slots, Flow B) ──────────────────────────
  self.setupClassOpen = false;
  self.setupClassSaving = false;
  self.setupClassError = '';
  self.setupClassForm = {
    mode: '', subject: '', level: '', country: '',
    classSize: 'one-to-one', maxStudents: 1,
    pricePerLesson: 0
  };
  self.setupClassSlots = {}; // { "YYYY-MM-DD": ["09:00 AM", ...], ... }

  // Grid rows are 30-minute slots (8:00 AM – 9:30 PM) — each row IS an exact,
  // directly-selectable start time, expressed as total minutes since midnight.
  self.setupClassGridSlots = [];
  for (var _gm = 8 * 60; _gm <= 21 * 60 + 30; _gm += 30) self.setupClassGridSlots.push(_gm);

  self.setupClassGridSlotLabel = function (totalMin) {
    return clockFromMinutes(totalMin);
  };

  function clockFromMinutes(totalMin) {
    var t = ((totalMin % (24 * 60)) + 24 * 60) % (24 * 60);
    var h = Math.floor(t / 60);
    var m = t % 60;
    var ampm = h >= 12 ? 'PM' : 'AM';
    var h12 = h % 12; if (h12 === 0) h12 = 12;
    return h12 + ':' + (m < 10 ? '0' + m : m) + ' ' + ampm;
  }

  self.setupClassSelectedDates = function () {
    return self.selectedCalDays.slice().sort(function (a, b) { return a - b; }).map(calDayStr);
  };

  self.setupClassSelectedDatesLabel = function () {
    var days = self.selectedCalDays.slice().sort(function (a, b) { return a - b; });
    if (!days.length) return '';
    return days.join(', ') + ' · ' + self.calYear;
  };

  // Setup Class needs at least one registered offering + teaching mode to
  // publish against — without either, the Mode/Subject dropdowns would just be
  // empty. Whether that's blocking (and what message explains it) also depends
  // on verification status — see the Setup Class modal template.
  self.tutorHasOfferings = function () {
    return !!(self.tutor && self.tutor.offerings && self.tutor.offerings.length &&
      self.tutor.modes && self.tutor.modes.length);
  };

  self.openSetupClass = function () {
    if (!self.selectedCalDays.length || !self.tutor) return;
    rebuildSetupClassUniqueSubjects();
    self.setupClassSlots = {};
    self.setupClassSelectedDates().forEach(function (key) { self.setupClassSlots[key] = []; });
    self.setupClassForm.mode = self.tutor.modes && self.tutor.modes[0] ? self.tutor.modes[0] : '';
    var firstOffering = self.tutor.offerings && self.tutor.offerings[0];
    self.setupClassForm.subject = firstOffering ? firstOffering.subject : '';
    self.setupClassForm.level = firstOffering ? firstOffering.level : '';
    self.setupClassForm.country = firstOffering ? firstOffering.country : '';
    self.setupClassForm.classSize = 'one-to-one';
    self.setupClassForm.maxStudents = 1;
    self.setupClassError = '';
    self._slotDrag = null;
    self.setupClassOpen = true;
  };

  self.closeSetupClass = function () { self.setupClassOpen = false; self._slotDrag = null; };

  // Grid rows carry their own exact time (30-min resolution) — a direct conversion,
  // no separate start-time spinner needed (removed along with the now-unused
  // Duration dropdown and time-summary pill; each class's time now comes entirely
  // from which grid cell(s) were clicked/dragged, see makeSlotRange below).
  self.setupClassTimeAt = function (totalMin) {
    return clockFromMinutes(totalMin);
  };

  // setupClassSlots[dateStr] holds an array of *ranges* — { startMin, endMin, start, end } —
  // not individual 30-min entries. Each range is ONE class: dragging across several grid
  // cells combines them into a single range spanning the first cell's start time to the
  // last cell's end time, rather than creating one class per cell.
  function makeSlotRange(startMin, endMin) {
    return { startMin: startMin, endMin: endMin, start: clockFromMinutes(startMin), end: clockFromMinutes(endMin) };
  }

  self.isSetupSlotSelected = function (dateStr, totalMin) {
    return (self.setupClassSlots[dateStr] || []).some(function (r) {
      return totalMin >= r.startMin && totalMin < r.endMin;
    });
  };

  // The combined "start – end" label is only shown on the first cell of a range,
  // since the cells after it belong to the same class, not separate ones.
  self.setupSlotRangeLabel = function (dateStr, totalMin) {
    var r = (self.setupClassSlots[dateStr] || []).find(function (r) { return r.startMin === totalMin; });
    return r ? (r.start + ' – ' + r.end) : '';
  };

  function parseTimeRangeMinutesForBooked(timeStr) {
    var matches = String(timeStr || '').match(/(\d{1,2}):(\d{2})\s*(AM|PM)/gi);
    if (!matches || matches.length < 2) return null;
    function toMin(t) {
      var mm = t.match(/(\d{1,2}):(\d{2})\s*(AM|PM)/i);
      var hh = parseInt(mm[1], 10) % 12, min = parseInt(mm[2], 10);
      if (mm[3].toUpperCase() === 'PM') hh += 12;
      return hh * 60 + min;
    }
    var start = toMin(matches[0]), end = toMin(matches[1]);
    if (end <= start) end += 24 * 60;
    return { start: start, end: end };
  }

  // Marks a grid cell as unavailable if it overlaps EITHER an existing confirmed/
  // countered booking OR a preset slot this tutor has already published (whether
  // booked or not) — both count as "an existing class", so neither can be
  // double-booked over from this grid.
  self.isSetupSlotBooked = function (dateStr, totalMin) {
    if (!self.tutor) return false;
    var slotStart = totalMin, slotEnd = totalMin + 30;

    var overlapsBooking = self.bookings.some(function (b) {
      if (b.tutorId !== self.tutor.id || (b.status !== 'confirmed' && b.status !== 'countered')) return false;
      return (b.classes || []).some(function (c) {
        if (c.date !== dateStr) return false;
        var range = parseTimeRangeMinutesForBooked(c.time);
        return range && slotStart < range.end && range.start < slotEnd;
      });
    });
    if (overlapsBooking) return true;

    return (self.tutor.timetable || []).some(function (slot) {
      if (!slot.mode || slot.day !== dateStr) return false;
      var range = parseTimeRangeMinutesForBooked(slot.time + ' - ' + (slot.endTime || slot.time));
      return range && slotStart < range.end && range.start < slotEnd;
    });
  };

  self.toggleSetupSlot = function (dateStr, totalMin) {
    if (self.isSetupSlotBooked(dateStr, totalMin)) return;
    var ranges = self.setupClassSlots[dateStr] || [];
    var idx = ranges.findIndex(function (r) { return totalMin >= r.startMin && totalMin < r.endMin; });
    if (idx > -1) ranges.splice(idx, 1);
    else ranges.push(makeSlotRange(totalMin, totalMin + 30));
    self.setupClassSlots[dateStr] = ranges;
  };

  // ── Drag-to-select a time range on the Setup Class grid ──────────────
  // A plain click (mousedown+mouseup on the same cell, no drag) still toggles
  // just that one cell. Dragging across multiple cells in the same date column
  // combines the whole span into ONE class — first cell's start time to last
  // cell's end time — rather than one class per cell. The drag can't extend
  // through an already-occupied cell (existing booking or published slot), so a
  // combined range never silently overlaps something that's already there. Drag
  // is confined to a single date column; moving into a different column is ignored.
  self._slotDrag = null;

  function slotRangeHasOccupiedCell(dateStr, loMin, hiMinExclusive) {
    for (var m = loMin; m < hiMinExclusive; m += 30) {
      if (self.isSetupSlotBooked(dateStr, m)) return true;
    }
    return false;
  }

  self.startSlotDrag = function (dateStr, totalMin, $event) {
    if ($event) $event.preventDefault();
    self._slotDrag = { dateStr: dateStr, startSlot: totalMin, endSlot: totalMin };
  };

  self.dragOverSlot = function (dateStr, totalMin) {
    if (!self._slotDrag || dateStr !== self._slotDrag.dateStr) return;
    var lo = Math.min(self._slotDrag.startSlot, totalMin);
    var hi = Math.max(self._slotDrag.startSlot, totalMin);
    if (slotRangeHasOccupiedCell(dateStr, lo, hi + 30)) return; // don't drag through an occupied cell
    self._slotDrag.endSlot = totalMin;
  };

  self.isSetupSlotDragPreview = function (dateStr, totalMin) {
    if (!self._slotDrag || dateStr !== self._slotDrag.dateStr) return false;
    var lo = Math.min(self._slotDrag.startSlot, self._slotDrag.endSlot);
    var hi = Math.max(self._slotDrag.startSlot, self._slotDrag.endSlot);
    return totalMin >= lo && totalMin <= hi;
  };

  self.endSlotDrag = function () {
    if (!self._slotDrag) return;
    var d = self._slotDrag;
    self._slotDrag = null;
    var lo = Math.min(d.startSlot, d.endSlot);
    var hi = Math.max(d.startSlot, d.endSlot) + 30; // exclusive upper bound, covers the last cell's full 30 min

    if (lo === hi - 30) {
      // Plain click, no real drag — toggle behaviour (existing single-cell semantics)
      self.toggleSetupSlot(d.dateStr, lo);
      return;
    }
    // Combine into one range spanning the whole drag. Any existing ranges the new
    // span touches are replaced by it (redefining that stretch of time), rather
    // than kept alongside it — avoids overlapping classes on the same date.
    var ranges = (self.setupClassSlots[d.dateStr] || []).filter(function (r) {
      return r.endMin <= lo || r.startMin >= hi;
    });
    ranges.push(makeSlotRange(lo, hi));
    self.setupClassSlots[d.dateStr] = ranges;
  };

  self.setupClassTotalSlots = function () {
    return Object.keys(self.setupClassSlots).reduce(function (sum, k) {
      return sum + (self.setupClassSlots[k] || []).length;
    }, 0);
  };

  self.onClassSizeChange = function () {
    if (self.setupClassForm.classSize === 'one-to-one') self.setupClassForm.maxStudents = 1;
    else if (!self.setupClassForm.maxStudents || self.setupClassForm.maxStudents < 2) self.setupClassForm.maxStudents = 2;
  };

  // Rebuilt once (not called from ng-repeat directly) — a function called from the
  // view that allocates new objects every digest never stabilizes: ng-repeat tracks
  // items by identity by default, so a fresh array/objects every digest tears down
  // and rebuilds every <option>, which was resetting the Subject <select> back to
  // blank on every digest (same trap computeContactableParents() above avoids).
  self.setupClassUniqueSubjectsList = [];

  function rebuildSetupClassUniqueSubjects() {
    if (!self.tutor || !self.tutor.offerings) { self.setupClassUniqueSubjectsList = []; return; }
    var seen = {}, out = [];
    self.tutor.offerings.forEach(function (o) {
      var key = o.subject + ' (' + o.level + ')';
      if (seen[key]) return;
      seen[key] = true;
      out.push({ key: key, subject: o.subject, level: o.level, country: o.country });
    });
    self.setupClassUniqueSubjectsList = out;
  }

  self.onSubjectChange = function () {
    var match = self.setupClassUniqueSubjectsList.find(function (o) { return o.subject === self.setupClassForm.subject; });
    if (match) { self.setupClassForm.level = match.level; self.setupClassForm.country = match.country; }
  };

  self.submitSetupClass = function () {
    self.setupClassError = '';
    if (self.setupClassTotalSlots() === 0) { self.setupClassError = 'Please select at least one time slot.'; return; }
    if (!self.setupClassForm.mode) { self.setupClassError = 'Please select a teaching mode.'; return; }
    if (!self.setupClassForm.subject) { self.setupClassError = 'Please select a subject.'; return; }
    if (!self.setupClassForm.pricePerLesson || self.setupClassForm.pricePerLesson <= 0) {
      self.setupClassError = 'Please enter a price per lesson.';
      return;
    }

    // Each range already carries its own accurate start/end (first cell's start
    // through last cell's end for a dragged/combined class), so its own span is
    // sent as-is rather than derived from the form's single duration dropdown —
    // that duration only applies to a plain single-cell click/selection.
    var slots = [];
    Object.keys(self.setupClassSlots).forEach(function (dateStr) {
      (self.setupClassSlots[dateStr] || []).forEach(function (r) {
        slots.push({ date: dateStr, startTime: r.start, endTime: r.end, durationMinutes: r.endMin - r.startMin });
      });
    });

    self.setupClassSaving = true;
    TutorService.setupClass(self.tutor.id, {
      slots: slots,
      mode: self.setupClassForm.mode,
      subject: self.setupClassForm.subject,
      level: self.setupClassForm.level,
      country: self.setupClassForm.country,
      classSize: self.setupClassForm.classSize,
      maxStudents: self.setupClassForm.maxStudents,
      pricePerLesson: self.setupClassForm.pricePerLesson
    }).then(function () {
      self.setupClassSaving = false;
      self.setupClassOpen = false;
      self.selectedCalDays = [];
      TutorService.getByUser(user.userId).then(function (res) {
        self.tutor = res.data;
        rebuildSetupClassUniqueSubjects();
      });
    }).catch(function (err) {
      self.setupClassSaving = false;
      self.setupClassError = (err.data && err.data.message) ? err.data.message : 'Failed to save. Please try again.';
    });
  };

  self.getInvoice = function (bookingId) {
    return self.invoices.find(function (i) { return i.bookingId === bookingId; });
  };

  self.isPaid = function (bookingId) {
    var inv = self.getInvoice(bookingId);
    return inv && inv.status === 'Paid';
  };

  // Actions
  self.confirmBooking = function (booking) {
    BookingService.updateStatus(booking.id, 'confirmed', null).then(function () {
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
    });
  };

  // Extracts just the start time ("04:00 PM") out of a "04:00 PM - 05:00 PM" range.
  function extractStartTime(rangeStr) {
    var m = String(rangeStr || '').match(/(\d{1,2}:\d{2}\s*(?:AM|PM))/i);
    return m ? m[1] : '';
  }

  // Given a start time and a duration in hours, computes the end time ("05:30 PM").
  // Returns '' if startTime isn't a recognizable "H:MM AM/PM" value.
  function calcEndTimeFromDuration(startTime, durationHours) {
    var m = String(startTime || '').match(/^(\d{1,2}):(\d{2})\s*(AM|PM)$/i);
    if (!m) return '';
    var h = parseInt(m[1], 10);
    var min = parseInt(m[2], 10);
    var ampm = m[3].toUpperCase();
    if (ampm === 'PM' && h !== 12) h += 12;
    if (ampm === 'AM' && h === 12) h = 0;
    var total = h * 60 + min + Math.round((durationHours || 1) * 60);
    var endH = Math.floor(total / 60) % 24;
    var endM = total % 60;
    var endAmpm = endH >= 12 ? 'PM' : 'AM';
    var displayH = endH % 12;
    if (displayH === 0) displayH = 12;
    return displayH + ':' + (endM < 10 ? '0' + endM : endM) + ' ' + endAmpm;
  }

  self.counterEndTime = function (c) {
    return calcEndTimeFromDuration(c.proposedStartTime, self.counterBooking && self.counterBooking.durationHours);
  };

  self.onCounterStartTimeChange = function (c) {
    c.proposedStartTime = normalizeTimeToAmPm(c.proposedStartTime);
    var end = self.counterEndTime(c);
    c.proposedTime = end ? (c.proposedStartTime + ' - ' + end) : c.proposedStartTime;
  };

  self.rescheduleEndTime = function (c) {
    return calcEndTimeFromDuration(c.proposedStartTime, self.rescheduleBooking && self.rescheduleBooking.durationHours);
  };

  self.onTutorRescheduleStartTimeChange = function (c) {
    c.proposedStartTime = normalizeTimeToAmPm(c.proposedStartTime);
    var end = self.rescheduleEndTime(c);
    c.proposedTime = end ? (c.proposedStartTime + ' - ' + end) : c.proposedStartTime;
  };

  self.startCounter = function (booking) {
    self.counterBooking = booking;
    self.counterForm = {
      message: '',
      classes: (booking.classes || []).map(function (c) {
        return { originalDate: c.date, originalTime: c.time, proposedDate: c.date, proposedTime: c.time, proposedStartTime: extractStartTime(c.time) };
      })
    };
    self.counterSuccess = false;
  };

  self.hasProfaneCounterMessage = function () {
    return ProfanityFilterService.containsProfanity(self.counterForm.message);
  };

  self.submitCounter = function () {
    if (!self.counterBooking) return;
    if (self.hasInvalidCounter()) return;
    if (self.hasTooSoonCounter()) return;
    if (self.hasDuplicateCounter()) return;
    if (self.hasProfaneCounterMessage()) return;
    BookingService.updateStatus(self.counterBooking.id, 'countered', {
      message: self.counterForm.message,
      classes: self.counterForm.classes.map(function (c) {
        var d = parseLocalDate(c.proposedDate);
        if (isNaN(d.getTime())) return { originalDate: c.originalDate, originalTime: c.originalTime, proposedDate: '', proposedTime: c.proposedTime };
        var mm = (d.getMonth() + 1 < 10 ? '0' : '') + (d.getMonth() + 1);
        var dd = (d.getDate() < 10 ? '0' : '') + d.getDate();
        return { originalDate: c.originalDate, originalTime: c.originalTime, proposedDate: d.getFullYear() + '-' + mm + '-' + dd, proposedTime: c.proposedTime };
      })
    }).then(function () {
      self.counterSuccess = true;
      self.counterBooking = null;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.counterSuccess = false;
      }, 2000);
    });
  };

  self.cancelCounter = function () { self.counterBooking = null; };

  self.isTutorBookingConflicted = function (booking) {
    if (!booking) return false;
    // Pending: block was attempted but rejected — conflict alert is showing
    if (self.blockConflicts && self.blockConflicts.length) {
      if (self.blockConflicts.some(function (c) { return c.bookingId === booking.id; })) {
        return true;
      }
    }
    // Committed: block is confirmed in ScheduleService
    // For countered bookings, check proposed dates (already rescheduled) not original dates
    if (!self.tutor) return false;
    var datesToCheck = (booking.status === 'countered' && booking.counterProposal && booking.counterProposal.classes && booking.counterProposal.classes.length)
      ? booking.counterProposal.classes.map(function (cp) { return cp.proposedDate || cp.originalDate; })
      : (booking.classes || []).map(function (c) { return c.date; });
    return datesToCheck.some(function (date) {
      return ScheduleService.isBlocked(self.tutor.id, date);
    });
  };

  self.startTutorReschedule = function (booking) {
    self.rescheduleBooking = booking;
    self.rescheduleForm.classes = (booking.classes || []).map(function (c) {
      return { originalDate: c.date, originalTime: c.time, proposedDate: c.date, proposedTime: c.time, proposedStartTime: extractStartTime(c.time) };
    });
    self.rescheduleSuccess = false;
  };

  self.startTutorRescheduleById = function (bookingId) {
    var b = self.bookings.find(function (x) { return x.id === bookingId; });
    if (b) self.startTutorReschedule(b);
  };

  self.cancelTutorReschedule = function () {
    self.rescheduleBooking = null;
    self.rescheduleForm = { classes: [] };
  };

  self.isProposedDateBlocked = function (dateVal) {
    if (!dateVal || !self.tutor) return false;
    var d = parseLocalDate(dateVal);
    if (isNaN(d.getTime())) return false;
    var mm = (d.getMonth() + 1 < 10 ? '0' : '') + (d.getMonth() + 1);
    var dd = (d.getDate() < 10 ? '0' : '') + d.getDate();
    if (ScheduleService.isBlocked(self.tutor.id, d.getFullYear() + '-' + mm + '-' + dd)) return true;
    // Also check pending block range (conflicts shown but block not yet committed)
    if (self.blockConflicts && self.blockConflicts.length && self.blockForm.startDate && self.blockForm.endDate) {
      var s = parseLocalDate(self.blockForm.startDate);
      var e = parseLocalDate(self.blockForm.endDate);
      if (!isNaN(s.getTime()) && !isNaN(e.getTime())) return d >= s && d <= e;
    }
    return false;
  };

  // Checks if an original class date is blocked — via committed ScheduleService ranges
  // OR via the pending blockConflicts list (block attempted but not yet confirmed).
  self.isOriginalDateBlocked = function (dateVal) {
    if (!dateVal) return false;
    if (self.isProposedDateBlocked(dateVal)) return true;
    if (self.blockConflicts && self.blockConflicts.length && self.rescheduleBooking) {
      return self.blockConflicts.some(function (c) {
        return c.bookingId === self.rescheduleBooking.id && c.date === dateVal;
      });
    }
    return false;
  };

  self.submitTutorReschedule = function () {
    if (!self.rescheduleBooking) return;
    if (self.hasInvalidTutorReschedule()) return;
    if (self.hasTooSoonTutorReschedule()) return;
    if (self.hasDurationMismatchTutorReschedule()) return;
    if (self.hasDuplicateTutorReschedule()) return;
    BookingService.updateStatus(self.rescheduleBooking.id, 'countered', {
      message: 'Tutor proposed reschedule',
      classes: self.rescheduleForm.classes.map(function (c) {
        var d = parseLocalDate(c.proposedDate);
        if (isNaN(d.getTime())) return { originalDate: c.originalDate, proposedDate: '', proposedTime: c.proposedTime };
        var mm = (d.getMonth() + 1 < 10 ? '0' : '') + (d.getMonth() + 1);
        var dd = (d.getDate() < 10 ? '0' : '') + d.getDate();
        return { originalDate: c.originalDate, proposedDate: d.getFullYear() + '-' + mm + '-' + dd, proposedTime: c.proposedTime };
      })
    }).then(function () {
      self.rescheduleSuccess = true;
      self.blockConflicts = [];
      self.cancelTutorReschedule();
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.rescheduleSuccess = false;
      }, 2500);
    });
  };

  self.counterAcceptSuccess = false;
  self.acceptCounterProposal = function (booking) {
    BookingService.updateStatus(booking.id, 'confirmed', null).then(function () {
      self.counterAcceptSuccess = true;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.counterAcceptSuccess = false;
      }, 2500);
    });
  };

  self.startReport = function (booking) {
    self.reportBooking = booking;
    self.reportForm = { covered: '', performance: '', homework: '' };
    self.reportSuccess = false;
    self.reportError = '';
  };

  self.submitReport = function () {
    if (!self.reportBooking) return;
    self.reportError = ProfanityFilterService.validate(self.reportForm.covered)
      || ProfanityFilterService.validate(self.reportForm.performance)
      || ProfanityFilterService.validate(self.reportForm.homework);
    if (self.reportError) return;
    BookingService.submitLessonReport(self.reportBooking.id, self.reportForm).then(function () {
      self.reportSuccess = true;
      self.reportBooking = null;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.reportSuccess = false;
      }, 2000);
    }, function (err) {
      self.reportError = (err.data && err.data.message) ? err.data.message : 'Failed to publish report. Please try again.';
    });
  };

  self.cancelReport = function () { self.reportBooking = null; };

  self.startEdit = function (booking) {
    self.editBooking = booking;
    self.editForm = {
      covered: booking.lessonReport.covered,
      performance: booking.lessonReport.performance,
      homework: booking.lessonReport.homework,
      changesMade: ''
    };
    self.editSuccess = false;
    self.editError = '';
  };

  self.submitEdit = function () {
    if (!self.editBooking) return;
    self.editError = ProfanityFilterService.validate(self.editForm.covered)
      || ProfanityFilterService.validate(self.editForm.performance)
      || ProfanityFilterService.validate(self.editForm.homework)
      || ProfanityFilterService.validate(self.editForm.changesMade);
    if (self.editError) return;
    BookingService.editLessonReport(self.editBooking.id, self.editForm).then(function () {
      self.editSuccess = true;
      self.editBooking = null;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.editSuccess = false;
      }, 2000);
    }, function (err) {
      self.editError = (err.data && err.data.message) ? err.data.message : 'Failed to save changes. Please try again.';
    });
  };

  self.cancelEdit = function () { self.editBooking = null; };

  // Edit profile offerings
  self.addOffering = function () {
    self.offeringLockedError = '';
    if (!self.tutor.offeringsUnlocked) {
      self.offeringLockedError = 'Complete your profile verification before adding subject offerings.';
      return;
    }
    var opt = self.newOffering.selectedOption;
    if (!self.newOffering.country || !opt || !self.newOffering.qualification || !self.modesRight.length) return;
    var price = parseFloat(self.newOffering.price) || 0;
    // One offering combo per mode currently in "Offered" — the offering builder no
    // longer asks for mode separately, it always matches whatever's selected above.
    self.modesRight.forEach(function (m) {
      var exists = self.profileForm.offerings.some(function (o) {
        return o.country === self.newOffering.country && o.subject === opt.subject &&
          o.level === opt.level && o.mode === m.mode && o.qualification === self.newOffering.qualification;
      });
      if (exists) return;
      self.profileForm.offerings.push({
        country: self.newOffering.country,
        subject: opt.subject,
        level: opt.level,
        mode: m.mode,
        qualification: self.newOffering.qualification,
        price: price
      });
    });
    self.newOffering = { country: self.newOffering.country, selectedOption: null, qualification: '', price: null };
  };

  self.removeOffering = function (index) {
    self.profileForm.offerings.splice(index, 1);
  };

  self.onFileSelect = function (element) {
    var file = element.files[0];
    if (!file) return;
    self.uploading = true;
    self.profileError = '';
    TutorService.uploadImage(file).then(function (res) {
      self.profileForm.imageUrl = res.data.url;
      self.uploading = false;
    }, function () {
      self.profileError = 'Image upload failed. Please try again.';
      self.uploading = false;
    });
  };

  self.toggleOnlineStatus = function () {
    if (!self.tutor) return;
    TutorService.updateOnlineStatus(self.tutor.id, !self.tutor.isOnline).then(function (res) {
      self.tutor = res.data;
    });
  };

  self.saveProfile = function () {
    if (!self.tutor) return;
    self.profileSuccess = false;
    self.profileError = '';
    self.modesError = '';
    if (!self.profileForm.imageUrl) {
      self.profileError = 'Please upload a profile photo before saving.';
      return;
    }
    if (!self.modesRight.length) {
      self.modesError = 'Please select at least one teaching mode.';
      return;
    }
    var bioProfanityError = ProfanityFilterService.validate(self.profileForm.bio);
    if (bioProfanityError) {
      self.profileError = bioProfanityError;
      return;
    }
    var payload = {
      imageUrl: self.profileForm.imageUrl,
      bio: self.profileForm.bio,
      experienceYears: self.profileForm.experienceYears,
      offerings: self.profileForm.offerings
    };
    var modes = self.modesRight.map(function (m) { return m.mode; });
    TutorService.update(self.tutor.id, payload).then(function () {
      return TutorService.updateModes(self.tutor.id, modes);
    }).then(function (res) {
      self.tutor = res.data;
      rebuildSetupClassUniqueSubjects();
      self.profileSuccess = true;
      $timeout(function () { self.profileSuccess = false; }, 3000);
    }, function () {
      self.profileError = 'Failed to save. Please try again.';
    });
  };

  // ── Verification helpers ──────────────────────────────────────────
  self.verif = {
    idType: 'NRIC',
    idNumber: '',
    introVideoLink: '',
    uploadingMap: {},   // { documentType: true/false }
    errorMap: {}        // { documentType: 'error message' }
  };

  // Single-upload types (identity_photo, intro_video) — one doc at most.
  self.getDocSingle = function (type) {
    return (self.tutor.documents || []).find(function (d) { return d.documentType === type; });
  };

  // Multi-upload types (o_level, a_level, degree, postgrad, nie_cert,
  // specialist_cert) — up to 3, sorted for stable display order.
  self.getDocs = function (type) {
    return (self.tutor.documents || [])
      .filter(function (d) { return d.documentType === type; })
      .sort(function (a, b) { return a.sortOrder - b.sortOrder; });
  };

  self.getDocCount = function (type) {
    return self.getDocs(type).length;
  };

  self.isDocTypeFull = function (type) {
    return self.getDocCount(type) >= 3;
  };

  self.getSpecialistCerts = function () {
    return self.getDocs('specialist_cert');
  };

  // Alias — kept for any template reference still using the old name.
  self.getDoc = self.getDocSingle;

  self.formatFileSize = function (bytes) {
    if (!bytes) return '';
    if (bytes >= 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return Math.round(bytes / 1024) + ' KB';
  };

  // Returns true if the tutor has at least one rejected document
  self.hasRejectedDoc = function () {
    return (self.tutor.documents || []).some(function (d) { return d.status === 'rejected'; });
  };

  // Returns the first rejected doc's adminNote (for the banner)
  self.rejectedDocNote = function () {
    var d = (self.tutor.documents || []).find(function (d) { return d.status === 'rejected'; });
    return d ? { type: d.documentType, note: d.adminNote } : null;
  };

  var ACADEMIC_LEVEL_TYPES = ['o_level', 'a_level', 'degree', 'postgrad'];

  // Static, not rebuilt per digest — ng-repeat over a fresh array literal in the
  // template tears down and rebuilds every row each cycle (same trap documented
  // on rebuildSetupClassUniqueSubjects/computeContactableParents above).
  self.academicLevels = [
    { type: 'o_level', label: 'O-Level / SPM' },
    { type: 'a_level', label: 'A-Level / STPM / Diploma' },
    { type: 'degree', label: "Bachelor's degree" },
    { type: 'postgrad', label: "Master's / PhD" }
  ];

  // Count of mandatory doc slots filled: identity photo + at least one academic level
  self.mandatoryUploadedCount = function () {
    var docs = self.tutor.documents || [];
    var count = 0;
    if (docs.some(function (d) { return d.documentType === 'identity_photo' && d.fileUrl; })) count++;
    if (docs.some(function (d) { return ACADEMIC_LEVEL_TYPES.indexOf(d.documentType) >= 0 && d.fileUrl; })) count++;
    return count;
  };

  self.mandatoryTotal = 2;

  self.verifProgress = function () {
    return Math.round((self.mandatoryUploadedCount() / self.mandatoryTotal) * 100) + '%';
  };

  self.canSubmitVerification = function () {
    return self.mandatoryUploadedCount() >= self.mandatoryTotal
      && self.tutor.verificationStatus !== 'pending';
  };

  // Refreshes vm.tutor after any verification mutation. Deliberately uses
  // getByUser (not getById/TutorService.getById) — GetById only returns tutors
  // that are IsVerified && IsOnline, so it 404s for exactly the tutors this
  // feature targets (mid-verification, not yet approved).
  self._refreshTutor = function () {
    return TutorService.getByUser(user.userId).then(function (res) {
      self.tutor = res.data;
      // Re-sync idType/idNumber from the saved identity doc, so the form still
      // reflects what's actually on file after a reload/refresh rather than
      // resetting to the hardcoded 'NRIC'/'' defaults.
      var idDoc = self.getDocSingle('identity_photo');
      if (idDoc && idDoc.idType) {
        self.verif.idType = idDoc.idType;
        self.verif.idNumber = idDoc.idNumber || '';
      }
    });
  };

  self.uploadVerifDoc = function (file, docType) {
    if (!file) return;
    self.verif.uploadingMap[docType] = true;
    self.verif.errorMap[docType] = null;

    TutorService.uploadDocument(file, docType).then(function (res) {
      return TutorService.saveDocument(self.tutor.id, {
        documentType: docType,
        fileUrl: res.data.url,
        fileName: res.data.fileName,
        fileSizeBytes: res.data.fileSizeBytes,
        idType: docType === 'identity_photo' ? self.verif.idType : null,
        idNumber: docType === 'identity_photo' ? self.verif.idNumber : null
      });
    }).then(function () {
      return self._refreshTutor();
    }).catch(function (err) {
      self.verif.errorMap[docType] = err.data && err.data.message
        ? err.data.message : 'Upload failed. Please try again.';
    }).finally(function () {
      self.verif.uploadingMap[docType] = false;
    });
  };

  // Replaces a rejected document: removes the old one first, then uploads and
  // saves the new file under the same type. Count stays the same (-1, +1) so
  // this never trips the 3-attachment cap on its own.
  self.reuploadVerifDoc = function (file, docType, docId) {
    if (!file || !docId) return;
    self.verif.uploadingMap[docType] = true;
    self.verif.errorMap[docType] = null;

    TutorService.removeDocument(self.tutor.id, parseInt(docId))
      .then(function () {
        return TutorService.uploadDocument(file, docType);
      })
      .then(function (res) {
        return TutorService.saveDocument(self.tutor.id, {
          documentType: docType,
          fileUrl: res.data.url,
          fileName: res.data.fileName,
          fileSizeBytes: res.data.fileSizeBytes
        });
      })
      .then(function () {
        return self._refreshTutor();
      })
      .catch(function (err) {
        self.verif.errorMap[docType] = err.data && err.data.message
          ? err.data.message : 'Re-upload failed. Please try again.';
      })
      .finally(function () {
        self.verif.uploadingMap[docType] = false;
      });
  };

  // Saves an intro video link (no file upload)
  self.saveIntroVideoLink = function () {
    if (!self.verif.introVideoLink) return;
    TutorService.saveDocument(self.tutor.id, {
      documentType: 'intro_video',
      externalUrl: self.verif.introVideoLink
    }).then(function () {
      self.verif.introVideoLink = '';
      return self._refreshTutor();
    }).catch(function () {
      self.verif.errorMap['intro_video'] = 'Failed to save link.';
    });
  };

  self.removeVerifDoc = function (docId) {
    TutorService.removeDocument(self.tutor.id, docId)
      .then(function () { return self._refreshTutor(); })
      .catch(function () { /* Non-fatal */ });
  };

  self.verifSubmitSuccess = false;
  self.verifSubmitError = '';

  self.submitVerification = function () {
    if (!self.canSubmitVerification()) return;
    self.verifSubmitError = '';
    TutorService.submitVerification(self.tutor.id).then(function () {
      return self._refreshTutor();
    }).then(function () {
      self.verifSubmitSuccess = true;
      $timeout(function () { self.verifSubmitSuccess = false; }, 3000);
    }).catch(function (err) {
      self.verifSubmitError = err.data && err.data.message ? err.data.message : 'Submission failed.';
    });
  };

  // Triggers a hidden file input from a custom "Browse" button
  self.triggerVerifUpload = function (inputId) {
    document.getElementById(inputId).click();
  };

  // Poll for booking updates (e.g. parent accepts counter proposal)
  var _pollInterval = $interval(function () {
    if (!self.rescheduleBooking && !self.counterBooking && !self.reportBooking && !self.editBooking) {
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
    }
  }, 15000);
  // $scope (not $rootScope) — $rootScope never actually fires '$destroy' on a
  // route change in a normal SPA lifecycle, only on full app teardown, so an
  // interval cancelled that way would never really stop; it'd just keep
  // multiplying (uncancelled) every time this controller is re-instantiated by
  // navigating between tutor pages. $scope IS destroyed on route change.
  $scope.$on('$destroy', function () { $interval.cancel(_pollInterval); });

  self.chatError = '';

  self.sendMessage = function () {
    if (!self.chatText.trim() || !self.tutor || !self.activeParentUserId) return;
    self.chatError = ProfanityFilterService.validate(self.chatText);
    if (self.chatError) return;
    ChatService.send({ tutorId: self.tutor.id, parentUserId: self.activeParentUserId, text: self.chatText })
      .then(function (res) {
        self.chatMessages.push(res.data);
        self.chatText = '';
        scrollChatToBottom();
      }, function (err) {
        self.chatError = (err.data && err.data.message) ? err.data.message : 'Failed to send. Please try again.';
      });
  };

  // Auto-refresh the open conversation so a parent's reply shows up without the
  // tutor needing to reload the page — see the matching comment in
  // parent.controller.js. Only appends messages not already present (by id).
  function mergeNewChatMessages(fetched) {
    var seenIds = {};
    self.chatMessages.forEach(function (m) { seenIds[m.id] = true; });
    var added = false;
    fetched.forEach(function (m) {
      if (!seenIds[m.id]) { self.chatMessages.push(m); added = true; }
    });
    if (added) scrollChatToBottom();
  }

  function scrollChatToBottom() {
    $timeout(function () {
      var el = document.getElementById('chatMessages');
      if (el) el.scrollTop = el.scrollHeight;
    });
  }

  var _chatPollInterval = $interval(function () {
    if (!self.tutor) return;
    self.loadUnreadCounts();
    if (!self.activeParentUserId) return;
    ChatService.getMessages(self.tutor.id, self.activeParentUserId).then(function (res) {
      mergeNewChatMessages(res.data);
      self.unreadCounts[self.activeParentUserId] = 0;
    });
  }, 4000);
  $scope.$on('$destroy', function () { $interval.cancel(_chatPollInterval); });
}]);
