'use strict';

angular.module('learnSphereApp')
.controller('ParentCtrl', ['$location', '$timeout', '$q', 'AuthService', 'TutorService',
  'StudentService', 'BookingService', 'InvoiceService', 'ChatService', 'AdminService', 'ScheduleService', 'PendingMatchService', 'SubjectCatalog', 'ParentProfileService',
function ($location, $timeout, $q, AuthService, TutorService, StudentService, BookingService, InvoiceService, ChatService, AdminService, ScheduleService, PendingMatchService, SubjectCatalog, ParentProfileService) {
  var self = this;
  var user = AuthService.getCurrentUser();
  self.user = user;
  self.subjectCatalog = SubjectCatalog;

  // Subject catalog + a leading "Other" entry, precomputed once (stable references,
  // safe for ng-options — see parseSubjectCombos note on the infinite-digest guard).
  var OTHER_SUBJECT_OPTION = { examType: 'Other', subject: 'Other', level: '', label: 'Other' };
  self.subjectCatalogWithOther = {
    Singapore: [OTHER_SUBJECT_OPTION].concat(SubjectCatalog.Singapore),
    Malaysia: [OTHER_SUBJECT_OPTION].concat(SubjectCatalog.Malaysia)
  };

  self.tutors = [];
  self.favoriteTutorIds = [];
  self.students = [];
  self.bookings = [];
  self.invoices = [];
  self.chatMessages = [];
  self.selectedTutor = null;

  // AI Speed Match
  self.aiMatchSelectedStudentId = null;
  self.selectAiMatchStudent = function (studentId) {
    self.aiMatchSelectedStudentId = self.aiMatchSelectedStudentId === studentId ? null : studentId;
  };
  self.aiMatchSubjectChoice = {}; // studentId -> chosen "level · subject" string
  self.aiMatchAppliedStudentId = null;
  self.aiMatchAppliedSubject = '';
  self.aiMatchResults = [];
  self.applyAiMatch = function (s) {
    self.aiMatchSelectedStudentId = s.id;
    self.aiMatchAppliedStudentId = s.id;
    self.aiMatchAppliedSubject = self.aiMatchSubjectChoice[s.id] || '';

    var combo = (s.subjectCombos || []).find(function (c) {
      return (c.country + ' · ' + c.level + ' · ' + c.subject) === self.aiMatchAppliedSubject;
    });
    var subjectName = combo ? combo.subject : '';
    self.aiMatchResults = self.tutors.filter(function (t) {
      return (t.offerings || []).some(function (o) { return o.subject === subjectName; }) ||
             (t.subjects || []).indexOf(subjectName) >= 0;
    });
  };

  // Personalize My Class
  self.personalize = {
    mode: '',
    qualification: '',
    country: '',
    selectedSubjectOption: null,
    subjectOther: '',
    price: null,
    description: ''
  };
  self.onPersonalizeCountryChange = function () {
    self.personalize.selectedSubjectOption = null;
  };

  self.personalizeTab = 'form';
  self.personalizeSaveSuccess = false;
  self.personalizeApplications = JSON.parse(localStorage.getItem('ls_personalize_apps') || '[]');

  self.savePersonalizeApplication = function () {
    var opt = self.personalize.selectedSubjectOption;
    var subjectLabel = opt ? (opt.subject === 'Other' ? ('Other: ' + (self.personalize.subjectOther || '')) : opt.label) : '';
    self.personalizeApplications.unshift({
      id: Date.now(),
      mode: self.personalize.mode,
      qualification: self.personalize.qualification,
      country: self.personalize.country,
      subject: subjectLabel,
      price: self.personalize.price,
      description: self.personalize.description,
      savedAt: new Date().toLocaleString()
    });
    localStorage.setItem('ls_personalize_apps', JSON.stringify(self.personalizeApplications));
    self.personalizeSaveSuccess = true;
    $timeout(function () { self.personalizeSaveSuccess = false; }, 2500);
  };

  self.removePersonalizeApplication = function (id) {
    self.personalizeApplications = self.personalizeApplications.filter(function (a) { return a.id !== id; });
    localStorage.setItem('ls_personalize_apps', JSON.stringify(self.personalizeApplications));
  };

  // Search / filter state
  self.searchQuery = '';
  self.selectedCountry = 'Singapore';
  self.selectedSubject = 'All';
  self.selectedMode = 'All';
  self.minRating = 0;
  self.minExperience = 0;
  self.filtersExpanded = true;

  self.selectSearchCountry = function (c) {
    self.selectedCountry = c;
    self.selectedSubject = 'All';
  };

  // Booking form
  self.bookingForm = {
    classesPerMonth: 1,
    sessions: [{ date: '', startTime: '04:00 PM', endTime: '05:00 PM' }],
    duration: 1,
    message: '',
    studentId: '',
    subject: ''
  };
  self.bookingSuccess = false;

  // New student form
  self.studentForm = {
    name: '',
    birthDate: '',
    school: '',
    learningGoal: '',
    photoUrl: ''
  };
  self.studentSubjects = []; // [{country, level, subject}]
  self.newStudentSubjectCombo = { country: '', selectedOption: null };
  self.studentSuccess = false;
  self.newlyAddedStudentId = null;

  // Edit student state
  self.editingStudent = null;
  self.editStudentForm = {};
  self.editStudentSubjects = []; // [{country, level, subject}]
  self.newEditStudentSubjectCombo = { country: '', selectedOption: null };

  // Parent profile panel
  self.parentProfile = null;
  self.editingParentProfile = false;
  self.parentProfileForm = {};
  self.parentProfileError = '';

  self.startEditParentProfile = function () {
    self.parentProfileForm = { name: self.parentProfile.name, email: self.parentProfile.email, password: '' };
    self.parentProfileError = '';
    self.editingParentProfile = true;
  };

  self.cancelEditParentProfile = function () {
    self.editingParentProfile = false;
    self.parentProfileForm = {};
    self.parentProfileError = '';
  };

  self.saveParentProfile = function () {
    var payload = { name: self.parentProfileForm.name, email: self.parentProfileForm.email };
    if (self.parentProfileForm.password) payload.password = self.parentProfileForm.password;

    ParentProfileService.update(self.parentProfile.id, payload).then(function (res) {
      self.parentProfile = res.data;
      self.editingParentProfile = false;
      self.parentProfileForm = {};
    }).catch(function (err) {
      self.parentProfileError = (err.data && err.data.message) || 'Could not update profile.';
    });
  };

  self.closeParentProfilePermanently = function () {
    if (!confirm('This will permanently close your account and cannot be undone. Continue?')) return;
    ParentProfileService.close(self.parentProfile.id).then(function () {
      AuthService.logout();
      $location.path('/login');
    });
  };

  // School dropdown
  self.schoolSearch = '';
  self.schoolDropdownOpen = false;
  self.institutions = [];
  self.schoolIsOther = false;
  self.schoolError = null;
  self.countryFilter = 'Singapore';

  // Issue report
  self.issueForm = { bookingId: null, issueType: 'Tutor was absent (No show)', details: '' };
  self.issueSuccess = false;

  // Chat
  self.chatText = '';
  self.activeTutorId = null;

  // Active day for calendar
  self.selectedCalDay = {};

  // Student schedule calendar
  var _scNow = new Date();
  var _scMonthNames = ['January','February','March','April','May','June',
                       'July','August','September','October','November','December'];
  self.studCal = { year: _scNow.getFullYear(), month: _scNow.getMonth(), selectedDay: 0, selectedStudentId: null };

  self.studCalMonthName = function () { return _scMonthNames[self.studCal.month]; };
  self.studCalPrevMonth = function () {
    if (self.studCal.month === 0) { self.studCal.month = 11; self.studCal.year--; }
    else { self.studCal.month--; }
    self.studCal.selectedDay = 0;
  };
  self.studCalNextMonth = function () {
    if (self.studCal.month === 11) { self.studCal.month = 0; self.studCal.year++; }
    else { self.studCal.month++; }
    self.studCal.selectedDay = 0;
  };
  self.studCalDaysArray = function () {
    var n = new Date(self.studCal.year, self.studCal.month + 1, 0).getDate();
    var a = []; for (var i = 0; i < n; i++) a.push(i + 1); return a;
  };
  self.studCalOffsetArray = function () {
    var d = new Date(self.studCal.year, self.studCal.month, 1).getDay();
    var off = d === 0 ? 6 : d - 1;
    var a = []; for (var i = 0; i < off; i++) a.push(i); return a;
  };
  self.studCalDayStr = function (day) {
    var m = self.studCal.month + 1;
    return self.studCal.year + '-' + (m < 10 ? '0' + m : m) + '-' + (day < 10 ? '0' + day : day);
  };
  self.getBookingsForStudentOnDay = function (studentId, day) {
    if (!studentId || !day) return [];
    var s = self.studCalDayStr(day);
    return self.bookings.filter(function (b) {
      return b.studentId === studentId && b.status === 'confirmed' &&
        (b.classes || []).some(function (c) { return c.date === s; });
    });
  };
  self.studCalClassTime = function (booking, day) {
    var s = self.studCalDayStr(day);
    var c = (booking.classes || []).find(function (c) { return c.date === s; });
    return c ? c.time : '';
  };
  self.toggleStudentCalendar = function (studentId) {
    self.studCal.selectedStudentId = self.studCal.selectedStudentId === studentId ? null : studentId;
    self.studCal.selectedDay = 0;
  };

  function isPastCalDay(year, month, day) {
    if (!day) return false;
    var d = new Date(year, month, day);
    d.setHours(0, 0, 0, 0);
    var today = new Date();
    today.setHours(0, 0, 0, 0);
    return d < today;
  }

  self.isStudCalPastDay = function (day) {
    return isPastCalDay(self.studCal.year, self.studCal.month, day);
  };

  // Tutor busy-times calendar (booking page) — dates/times only, no booking details,
  // so parents can avoid picking an overlapping slot.
  self.tutorCal = { year: _scNow.getFullYear(), month: _scNow.getMonth(), selectedDay: 0, busyTimes: [] };

  self.tutorCalMonthName = function () { return _scMonthNames[self.tutorCal.month]; };
  self.tutorCalPrevMonth = function () {
    if (self.tutorCal.month === 0) { self.tutorCal.month = 11; self.tutorCal.year--; }
    else { self.tutorCal.month--; }
    self.tutorCal.selectedDay = 0;
  };
  self.tutorCalNextMonth = function () {
    if (self.tutorCal.month === 11) { self.tutorCal.month = 0; self.tutorCal.year++; }
    else { self.tutorCal.month++; }
    self.tutorCal.selectedDay = 0;
  };
  self.tutorCalDaysArray = function () {
    var n = new Date(self.tutorCal.year, self.tutorCal.month + 1, 0).getDate();
    var a = []; for (var i = 0; i < n; i++) a.push(i + 1); return a;
  };
  self.tutorCalOffsetArray = function () {
    var d = new Date(self.tutorCal.year, self.tutorCal.month, 1).getDay();
    var off = d === 0 ? 6 : d - 1;
    var a = []; for (var i = 0; i < off; i++) a.push(i); return a;
  };
  self.tutorCalDayStr = function (day) {
    var m = self.tutorCal.month + 1;
    return self.tutorCal.year + '-' + (m < 10 ? '0' + m : m) + '-' + (day < 10 ? '0' + day : day);
  };
  self.getBusyTimesForDay = function (day) {
    if (!day) return [];
    var s = self.tutorCalDayStr(day);
    return self.tutorCal.busyTimes.filter(function (b) { return b.date === s; });
  };
  self.dayHasBusyTimes = function (day) {
    return self.getBusyTimesForDay(day).length > 0;
  };

  self.isTutorCalPastDay = function (day) {
    return isPastCalDay(self.tutorCal.year, self.tutorCal.month, day);
  };

  // Reschedule
  self.rescheduleBooking = null;
  self.rescheduleForm = { classes: [] };
  self.rescheduleSuccess = false;

  self.isBookingConflicted = function (booking) {
    if (!booking || booking.status !== 'confirmed') return false;
    return (booking.classes || []).some(function (c) {
      return ScheduleService.isBlocked(booking.tutorId, c.date);
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

  self.rescheduleEndTime = function (c) {
    return calcEndTimeFromDuration(c.proposedStartTime, self.rescheduleBooking && self.rescheduleBooking.durationHours);
  };

  self.onRescheduleStartTimeChange = function (c) {
    var end = self.rescheduleEndTime(c);
    c.time = end ? (c.proposedStartTime + ' - ' + end) : c.proposedStartTime;
  };

  self.startReschedule = function (booking) {
    self.rescheduleBooking = booking;
    self.rescheduleForm.classes = (booking.classes || []).map(function (c) {
      return { originalDate: c.date, originalTime: c.time, date: c.date, time: c.time, proposedStartTime: extractStartTime(c.time) };
    });
    self.rescheduleSuccess = false;
  };

  self.cancelReschedule = function () {
    self.rescheduleBooking = null;
    self.rescheduleForm = { classes: [] };
  };

  self.submitReschedule = function () {
    if (!self.rescheduleBooking) return;
    if (self.hasTooSoonReschedule()) return;
    if (self.hasDurationMismatchReschedule()) return;
    if (self.hasDuplicateReschedule()) return;
    var newClasses = self.rescheduleForm.classes.map(function (c) {
      return { date: toDateStr(toDateObj(c.date)), time: c.time };
    });
    self.rescheduleBooking.classes = newClasses;
    self.rescheduleSuccess = true;
    self.cancelReschedule();
    $timeout(function () {
      self.rescheduleSuccess = false;
    }, 2500);
  };

  self.isDateBlockedForTutor = function (tutorId, dateVal) {
    if (!tutorId || !dateVal) return false;
    var s = dateVal instanceof Date ? toDateStr(dateVal) : dateVal;
    return ScheduleService.isBlocked(tutorId, s);
  };

  self.isSessionDateBlocked = function (dateVal) {
    if (!dateVal || !self.selectedTutor) return false;
    return self.isDateBlockedForTutor(self.selectedTutor.id, dateVal);
  };

  self.hasBlockedSessionDate = function () {
    if (!self.selectedTutor || !self.bookingForm.sessions) return false;
    return self.bookingForm.sessions.some(function (s) {
      return s.date && self.isSessionDateBlocked(s.date);
    });
  };

  // Load data
  function init() {
    TutorService.getAll().then(function (res) {
      self.tutors = res.data;
      var pendingTutorId = PendingMatchService.consumeTutor();
      if (pendingTutorId) {
        var t = self.tutors.find(function (x) { return x.id === pendingTutorId; });
        if (t) self.selectTutor(t);
      }
    });
    StudentService.getMyStudents().then(function (res) {
      self.students = res.data;
      self.students.forEach(function (s) { s.subjectCombos = self.parseSubjectCombos(s.subjectSelect, s.educationLevel); });
      var firstActive = self.students.find(function (s) { return !s.isArchived; });
      if (firstActive) {
        self.bookingForm.studentId = firstActive.id;
      }
    });
    BookingService.getAll().then(function (res) { self.bookings = res.data; });
    InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
    ParentProfileService.getProfile().then(function (res) { self.parentProfile = res.data; });
    TutorService.getFavorites().then(function (res) { self.favoriteTutorIds = res.data; });
  }
  init();

  // Favorite tutors
  self.isFavorited = function (tutorId) {
    return self.favoriteTutorIds.indexOf(tutorId) >= 0;
  };

  self.toggleFavorite = function (tutor, $event) {
    if ($event) $event.stopPropagation();
    if (self.isFavorited(tutor.id)) {
      TutorService.removeFavorite(tutor.id).then(function () {
        self.favoriteTutorIds = self.favoriteTutorIds.filter(function (id) { return id !== tutor.id; });
      });
    } else {
      TutorService.addFavorite(tutor.id).then(function () {
        self.favoriteTutorIds.push(tutor.id);
      });
    }
  };

  // Filtered tutors
  self.filteredTutors = function () {
    return self.tutors.filter(function (t) {
      var q = self.searchQuery.toLowerCase();
      var matchQuery = !q || t.name.toLowerCase().indexOf(q) >= 0 ||
        t.subjects.some(function (s) { return s.toLowerCase().indexOf(q) >= 0; }) ||
        (t.qualifications || []).some(function (ql) { return ql.toLowerCase().indexOf(q) >= 0; });
      var matchCountry = (t.offerings || []).some(function (o) { return o.country === self.selectedCountry; });
      var matchSub = self.selectedSubject === 'All' || t.subjects.indexOf(self.selectedSubject) >= 0;
      var matchMode = self.selectedMode === 'All' || t.modes.indexOf(self.selectedMode) >= 0;
      var matchRating = !self.minRating || t.rating >= self.minRating;
      var matchExperience = !self.minExperience || t.experienceYears >= self.minExperience;
      return matchQuery && matchCountry && matchSub && matchMode && matchRating && matchExperience;
    }).sort(function (a, b) {
      return (self.isFavorited(b.id) ? 1 : 0) - (self.isFavorited(a.id) ? 1 : 0);
    });
  };

  // Select a tutor to book
  self.selectTutor = function (tutor) {
    self.selectedTutor = tutor;
    self.bookingForm.subject = tutor.subjects[0] || '';
    self.bookingForm.classesPerMonth = 1;
    self.bookingForm.sessions = [{ date: '', startTime: '04:00 PM', endTime: '05:00 PM', recurring: false }];
    if (self.students.length) self.bookingForm.studentId = self.students[0].id;

    self.tutorCal.year = _scNow.getFullYear();
    self.tutorCal.month = _scNow.getMonth();
    self.tutorCal.selectedDay = 0;
    self.tutorCal.busyTimes = [];
    TutorService.getBusyTimes(tutor.id).then(function (res) { self.tutorCal.busyTimes = res.data; });
  };

  // Jump from AI Speed Match results straight into the booking flow on the search page.
  // Route changes re-instantiate ParentCtrl, so the tutor id is handed off via PendingMatchService
  // and picked back up once the search page's tutor list has loaded (see init()).
  self.goToBookTutor = function (tutor) {
    PendingMatchService.setTutor(tutor.id);
    $location.path('/parent/search');
  };

  self.updateSessions = function () {
    var n = parseInt(self.bookingForm.classesPerMonth) || 1;
    while (self.bookingForm.sessions.length < n) {
      self.bookingForm.sessions.push({ date: '', startTime: '04:00 PM', endTime: '05:00 PM' });
    }
    self.bookingForm.sessions = self.bookingForm.sessions.slice(0, n);
    self.applyRecurring();
  };

  function parseAnyDate(val) {
    if (!val) return new Date(NaN);
    if (val instanceof Date) return new Date(val.getFullYear(), val.getMonth(), val.getDate());
    var s = String(val).trim();
    var dmyh = s.match(/^(\d{1,2})-(\d{1,2})-(\d{4})$/);
    if (dmyh) return new Date(+dmyh[3], +dmyh[2] - 1, +dmyh[1]);
    var dmy = s.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
    if (dmy) return new Date(+dmy[3], +dmy[2] - 1, +dmy[1]);
    var ymd = s.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (ymd) return new Date(+ymd[1], +ymd[2] - 1, +ymd[3]);
    return new Date(s + 'T00:00:00');
  }

  function toDateObj(val) {
    if (!val) return null;
    var d = parseAnyDate(val);
    return isNaN(d.getTime()) ? null : d;
  }

  function toDateStr(val) {
    if (!val) return '';
    var d = parseAnyDate(val);
    if (isNaN(d.getTime())) return '';
    var mm = (d.getMonth() + 1 < 10 ? '0' : '') + (d.getMonth() + 1);
    var dd = (d.getDate() < 10 ? '0' : '') + d.getDate();
    return d.getFullYear() + '-' + mm + '-' + dd;
  }

  // Minimum lead time before a class can be booked/changed, to avoid urgent last-minute changes.
  var MIN_LEAD_HOURS = 6;

  function combineDateTime(dateVal, timeStr) {
    var d = toDateObj(dateVal);
    if (!d) return null;
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

  self.hasTooSoonSession = function () {
    return (self.bookingForm.sessions || []).some(function (s) {
      return s.date && self.isTooSoon(s.date, s.startTime);
    });
  };

  self.hasTooSoonReschedule = function () {
    return (self.rescheduleForm.classes || []).some(function (c) {
      return c.date && self.isTooSoon(c.date, c.time);
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
      var d1 = toDateStr(list[i][dateKey]);
      for (var j = i + 1; j < list.length; j++) {
        if (toDateStr(list[j][dateKey]) !== d1) continue;
        var r2 = parseTimeRangeMinutes(list[j][timeKey]);
        if (!r2) continue;
        if (r1.start < r2.end && r2.start < r1.end) return true;
      }
    }
    return false;
  }

  self.hasDuplicateSessions = function () {
    return hasOverlappingDateTimes((self.bookingForm.sessions || []).map(function (s) {
      return { date: s.date, time: s.startTime + ' - ' + s.endTime };
    }), 'date', 'time');
  };

  self.hasDuplicateReschedule = function () {
    return hasOverlappingDateTimes(self.rescheduleForm.classes || [], 'date', 'time');
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

  self.hasDurationMismatchReschedule = function () {
    if (!self.rescheduleBooking) return false;
    return (self.rescheduleForm.classes || []).some(function (c) {
      return self.isDurationMismatch(c.time, self.rescheduleBooking.durationHours);
    });
  };

  self.applyRecurring = function () {
    var sessions = self.bookingForm.sessions;
    if (!sessions || sessions.length < 2 || !sessions[0].recurring || !sessions[0].date) return;
    var base = toDateObj(sessions[0].date);
    if (!base) return;
    for (var i = 1; i < sessions.length; i++) {
      var d = new Date(base.getFullYear(), base.getMonth(), base.getDate() + i * 7);
      var dday = (d.getDate() < 10 ? '0' : '') + d.getDate();
      var dmon = (d.getMonth() + 1 < 10 ? '0' : '') + (d.getMonth() + 1);
      sessions[i].date = dday + '-' + dmon + '-' + d.getFullYear();
    }
  };

  self.onDateChange = function (idx) {
    if (idx === 0) self.applyRecurring();
  };

  self.calcEndTime = function (session) {
    if (!session.startTime) return;
    var match = session.startTime.match(/^(\d{1,2}):(\d{2})\s*(AM|PM)$/i);
    if (!match) return;
    var h = parseInt(match[1]);
    var m = parseInt(match[2]);
    var ampm = match[3].toUpperCase();
    if (ampm === 'PM' && h !== 12) h += 12;
    if (ampm === 'AM' && h === 12) h = 0;
    h += 1;
    var endAmpm = (h >= 12 && h < 24) ? 'PM' : 'AM';
    if (h >= 24) h -= 24;
    if (h === 0) h = 12;
    else if (h > 12) h -= 12;
    session.endTime = h + ':' + (m < 10 ? '0' + m : m) + ' ' + endAmpm;
  };

  self.clearSelectedTutor = function () { self.selectedTutor = null; };

  // Book a tutor
  self.submitBooking = function () {
    if (!self.selectedTutor) return;
    if (self.hasTooSoonSession()) return;
    if (self.hasDuplicateSessions()) return;
    var student = self.students.find(function (s) { return s.id === self.bookingForm.studentId; });
    var classes = self.bookingForm.sessions.map(function (session) {
      return { date: toDateStr(session.date), time: session.startTime + ' - ' + session.endTime };
    });
    BookingService.create({
      tutorId: self.selectedTutor.id,
      studentId: self.bookingForm.studentId,
      subject: self.bookingForm.subject + ' - ' + (student ? student.educationLevel : ''),
      mode: self.selectedTutor.modes[0],
      classes: classes,
      durationHours: self.bookingForm.duration,
      message: self.bookingForm.message,
      totalPrice: self.selectedTutor.pricePerSession * parseInt(self.bookingForm.classesPerMonth)
    }).then(function (res) {
      self.bookings.unshift(res.data);
      self.bookingSuccess = true;
      $timeout(function () {
        self.bookingSuccess = false;
        self.selectedTutor = null;
        $location.path('/parent/sessions');
      }, 2000);
    }).catch(function (err) {
      alert((err.data && err.data.message) || 'Could not create this booking. Please try again.');
    });
  };

  // Subject combinations (Country + Level + Subject per entry, mirrors the Tutor Offering builder)
  self.parseSubjectCombos = function (subjectSelect, fallbackLevel) {
    if (!subjectSelect) return [];
    return subjectSelect.split(',').map(function (x) { return x.trim(); }).filter(function (x) { return x; })
      .map(function (token) {
        var parts = token.split('·').map(function (p) { return p.trim(); });
        if (parts.length >= 3) return { country: parts[0], level: parts[1], subject: parts.slice(2).join('·').trim() };
        if (parts.length === 2) return { country: 'Singapore', level: parts[0], subject: parts[1] };
        return { country: 'Singapore', level: fallbackLevel || '', subject: token };
      });
  };

  self.formatSubjectCombos = function (combos) {
    return combos.map(function (c) { return c.country + ' · ' + c.level + ' · ' + c.subject; }).join(', ');
  };

  // Edit student
  self.startEditStudent = function (s) {
    self.editingStudent = s;
    self.editStudentForm = { name: s.name, school: s.school, learningGoal: s.learningGoal || '' };
    self.editStudentSubjects = self.parseSubjectCombos(s.subjectSelect, s.educationLevel);
    self.newEditStudentSubjectCombo = { country: '', selectedOption: null };
  };

  self.cancelEditStudent = function () {
    self.editingStudent = null;
    self.editStudentForm = {};
    self.editStudentSubjects = [];
    self.newEditStudentSubjectCombo = { country: '', selectedOption: null };
  };

  self.addEditStudentSubject = function () {
    var c = self.newEditStudentSubjectCombo;
    var opt = c.selectedOption;
    if (c.country && opt && !self.editStudentSubjects.some(function (x) { return x.country === c.country && x.level === opt.level && x.subject === opt.subject; })) {
      self.editStudentSubjects.push({ country: c.country, level: opt.level, subject: opt.subject });
    }
    self.newEditStudentSubjectCombo = { country: c.country, selectedOption: null };
  };

  self.removeEditStudentSubject = function (i) { self.editStudentSubjects.splice(i, 1); };

  self.saveEditStudent = function () {
    if (!self.editingStudent || !self.editStudentForm.name.trim()) return;
    var payload = {
      name: self.editStudentForm.name,
      school: self.editStudentForm.school || '',
      educationLevel: self.editStudentSubjects.length ? self.editStudentSubjects[0].level : (self.editingStudent.educationLevel || ''),
      subjectSelect: self.formatSubjectCombos(self.editStudentSubjects),
      learningGoal: self.editStudentForm.learningGoal || null
    };
    StudentService.update(self.editingStudent.id, payload).then(function (res) {
      res.data.subjectCombos = self.parseSubjectCombos(res.data.subjectSelect, res.data.educationLevel);
      var idx = self.students.findIndex(function (s) { return s.id === self.editingStudent.id; });
      if (idx >= 0) self.students[idx] = res.data;
      self.cancelEditStudent();
    });
  };

  self.deleteBlockedStudentId = null;
  self.deleteBlockedInfo = null;

  self.studentsTab = 'active';

  self.activeStudents = function () {
    return self.students.filter(function (s) { return !s.isArchived; });
  };

  self.archivedStudents = function () {
    return self.students.filter(function (s) { return s.isArchived; });
  };

  self.visibleStudents = function () {
    return self.studentsTab === 'archived' ? self.archivedStudents() : self.activeStudents();
  };

  self.deleteStudent = function (s) {
    if (!confirm('Delete ' + s.name + '\'s profile? This permanently erases all their past session and billing history too. This cannot be undone — archive instead if you want to keep those records.')) return;
    self.deleteBlockedStudentId = null;
    self.deleteBlockedInfo = null;
    StudentService.delete(s.id).then(function () {
      self.students = self.students.filter(function (x) { return x.id !== s.id; });
      if (self.editingStudent && self.editingStudent.id === s.id) self.cancelEditStudent();
    }).catch(function (err) {
      if (err.status === 400 && err.data) {
        self.deleteBlockedStudentId = s.id;
        self.deleteBlockedInfo = err.data;
      } else {
        alert('Could not delete this profile. Please try again.');
      }
    });
  };

  self.archiveStudent = function (s) {
    if (!confirm('Archive ' + s.name + '\'s profile? It will be hidden from your active roster but its history is kept, and you can restore it anytime.')) return;
    StudentService.archive(s.id).then(function (res) {
      var idx = self.students.findIndex(function (x) { return x.id === s.id; });
      if (idx >= 0) self.students[idx] = res.data;
      self.deleteBlockedStudentId = null;
      self.deleteBlockedInfo = null;
    }).catch(function (err) {
      alert((err.data && err.data.message) || 'Could not archive this profile. Please try again.');
    });
  };

  self.unarchiveStudent = function (s) {
    StudentService.unarchive(s.id).then(function (res) {
      var idx = self.students.findIndex(function (x) { return x.id === s.id; });
      if (idx >= 0) self.students[idx] = res.data;
    }).catch(function () {
      alert('Could not restore this profile. Please try again.');
    });
  };

  self.addStudentSubject = function () {
    var c = self.newStudentSubjectCombo;
    var opt = c.selectedOption;
    if (c.country && opt && !self.studentSubjects.some(function (x) { return x.country === c.country && x.level === opt.level && x.subject === opt.subject; })) {
      self.studentSubjects.push({ country: c.country, level: opt.level, subject: opt.subject });
    }
    self.newStudentSubjectCombo = { country: c.country, selectedOption: null };
  };

  self.removeStudentSubject = function (i) { self.studentSubjects.splice(i, 1); };

  // Add student
  self.createStudent = function () {
    if (!self.studentForm.name.trim()) return;

    var save = function (schoolName) {
      var payload = {
        name: self.studentForm.name,
        birthDate: self.studentForm.birthDate || null,
        school: schoolName,
        educationLevel: self.studentSubjects.length ? self.studentSubjects[0].level : '',
        subjectSelect: self.formatSubjectCombos(self.studentSubjects),
        learningGoal: self.studentForm.learningGoal,
        photoUrl: self.studentForm.photoUrl || null
      };
      StudentService.create(payload).then(function (res) {
        res.data.subjectCombos = self.parseSubjectCombos(res.data.subjectSelect, res.data.educationLevel);
        self.students.push(res.data);
        self.newlyAddedStudentId = res.data.id;
        self.studentSuccess = true;
        self.studentForm = { name: '', birthDate: '', school: '', learningGoal: '', photoUrl: '' };
        self.studentSubjects = [];
        self.newStudentSubjectCombo = { country: '', selectedOption: null };
        self.schoolSearch = '';
        self.institutions = [];
        self.schoolDropdownOpen = false;
        self.schoolIsOther = false;
        self.schoolError = null;
        $timeout(function () { self.studentSuccess = false; }, 5000);
      });
    };

    if (self.schoolIsOther) {
      var otherName = (self.studentForm.school || '').trim();
      if (!otherName) {
        self.schoolError = 'Please enter your institution\'s name.';
        return;
      }
      self.schoolError = null;
      save(otherName);
      return;
    }

    var enteredName = (self.schoolSearch || '').trim();
    if (!enteredName) {
      self.schoolError = 'Please select an education institution, or choose "Others" if it isn\'t listed.';
      return;
    }

    AdminService.getInstitutions({ search: enteredName }).then(function (res) {
      var match = (res.data || []).find(function (inst) {
        return inst.name.toLowerCase() === enteredName.toLowerCase();
      });
      if (!match) {
        self.schoolError = 'We couldn\'t match "' + enteredName + '" to a listed institution. Please pick one from the dropdown, or choose "Others" if it isn\'t listed.';
        return;
      }
      self.schoolError = null;
      save(match.name);
    });
  };

  // Pay an invoice
  self.paySuccess = false;

  self.invoicesTab = 'active';

  self.nonCancelledInvoices = function () {
    return self.invoices.filter(function (inv) { return inv.status !== 'Cancelled'; });
  };

  self.cancelledInvoices = function () {
    return self.invoices.filter(function (inv) { return inv.status === 'Cancelled'; });
  };

  self.visibleInvoices = function () {
    return self.invoicesTab === 'cancelled' ? self.cancelledInvoices() : self.nonCancelledInvoices();
  };

  self.payInvoice = function (invoiceId) {
    InvoiceService.pay(invoiceId).then(function () {
      self.paySuccess = true;
      InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
      $timeout(function () {
        self.paySuccess = false;
      }, 2500);
    }).catch(function (err) {
      alert((err.data && err.data.message) || 'Could not process this payment. Please try again.');
      InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
    });
  };

  // Report an issue
  self.startReportIssue = function (bookingId) {
    self.issueForm.bookingId = bookingId;
    self.issueForm.details = '';
    self.issueSuccess = false;
  };

  self.submitIssue = function () {
    BookingService.reportIssue(self.issueForm.bookingId, {
      issueType: self.issueForm.issueType,
      details: self.issueForm.details
    }).then(function () {
      self.issueSuccess = true;
      self.issueForm.bookingId = null;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.issueSuccess = false;
      }, 3000);
    });
  };

  // Chat
  self.loadChat = function (tutorId) {
    self.activeTutorId = tutorId;
    ChatService.getMessages(tutorId).then(function (res) { self.chatMessages = res.data; });
  };

  self.sendMessage = function () {
    if (!self.chatText.trim() || !self.activeTutorId) return;
    ChatService.send({ tutorId: self.activeTutorId, sender: 'parent', text: self.chatText })
      .then(function (res) {
        self.chatMessages.push(res.data);
        self.chatText = '';
      });
  };

  // School dropdown
  self.openSchoolDropdown = function () {
    self.schoolDropdownOpen = true;
    AdminService.getInstitutions({
      search: self.schoolSearch,
      country: self.countryFilter
    }).then(function (res) { self.institutions = res.data; });
  };

  self.searchSchools = function () {
    AdminService.getInstitutions({
      search: self.schoolSearch,
      country: self.countryFilter
    }).then(function (res) { self.institutions = res.data; });
  };

  self.selectSchool = function (name) {
    self.studentForm.school = name;
    self.schoolSearch = name;
    self.schoolDropdownOpen = false;
    self.schoolError = null;
  };

  self.selectSchoolOther = function () {
    self.schoolIsOther = true;
    self.studentForm.school = '';
    self.schoolDropdownOpen = false;
    self.schoolError = null;
  };

  self.backToSchoolSearch = function () {
    self.schoolIsOther = false;
    self.studentForm.school = '';
    self.schoolSearch = '';
    self.schoolError = null;
  };

  self.closeSchoolDropdown = function () {
    $timeout(function () { self.schoolDropdownOpen = false; }, 200);
  };

  // Calendar helpers
  self.getBookingsForDay = function (studentId, dayStr) {
    return self.bookings.filter(function (b) {
      return b.studentId === studentId &&
        b.classes && b.classes.some(function (c) { return c.date === dayStr; });
    });
  };

  self.getInvoiceForBooking = function (bookingId) {
    return self.invoices.find(function (i) { return i.bookingId === bookingId; });
  };

  self.isPaidBooking = function (bookingId) {
    var inv = self.getInvoiceForBooking(bookingId);
    return inv && inv.status === 'Paid';
  };

  self.pendingPayments = function () {
    return self.invoices.filter(function (inv) {
      if (inv.status !== 'Unpaid') return false;
      return self.bookings.some(function (b) { return b.id === inv.bookingId && b.status === 'confirmed'; });
    });
  };

  self.confirmedAndCounteredBookings = function () {
    return self.bookings.filter(function (b) { return b.status === 'confirmed' || b.status === 'countered'; });
  };

  self.counterAcceptSuccess = false;

  self.sessionsTab = 'active';

  self.nonCancelledBookings = function () {
    return self.bookings.filter(function (b) { return b.status !== 'cancelled'; });
  };

  self.cancelledBookings = function () {
    return self.bookings.filter(function (b) { return b.status === 'cancelled'; });
  };

  self.visibleBookings = function () {
    return self.sessionsTab === 'cancelled' ? self.cancelledBookings() : self.nonCancelledBookings();
  };

  self.cancelSuccess = false;

  // A paid invoice is a permanent payment record — a booking that's already been paid for can't be cancelled.
  self.bookingHasPaidInvoice = function (bookingId) {
    return self.invoices.some(function (inv) {
      return inv.bookingId === bookingId && inv.status === 'Paid';
    });
  };

  self.cancelBooking = function (booking) {
    if (self.bookingHasPaidInvoice(booking.id)) return;
    var msg = booking.status === 'pending'
      ? 'Cancel this booking request? The tutor hasn\'t responded yet, so no notification will be sent.'
      : 'Cancel this booking? The tutor will be notified since they\'ve already responded to it.';
    if (!confirm(msg)) return;

    BookingService.cancel(booking.id).then(function () {
      self.cancelSuccess = true;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
      $timeout(function () {
        self.cancelSuccess = false;
      }, 3000);
    }).catch(function (err) {
      alert((err.data && err.data.message) || 'Could not cancel this booking. Please try again.');
    });
  };

  self.acceptCounterProposal = function (booking) {
    BookingService.updateStatus(booking.id, 'confirmed', null)
      .then(function () {
        self.counterAcceptSuccess = true;
        BookingService.getAll().then(function (res) { self.bookings = res.data; });
        InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
        $timeout(function () {
          self.counterAcceptSuccess = false;
        }, 2500);
      });
  };

  // Load chat messages on chat page
  if ($location.path() === '/parent/chat') {
    TutorService.getAll().then(function (res) {
      if (res.data.length) self.loadChat(res.data[0].id);
    });
  }
}]);
