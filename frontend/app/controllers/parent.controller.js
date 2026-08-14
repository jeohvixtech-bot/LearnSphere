'use strict';

angular.module('learnSphereApp')
.controller('ParentCtrl', ['$scope', '$location', '$timeout', '$interval', '$q', 'AuthService', 'TutorService',
  'StudentService', 'BookingService', 'InvoiceService', 'ChatService', 'AdminService', 'ScheduleService', 'PendingMatchService', 'SubjectCatalog', 'ParentProfileService', 'TeachingModesCatalog', 'PresetCancellationService', 'NameValidationService', 'ProfanityFilterService',
function ($scope, $location, $timeout, $interval, $q, AuthService, TutorService, StudentService, BookingService, InvoiceService, ChatService, AdminService, ScheduleService, PendingMatchService, SubjectCatalog, ParentProfileService, TeachingModesCatalog, PresetCancellationService, NameValidationService, ProfanityFilterService) {
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
  self.pinnedTutorId = null;

  // AI Speed Match
  self.aiMatchSelectedStudentId = null;
  self.selectAiMatchStudent = function (studentId) {
    self.aiMatchSelectedStudentId = self.aiMatchSelectedStudentId === studentId ? null : studentId;
  };
  self.aiMatchSubjectChoice = {}; // studentId -> chosen "level · subject" string
  self.aiMatchAppliedStudentId = null;
  self.aiMatchAppliedSubject = '';
  self.aiMatchResults = [];
  self.aiMatchScoresLoading = false;
  self.applyAiMatch = function (s) {
    self.aiMatchSelectedStudentId = s.id;
    self.aiMatchAppliedStudentId = s.id;
    self.aiMatchAppliedSubject = self.aiMatchSubjectChoice[s.id] || '';

    var combo = (s.subjectCombos || []).find(function (c) {
      return (c.country + ' · ' + c.level + ' · ' + c.subject) === self.aiMatchAppliedSubject;
    });
    // Filters, all of which must pass:
    //  - Subject + level + country together (same fix as filteredTutors()'s
    //    matchSub) — a bare subject-name check would pass a tutor teaching the
    //    same subject at a completely different level than the child needs.
    //  - Teaching mode — the child's saved preferredModes must overlap with the
    //    tutor's offered modes; a strict online-only/home-visit-only mismatch
    //    isn't something ranking can fix, so it's excluded outright rather than
    //    just ranked lower. No preferredModes saved = no mode filter applied.
    //  - Availability (tutor currently accepting bookings) is already covered
    //    upstream — self.tutors only ever contains IsVerified && IsOnline
    //    tutors (see TutorsController.GetAll), so nothing further is needed here.
    var preferredModes = s.preferredModes || [];
    var matched = combo ? self.tutors.filter(function (t) {
      var subjectMatch = (t.offerings || []).some(function (o) {
        return o.subject === combo.subject && o.level === combo.level && o.country === combo.country;
      });
      var modeMatch = !preferredModes.length || (t.modes || []).some(function (m) {
        return preferredModes.indexOf(m) >= 0;
      });
      // Same rule as the catalog's filteredTutors() — booking now only happens
      // from a tutor's published preset classes, so a tutor with none isn't a
      // useful match result right now.
      var hasPresetClasses = self.tutorHasAnySlots(t);
      return subjectMatch && modeMatch && hasPresetClasses;
    }) : [];
    self.aiMatchResults = matched;
    if (!matched.length) return;

    // Rank by AI Speed Match score (admin-configured weightage × tutor's live
    // rating/experience/this-month activeness/disputes — see TutorsController.
    // GetMatchScores), highest first; price breaks ties among equal scores
    // (cheaper tutor ranks higher), never overriding a better score outright.
    self.aiMatchScoresLoading = true;
    TutorService.getMatchScores().then(function (res) {
      var scoreByTutor = {};
      res.data.forEach(function (m) { scoreByTutor[m.tutorId] = m; });
      matched.forEach(function (t) { t.matchScore = scoreByTutor[t.id] || null; });
      self.aiMatchResults = matched.slice().sort(function (a, b) {
        var sa = a.matchScore ? a.matchScore.score : -Infinity;
        var sb = b.matchScore ? b.matchScore.score : -Infinity;
        if (sb !== sa) return sb - sa;
        return a.pricePerSession - b.pricePerSession;
      });
      self.aiMatchScoresLoading = false;
    }).catch(function () { self.aiMatchScoresLoading = false; });
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
  // null = "All Subjects"; otherwise the whole {examType, subject, level, label} catalog
  // entry — filtering needs the level too, not just the subject name (see filteredTutors).
  self.selectedSubject = null;
  self.selectedMode = 'All';
  self.minRating = 0;

  // Flow B — booking a tutor's already-published class (picked via a catalog
  // card's "View & Book" row button, see viewAndBookPreset/selectTutor below)
  // confirms immediately, no per-request tutor approval. State for that flow's
  // invoice summary + receipt (see confirmPresetGroupBooking below).
  self.selectedPresetGroup = null;
  self.presetBookingBusy = false;
  self.presetBookingError = '';
  self.presetBookingSuccess = false;
  self.presetBookingReceipt = null;

  // Books every occurrence in the selected preset class group as ONE booking
  // (BookPreset takes an array of slot ids and creates a single Booking with one
  // class per occurrence — same as how a parent-offer booking already covers
  // multiple sessions) so it shows up as one entry in Sessions & Activity, not
  // one per occurrence.
  self.confirmPresetGroupBooking = function () {
    var group = self.selectedPresetGroup;
    if (!group || !self.bookingForm.studentId || self.presetBookingBusy) return;

    self.presetBookingBusy = true;
    self.presetBookingError = '';

    BookingService.bookPreset({
      presetSlotIds: group.slots.map(function (s) { return s.id; }),
      studentId: self.bookingForm.studentId
    }).then(function (res) {
      var createdBooking = res.data;
      // The button says "Confirm Payment" — actually pay the invoice immediately
      // here rather than just creating it Unpaid, unlike the tutor-approval
      // request flow (submitBooking) which genuinely can't charge until the
      // tutor accepts. Otherwise "Pay Invoice" would still show as outstanding
      // in Sessions & Activity right after a parent thinks they've just paid.
      return InvoiceService.getAll().then(function (r) {
        self.invoices = r.data;
        var inv = self.invoices.find(function (i) { return i.bookingId === createdBooking.id; });
        return inv ? InvoiceService.pay(inv.id) : $q.when();
      }).then(function () {
        return InvoiceService.getAll();
      }).then(function (r2) {
        self.invoices = r2.data;
        return createdBooking;
      });
    }).then(function (createdBooking) {
      self.presetBookingBusy = false;
      self.presetBookingSuccess = true;
      var student = self.students.find(function (x) { return x.id === self.bookingForm.studentId; });
      self.presetBookingReceipt = {
        tutorName: self.selectedTutor.name,
        studentName: student ? student.name : '',
        subject: group.subject,
        level: group.level,
        mode: group.mode,
        pricePerLesson: group.pricePerLesson,
        total: createdBooking.totalPrice,
        booking: createdBooking
      };
      BookingService.getAll().then(function (r) { self.bookings = r.data; });
      // Refresh the catalog so the booked occurrences drop out of / update in
      // everyone's "Available classes" chips (fill count, isFull, etc).
      TutorService.getAll({ includePresetSlots: true }).then(function (res2) {
        self.tutors = res2.data;
        self.tutors.forEach(function (t) { t._presetSummary = computeTutorPresetSummary(t); });
      });
      $timeout(function () {
        self.presetBookingSuccess = false;
        self.presetBookingReceipt = null;
        self.selectedTutor = null;
        self.selectedPresetGroup = null;
        $location.path('/parent/sessions');
      }, 3500);
    }).catch(function (err) {
      self.presetBookingBusy = false;
      self.presetBookingError = (err.data && err.data.message) || 'Booking failed. Please try again.';
    });
  };
  self.minExperience = 0;

  self.selectSearchCountry = function (c) {
    self.selectedCountry = c;
    self.selectedSubject = null;
  };

  // Finds the catalog entry matching a given subject+level (used when a tutor card's
  // subject chip is clicked, so the filter dropdown highlights the same entry it just
  // applied — ng-options matches by object identity, so a freshly-built object with the
  // same fields wouldn't show as selected even though it'd filter correctly either way).
  self.findSubjectCatalogEntry = function (country, subject, level) {
    return (self.subjectCatalog[country] || []).find(function (opt) {
      return opt.subject === subject && opt.level === level;
    }) || { examType: '', subject: subject, level: level, label: subject + ' (' + level + ')' };
  };

  var COUNTRY_ABBREV = { Singapore: 'SG', Malaysia: 'MY' };
  var SUBJECT_ABBREV = {
    'Mathematics': 'Maths',
    'Additional Mathematics': 'A. Maths',
    'English Language': 'English',
    'Mother Tongue (Chinese)': 'Chinese',
    'Mother Tongue (Malay)': 'Malay',
    'Mother Tongue (Tamil)': 'Tamil',
    'Combined Science': 'Comb. Science',
    'Literature in English': 'Literature',
    'Principles of Accounts': 'POA',
    'Social Studies': 'Soc. Studies',
    'Design & Technology': 'D&T',
    'Food & Nutrition': 'F&N',
    'General Paper': 'GP',
    'Bahasa Malaysia': 'BM',
    'Business Studies': 'Biz Studies',
    'Information Technology': 'IT'
  };

  // Card-tag abbreviation for the compact "SG · P6 · Maths" chip format — the
  // detailed tooltip/booking view keeps full text (e.g. "Mathematics · Primary 6"),
  // this is only for the dense catalog-card tags where space is tight.
  function abbreviateLevel(level) {
    if (!level) return '';
    var m = level.match(/^Primary\s+(\d+)$/i);
    if (m) return 'P' + m[1];
    m = level.match(/^Secondary\s+(\d+)$/i);
    if (m) return 'Sec' + m[1];
    if (/PSLE/i.test(level)) return 'PSLE';
    if (/SPM/i.test(level)) return 'SPM';
    if (/UPSR/i.test(level)) return 'UPSR';
    if (/PT3/i.test(level)) return 'PT3';
    if (/STPM/i.test(level)) return 'STPM';
    return level;
  }

  self.abbreviateSubject = function (subject) {
    return SUBJECT_ABBREV[subject] || subject;
  };

  self.abbreviateOffering = function (o) {
    if (!o) return '';
    var country = COUNTRY_ABBREV[o.country] || o.country;
    var level = abbreviateLevel(o.level);
    return country + ' · ' + level + ' · ' + self.abbreviateSubject(o.subject);
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

  // Preferred teaching-modes dual-pool for the "Add New Child" form — the student
  // doesn't exist yet, so this is saved via a follow-up PATCH right after creation.
  self.newStudentPreferredLeft = TeachingModesCatalog.slice();
  self.newStudentPreferredRight = [];
  self.newStudentPreferredModesError = '';

  // Edit student state
  self.editingStudent = null;
  self.editStudentForm = {};
  self.editStudentSubjects = []; // [{country, level, subject}]

  // Preferred teaching-modes dual-pool selector (drag-and-drop, right pool is ordered).
  // Rebuilt into plain arrays only on edit-open/change, never inline in the template.
  self.preferredLeft = [];
  self.preferredRight = [];
  self.preferredModesError = '';
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
      $location.path('/welcome');
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
    c.proposedStartTime = normalizeTimeToAmPm(c.proposedStartTime);
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
    if (self.hasInvalidReschedule()) return;
    if (self.hasTooSoonReschedule()) return;
    if (self.hasDurationMismatchReschedule()) return;
    if (self.hasDuplicateReschedule()) return;
    var bookingId = self.rescheduleBooking.id;
    BookingService.updateStatus(bookingId, 'countered', {
      message: 'Parent proposed reschedule',
      classes: self.rescheduleForm.classes.map(function (c) {
        return {
          originalDate: c.originalDate,
          originalTime: c.originalTime,
          proposedDate: toDateStr(toDateObj(c.date)),
          proposedTime: c.time
        };
      })
    }).then(function () {
      self.rescheduleSuccess = true;
      self.cancelReschedule();
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.rescheduleSuccess = false;
      }, 2500);
    });
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
    // Consumed synchronously up front (PendingMatchService is a plain in-memory
    // store, no async needed) so both loads below — which run in parallel with no
    // guaranteed order — can agree on the same values regardless of which
    // resolves first, rather than racing to set self.bookingForm.studentId.
    var pendingTutorId = PendingMatchService.consumeTutor();
    var pendingPresetGroupId = PendingMatchService.consumePresetGroupId();
    var pendingStudentId = PendingMatchService.consumeStudentId();

    TutorService.getAll({ includePresetSlots: true }).then(function (res) {
      self.tutors = res.data;
      // Preset slot strip on each tutor card (search page) reads straight off
      // each tutor's own t.timetable (see computeTutorPresetSummary below) —
      // that's already included in this response, no per-tutor round trip needed.
      // Computed once here (not from the template) to avoid $rootScope:infdig.
      self.tutors.forEach(function (t) { t._presetSummary = computeTutorPresetSummary(t); });

      // A pending tutor WITH a preset group is the AI Speed Match "View & Book"
      // hand-off — jump straight to that class's booking summary, same as
      // clicking the chip directly would. Without a group (e.g. a signed-out
      // visitor clicking a tutor card on the welcome page — see
      // WelcomeCtrl.goToLogin), there's no specific class to jump to yet, so
      // don't guess: just pin the card below instead of auto-opening anything.
      if (pendingTutorId && pendingPresetGroupId) {
        var t = self.tutors.find(function (x) { return x.id === pendingTutorId; });
        if (t) {
          // Carry the chip selection from AI Speed Match over onto this
          // freshly-loaded tutor object — selectTutor() below reads it straight
          // off the tutor, same as a chip clicked directly on this page would.
          t._selectedPresetGroupId = pendingPresetGroupId;
          self.selectTutor(t, pendingStudentId);
        }
      }

      // Pin — deliberately separate from the one-shot consume above.
      // PendingMatchService.getTutor() doesn't clear its value on read, so this
      // re-derives correctly every time ParentCtrl is re-instantiated (every
      // /parent/* route change creates a fresh one), letting the pin survive
      // navigating away and back until the user actually logs out.
      var pinnedId = PendingMatchService.getTutor ? PendingMatchService.getTutor() : null;
      if (pinnedId) {
        var pinnedTutor = self.tutors.find(function (x) { return String(x.id) === String(pinnedId); });
        if (pinnedTutor) {
          // Auto-switch country filter to match the tutor's country so their
          // pinned card is actually visible under the current filters.
          if (pinnedTutor.country) {
            self.selectedCountry = pinnedTutor.country;
          } else if (pinnedTutor.offerings && pinnedTutor.offerings.length) {
            self.selectedCountry = pinnedTutor.offerings[0].country || self.selectedCountry;
          }
          self.pinnedTutorId = pinnedId;
        } else {
          self.pinnedTutorId = null;
        }
      }
    });
    StudentService.getMyStudents().then(function (res) {
      self.students = res.data;
      self.students.forEach(function (s) { s.subjectCombos = self.parseSubjectCombos(s.subjectSelect, s.educationLevel); });
      // Computes the exact same target selectTutor() above would, so it doesn't
      // matter which of these two parallel loads resolves first — a pending
      // student (from an AI Speed Match hand-off) wins if still valid/active,
      // otherwise the first active child, same as before.
      var activeStudents = self.activeStudents();
      var preferredValid = pendingStudentId && activeStudents.some(function (s) { return s.id === pendingStudentId; });
      if (preferredValid) self.bookingForm.studentId = pendingStudentId;
      else if (activeStudents.length) self.bookingForm.studentId = activeStudents[0].id;
    });
    BookingService.getAll().then(function (res) {
      self.bookings = res.data;
      // A parent can only message a tutor they actually have a relationship with —
      // pick the first contactable tutor once bookings are known, not an arbitrary
      // one from the full public catalog.
      computeContactableTutors();
      if ($location.path() === '/parent/chat') {
        self.loadUnreadCounts();
        if (self.contactableTutors.length) self.loadChat(self.contactableTutors[0].id);
      }
    });
    InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
    ParentProfileService.getProfile().then(function (res) { self.parentProfile = res.data; });
    TutorService.getFavorites().then(function (res) { self.favoriteTutorIds = res.data; });

    // Forced "tutor cancelled your class" popup — only checked/shown on the
    // dashboard page (explicit requirement), but reappears every time the
    // parent lands back there until every pending item is resolved. The GET
    // itself also sweeps auto-accept server-side (see
    // PresetCancellationsController.GetMine), so a proposal nobody responded
    // to before its date/time passed shows up already resolved, not pending.
    if ($location.path() === '/parent/dashboard') {
      PresetCancellationService.getMine().then(function (res) {
        self.pendingCancellations = res.data;
      });
    }
  }
  init();

  self.pendingCancellations = [];
  self.cancellationActionBusy = false;
  self.cancellationActionError = '';

  self.currentCancellation = function () {
    return self.pendingCancellations.length ? self.pendingCancellations[0] : null;
  };

  self.acceptCancellation = function (decision) {
    self.cancellationActionBusy = true;
    self.cancellationActionError = '';
    PresetCancellationService.accept(decision.id).then(function () {
      self.pendingCancellations = self.pendingCancellations.filter(function (d) { return d.id !== decision.id; });
      self.cancellationActionBusy = false;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
    }).catch(function (err) {
      self.cancellationActionBusy = false;
      self.cancellationActionError = (err.data && err.data.message) || 'Could not process this. Please try again.';
    });
  };

  self.rejectCancellation = function (decision) {
    self.cancellationActionBusy = true;
    self.cancellationActionError = '';
    PresetCancellationService.reject(decision.id).then(function () {
      self.pendingCancellations = self.pendingCancellations.filter(function (d) { return d.id !== decision.id; });
      self.cancellationActionBusy = false;
    }).catch(function (err) {
      self.cancellationActionBusy = false;
      self.cancellationActionError = (err.data && err.data.message) || 'Could not process this. Please try again.';
    });
  };

  self.acknowledgeCancellation = function (decision) {
    self.cancellationActionBusy = true;
    self.cancellationActionError = '';
    PresetCancellationService.acknowledge(decision.id).then(function () {
      self.pendingCancellations = self.pendingCancellations.filter(function (d) { return d.id !== decision.id; });
      self.cancellationActionBusy = false;
      InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
    }).catch(function (err) {
      self.cancellationActionBusy = false;
      self.cancellationActionError = (err.data && err.data.message) || 'Could not process this. Please try again.';
    });
  };

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
    var all = self.tutors.filter(function (t) {
      var q = self.searchQuery.toLowerCase();
      var matchQuery = !q || t.name.toLowerCase().indexOf(q) >= 0 ||
        t.subjects.some(function (s) { return s.toLowerCase().indexOf(q) >= 0; }) ||
        (t.qualifications || []).some(function (ql) { return ql.toLowerCase().indexOf(q) >= 0; });
      var matchCountry = (t.offerings || []).some(function (o) { return o.country === self.selectedCountry; });
      // Matches on subject AND level together (via the tutor's actual offerings) — a
      // bare subject-name check would pass for a tutor teaching the subject at a
      // completely different level than the one selected.
      var matchSub = !self.selectedSubject || (t.offerings || []).some(function (o) {
        return o.subject === self.selectedSubject.subject && o.level === self.selectedSubject.level;
      });
      var matchMode = self.selectedMode === 'All' || t.modes.indexOf(self.selectedMode) >= 0;
      var matchRating = !self.minRating || t.rating >= self.minRating;
      var matchExperience = !self.minExperience || t.experienceYears >= self.minExperience;
      // The catalog only ever books from a tutor's published preset classes now
      // (see viewAndBookPreset) — a tutor with none isn't bookable from this page,
      // so hide them entirely rather than showing a card with no action on it.
      var hasPresetClasses = self.tutorHasAnySlots(t);
      return matchQuery && matchCountry && matchSub && matchMode && matchRating && matchExperience && hasPresetClasses;
    });

    // Pinned tutor (see init()) always leads, regardless of filters below —
    // the whole point is it stays visible after a welcome-page hand-off. The
    // rest keeps the existing favorited-first ordering unchanged.
    var pinned = null;
    var rest = [];
    all.forEach(function (t) {
      if (String(t.id) === String(self.pinnedTutorId)) pinned = t;
      else rest.push(t);
    });

    rest.sort(function (a, b) {
      return (self.isFavorited(b.id) ? 1 : 0) - (self.isFavorited(a.id) ? 1 : 0);
    });

    return pinned ? [pinned].concat(rest) : rest;
  };

  // ── Next month label e.g. "Aug 2026" ──────────────────────────────
  self.nextMonthLabel = function () {
    var months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    var d = new Date();
    d.setMonth(d.getMonth() + 1);
    return months[d.getMonth()] + ' ' + d.getFullYear();
  };

  // ── Returns the "upcoming" date range as YYYY-MM-DD strings: today
  // through the end of next month. Deliberately NOT just next calendar
  // month alone — a slot dated later this month (e.g. published 9 days out)
  // is exactly the kind of imminent, bookable class this strip exists to
  // surface, and excluding it just because it isn't technically "next
  // month" would hide real, useful data for no good reason.
  self._nextMonthRange = function () {
    var now = new Date();
    var y = now.getMonth() === 11 ? now.getFullYear() + 1 : now.getFullYear();
    var m = (now.getMonth() + 1) % 12;
    var end = new Date(y, m + 1, 0);
    var pad = function (n) { return n < 10 ? '0' + n : '' + n; };
    var fmt = function (d) { return d.getFullYear() + '-' + pad(d.getMonth()+1) + '-' + pad(d.getDate()); };
    var start = now;
    return { start: fmt(start), end: fmt(end) };
  };

  // Preset-slot summary for one tutor — computed ONCE per tutor (see init(),
  // where this is called right after self.tutors loads) and cached on
  // t._presetSummary, NOT recomputed from the template. tutorNextMonthSlots/
  // tutorHasAnySlots/tutorSlotsBySubject/tutorSubjectsWithoutSlots below are
  // called directly from ng-if/ng-repeat in the template; a function called
  // from there that builds fresh arrays/objects every call never stabilizes
  // and trips Angular's infinite-digest guard ($rootScope:infdig) — the
  // original version of this code did exactly that (same trap fixed
  // elsewhere for setupClassUniqueSubjects/computeContactableTutors, just
  // missed here when this feature was first built).
  function computeTutorPresetSummary(t) {
    var range = self._nextMonthRange();
    var slots = (t.timetable || []).filter(function (s) {
      return s.mode && s.day >= range.start && s.day <= range.end;
    }).map(function (s) {
      return {
        id: s.id,
        date: s.day,
        startTime: s.time,
        endTime: s.endTime,
        isFull: s.isFull,
        subject: s.subject,
        level: s.level,
        mode: s.mode,
        classSize: s.classSize,
        pricePerLesson: s.pricePerLesson,
        confirmedCount: s.confirmedCount,
        maxStudents: s.maxStudents,
        presetGroupId: s.presetGroupId
      };
    });

    var groups = {};
    var days = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];
    var months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];

    // Grouped by preset-id (one id per Setup Class submission, assigned server-side —
    // see SetupClass in TutorsController) rather than subject+mode, so a class only
    // merges with the OTHER occurrences it was actually published together with, not
    // every unrelated slot that happens to share the same subject. Falls back to
    // subject|mode for any pre-migration row that somehow still lacks a group id.
    slots.forEach(function (s) {
      var key = s.presetGroupId || (s.subject + '|' + s.mode);
      if (!groups[key]) {
        groups[key] = {
          presetGroupId: key,
          subject: s.subject,
          level: s.level,
          mode: s.mode,
          classSize: s.classSize,
          pricePerLesson: s.pricePerLesson,
          startTime: s.startTime,
          endTime: s.endTime,
          recurrenceLabel: '',
          slots: []
        };
      }
      var d = new Date(s.date + 'T00:00:00');
      groups[key].slots.push({
        id: s.id,
        date: s.date,
        dateFormatted: days[d.getDay()] + ', ' + d.getDate() + ' ' + months[d.getMonth()],
        dateShort: d.getDate() + ' ' + months[d.getMonth()],
        startTime: s.startTime,
        endTime: s.endTime,
        isFull: s.isFull,
        classSize: s.classSize,
        confirmedCount: s.confirmedCount || 0,
        maxStudents: s.maxStudents || 1
      });
    });

    var nm = self.nextMonthLabel().split(' ');
    var groupList = Object.keys(groups).map(function (k) { return groups[k]; });
    groupList.forEach(function (g) {
      g.slots.sort(function (a, b) { return a.date.localeCompare(b.date); });
      if (g.slots.length > 0) {
        var d = new Date(g.slots[0].date + 'T00:00:00');
        g.recurrenceLabel = 'Every ' + ['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'][d.getDay()] + ', ' + nm[0] + ' ' + nm[1];
      }
    });

    var subjectsWithSlots = groupList.map(function (g) { return g.subject; });
    var subjectsWithoutSlots = (t.offerings || []).filter(function (o) {
      return subjectsWithSlots.indexOf(o.subject) < 0;
    }).reduce(function (acc, o) {
      if (!acc.some(function (x) { return x.subject === o.subject; })) acc.push(o);
      return acc;
    }, []);

    return {
      openSlots: slots.filter(function (s) { return !s.isFull; }),
      hasAny: slots.length > 0,
      groups: groupList,
      subjectsWithoutSlots: subjectsWithoutSlots
    };
  }

  // ── All preset slots for a tutor in next month ────────────────────
  self.tutorNextMonthSlots = function (t) {
    return (t._presetSummary && t._presetSummary.openSlots) || [];
  };

  // ── Does the tutor have ANY preset slots (full or not) next month ─
  self.tutorHasAnySlots = function (t) {
    return !!(t._presetSummary && t._presetSummary.hasAny);
  };

  // ── Group tutor's next month slots by subject ─────────────────────
  self.tutorSlotsBySubject = function (t) {
    return (t._presetSummary && t._presetSummary.groups) || [];
  };

  // ── Subjects tutor teaches but has NO preset slot next month ───────
  self.tutorSubjectsWithoutSlots = function (t) {
    return (t._presetSummary && t._presetSummary.subjectsWithoutSlots) || [];
  };

  // ── Mode icon helper (Tabler icon suffix) ─────────────────────────
  self.modeIcon = function (mode) {
    var map = {
      'Online': 'video',
      'Home Visit': 'home',
      'Tutor Place': 'building',
      'Tuition Center': 'building'
    };
    return map[mode] || 'calendar';
  };

  // Each row in the catalog/AI-Match "Available classes" list has its own View &
  // Book button — jumps straight to the booking summary for THAT class (via
  // selectTutor()'s existing t._selectedPresetGroupId pre-fill logic), no
  // separate select-then-click-a-shared-button step needed.
  self.viewAndBookPreset = function (t, sg) {
    t._selectedPresetGroupId = sg.presetGroupId;
    self.selectTutor(t);
  };

  // AI Speed Match equivalent of viewAndBookPreset — this page has no in-page
  // booking-detail section of its own, so it reuses goToBookTutor's existing
  // PendingMatchService hand-off to land on that section over on /parent/search.
  self.viewAndBookPresetMatch = function (t, sg) {
    t._selectedPresetGroupId = sg.presetGroupId;
    self.goToBookTutor(t);
  };

  // The preset-class list (.tutor-slot-list) scrolls internally (max ~4 rows
  // visible) — a plain CSS :hover + position:absolute tooltip would get
  // clipped by that scroll box the moment its content is taller than the box
  // itself, even with nothing currently scrolled (overflow:auto clips
  // absolutely-positioned descendants unconditionally, not just while
  // scrolled). Computing position:fixed coordinates here escapes that
  // clipping entirely, since fixed positioning is relative to the viewport,
  // not any scrolling ancestor.
  self.showRowTooltip = function (sg, $event) {
    var rowRect = $event.currentTarget.getBoundingClientRect();
    // Anchored below the WHOLE scrollable list (.tutor-slot-list), not just the
    // hovered row — anchoring to the row alone let the tooltip overlap
    // whichever sibling row(s) happened to sit right below it, leaving that
    // row's own View & Book button peeking out beside the tooltip looking
    // like a dangling, disconnected control. Below the full list, it never
    // overlaps any row regardless of which one is hovered.
    var listEl = $event.currentTarget.closest('.tutor-slot-list') || $event.currentTarget;
    var listRect = listEl.getBoundingClientRect();

    // Flip upward when there isn't reasonably enough room below (e.g. the card
    // is scrolled near the bottom of the viewport) — otherwise the tooltip
    // runs off the bottom of the screen instead of being fully visible.
    // Anchoring with `bottom` (grows upward) rather than computing `top` from
    // an estimated height means this works regardless of the tooltip's actual
    // rendered height, which varies with how many dates are in the schedule.
    var spaceBelow = window.innerHeight - listRect.bottom;
    var flipUp = spaceBelow < 260;

    sg._tooltipStyle = flipUp ? {
      display: 'block',
      top: 'auto',
      bottom: (window.innerHeight - listRect.top + 6) + 'px',
      left: rowRect.left + 'px'
    } : {
      display: 'block',
      top: (listRect.bottom + 6) + 'px',
      bottom: 'auto',
      left: rowRect.left + 'px'
    };
  };

  self.hideRowTooltip = function (sg) {
    sg._tooltipStyle = null;
  };

  // 'YYYY-MM-DD' (as stored/returned by the API) -> 'DD-MM-YYYY' (what the
  // booking form's fp-date-bound session.date fields expect — see filters.js).
  function toDdMmYyyy(ymd) {
    var m = String(ymd || '').match(/^(\d{4})-(\d{2})-(\d{2})$/);
    return m ? (m[3] + '-' + m[2] + '-' + m[1]) : '';
  }

  // Select a tutor to book. preferredStudentId (optional) is the child a match
  // was actually run for (e.g. AI Speed Match, via PendingMatchService) — when
  // given and valid, it wins over the plain "first active child" default, so a
  // booking made off an AI Speed Match result doesn't silently land on whichever
  // child happens to be first in the parent's list instead of the one matched.
  self.selectTutor = function (tutor, preferredStudentId) {
    self.selectedTutor = tutor;
    self.selectedPresetGroup = null;
    self.presetBookingError = '';
    self.bookingForm.subject = tutor.subjects[0] || '';
    self.bookingForm.classesPerMonth = 1;
    self.bookingForm.sessions = [{ date: '', startTime: '04:00 PM', endTime: '05:00 PM', recurring: false }];
    var activeStudents = self.activeStudents();
    var preferredValid = preferredStudentId && activeStudents.some(function (s) { return s.id === preferredStudentId; });
    if (preferredValid) self.bookingForm.studentId = preferredStudentId;
    else if (activeStudents.length) self.bookingForm.studentId = activeStudents[0].id;

    // A published class chip was selected first — carry its subject and exact
    // schedule (one session row per still-bookable occurrence) into the booking
    // form/invoice summary. The parent only needs to pick a child from here.
    // Already-full occurrences are dropped — nothing left to book there.
    var groupId = tutor._selectedPresetGroupId;
    var group = groupId && tutor._presetSummary &&
      (tutor._presetSummary.groups || []).find(function (g) { return g.presetGroupId === groupId; });
    var openSlots = group ? group.slots.filter(function (s) { return !s.isFull; }) : [];
    if (group && openSlots.length > 0) {
      self.selectedPresetGroup = angular.extend({}, group, { slots: openSlots });
      self.bookingForm.subject = group.subject;
      self.bookingForm.classesPerMonth = openSlots.length;
      self.bookingForm.sessions = openSlots.map(function (s) {
        return { date: toDdMmYyyy(s.date), startTime: s.startTime, endTime: s.endTime, recurring: false };
      });
    }

    self.tutorCal.year = _scNow.getFullYear();
    self.tutorCal.month = _scNow.getMonth();
    self.tutorCal.selectedDay = 0;
    self.tutorCal.busyTimes = [];
    TutorService.getBusyTimes(tutor.id).then(function (res) { self.tutorCal.busyTimes = res.data; });
  };

  // Jump from AI Speed Match results straight into the booking flow on the search page.
  // Route changes re-instantiate ParentCtrl, so the tutor id (and, if a preset class
  // chip was selected on the AI Match card, its group id too) is handed off via
  // PendingMatchService and picked back up once the search page's tutor list has
  // loaded (see init()) — landing straight on the booking summary instead of the
  // plain request form, same as picking a chip directly on the search page does.
  self.goToBookTutor = function (tutor) {
    PendingMatchService.setTutor(tutor.id, tutor._selectedPresetGroupId, self.aiMatchAppliedStudentId);
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

  self.hasInvalidReschedule = function () {
    return (self.rescheduleForm.classes || []).some(function (c) {
      if (c.date && !toDateObj(c.date)) return true;
      return parseClockTimeMinutes(c.proposedStartTime) === null;
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

  // Parses "H:MM AM/PM" into minutes since midnight, or null if unrecognizable OR the
  // hour is out of the valid 1-12 range for 12-hour format (e.g. "19:00 PM" — a
  // self-contradictory 24-hour hour with an AM/PM suffix tacked on; can't be auto-fixed
  // since it's ambiguous what was actually meant, so it's rejected rather than converted).
  function parseClockTimeMinutes(timeStr) {
    var m = String(timeStr || '').match(/^(\d{1,2}):(\d{2})\s*(AM|PM)$/i);
    if (!m) return null;
    var h = parseInt(m[1], 10), min = parseInt(m[2], 10), ampm = m[3].toUpperCase();
    if (h < 1 || h > 12 || min < 0 || min > 59) return null;
    if (ampm === 'PM' && h !== 12) h += 12;
    if (ampm === 'AM' && h === 12) h = 0;
    return h * 60 + min;
  }

  // Converts a bare 24-hour "HH:MM" time (no AM/PM suffix) into 12-hour "H:MM AM/PM"
  // format. Leaves anything else (already-AM/PM, or unparseable) unchanged, so this is
  // safe to run on every time input's change event without fighting the user's typing.
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

  // Every session's date and time must be a valid, fully-formed value — a malformed or
  // partial entry (e.g. "05:00 A") is rejected outright, not silently skipped — and end
  // time must be strictly after start time, same day (no overnight spans).
  self.hasInvalidTimeRangeSession = function () {
    return (self.bookingForm.sessions || []).some(function (s) {
      if (s.date && !toDateObj(s.date)) return true;
      var start = parseClockTimeMinutes(s.startTime);
      var end = parseClockTimeMinutes(s.endTime);
      if (start === null || end === null) return true;
      return end <= start;
    });
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

  // A booking's classes must all be the same length — durationHours is a single value
  // for the whole booking, so every session's actual (end - start) time span must match
  // the first session's. There's no separate "duration" input for the user to get out of
  // sync with the times themselves; it's always derived from what was actually entered.
  self.hasDurationMismatchSession = function () {
    var sessions = self.bookingForm.sessions || [];
    if (sessions.length < 2) return false;
    var first = parseTimeRangeHours(sessions[0].startTime + ' - ' + sessions[0].endTime);
    if (first === null) return false;
    return sessions.some(function (s) {
      return self.isDurationMismatch(s.startTime + ' - ' + s.endTime, first);
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
    session.startTime = normalizeTimeToAmPm(session.startTime);
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

  self.onEndTimeChange = function (session) {
    session.endTime = normalizeTimeToAmPm(session.endTime);
  };

  self.clearSelectedTutor = function () {
    self.selectedTutor = null;
    self.selectedPresetGroup = null;
    self.presetBookingError = '';
  };

  // Book a tutor
  self.hasProfaneBookingMessage = function () {
    return ProfanityFilterService.containsProfanity(self.bookingForm.message);
  };

  self.submitBooking = function () {
    if (!self.selectedTutor) return;
    if (self.hasTooSoonSession()) return;
    if (self.hasInvalidTimeRangeSession()) return;
    if (self.hasDurationMismatchSession()) return;
    if (self.hasDuplicateSessions()) return;
    if (self.hasProfaneBookingMessage()) return;
    var student = self.students.find(function (s) { return s.id === self.bookingForm.studentId; });
    var classes = self.bookingForm.sessions.map(function (session) {
      return { date: toDateStr(session.date), time: session.startTime + ' - ' + session.endTime };
    });
    // durationHours is derived from what was actually entered, not a separate field the
    // user never sees or edits — that disconnect is exactly how a booking could end up
    // with a 2-hour time range stored against a 1-hour duration.
    var derivedDuration = parseTimeRangeHours(classes[0].time);
    BookingService.create({
      tutorId: self.selectedTutor.id,
      studentId: self.bookingForm.studentId,
      subject: self.bookingForm.subject + ' - ' + (student ? student.educationLevel : ''),
      mode: self.selectedTutor.modes[0],
      classes: classes,
      durationHours: derivedDuration !== null ? derivedDuration : 1,
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
    self.editStudentNameError = '';
    self.editStudentGoalError = '';
    rebuildPreferredPools(s.preferredModes);
  };

  self.cancelEditStudent = function () {
    self.editingStudent = null;
    self.editStudentForm = {};
    self.editStudentSubjects = [];
    self.newEditStudentSubjectCombo = { country: '', selectedOption: null };
    self.preferredLeft = [];
    self.preferredRight = [];
  };

  function rebuildPreferredPools(preferredModes) {
    var preferred = preferredModes || [];
    self.preferredRight = preferred
      .map(function (mode) { return TeachingModesCatalog.filter(function (m) { return m.mode === mode; })[0]; })
      .filter(function (m) { return !!m; });
    self.preferredLeft = TeachingModesCatalog.filter(function (m) { return preferred.indexOf(m.mode) === -1; });
  }

  self.onPreferredDropToRight = function (item) {
    if (self.preferredRight.some(function (m) { return m.mode === item.mode; })) return;
    self.preferredLeft = self.preferredLeft.filter(function (m) { return m.mode !== item.mode; });
    self.preferredRight.push(item);
  };

  self.onPreferredDropToLeft = function (item) {
    if (self.preferredLeft.some(function (m) { return m.mode === item.mode; })) return;
    self.preferredRight = self.preferredRight.filter(function (m) { return m.mode !== item.mode; });
    self.preferredLeft.push(item);
  };

  self.removePreferredMode = function (mode) {
    self.onPreferredDropToLeft(mode);
  };

  // Drop directly on a row within the ordered pool — inserts at that row's position,
  // whether the dragged item came from the left pool or is being reordered in-place.
  self.onPreferredDropOnItem = function (item, targetIndex) {
    var fromIndex = self.preferredRight.findIndex(function (m) { return m.mode === item.mode; });
    if (fromIndex !== -1) {
      self.preferredRight.splice(fromIndex, 1);
      if (fromIndex < targetIndex) targetIndex -= 1;
    } else {
      self.preferredLeft = self.preferredLeft.filter(function (m) { return m.mode !== item.mode; });
    }
    if (targetIndex > self.preferredRight.length) targetIndex = self.preferredRight.length;
    if (targetIndex < 0) targetIndex = 0;
    self.preferredRight.splice(targetIndex, 0, item);
  };

  self.movePreferredModeUp = function (index) {
    if (index <= 0) return;
    var arr = self.preferredRight;
    var tmp = arr[index - 1];
    arr[index - 1] = arr[index];
    arr[index] = tmp;
  };

  self.movePreferredModeDown = function (index) {
    if (index >= self.preferredRight.length - 1) return;
    var arr = self.preferredRight;
    var tmp = arr[index + 1];
    arr[index + 1] = arr[index];
    arr[index] = tmp;
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

  // Child-name validation — same rule as registration (see NameValidationService).
  self.editStudentNameError = '';
  self.validateEditStudentName = function () {
    self.editStudentNameError = NameValidationService.validate(self.editStudentForm.name);
  };

  self.editStudentGoalError = '';

  self.saveEditStudent = function () {
    if (!self.editingStudent || !self.editStudentForm.name.trim()) return;
    self.validateEditStudentName();
    if (self.editStudentNameError) return;
    self.editStudentGoalError = ProfanityFilterService.validate(self.editStudentForm.learningGoal);
    if (self.editStudentGoalError) return;
    self.preferredModesError = '';
    if (!self.preferredRight.length) {
      self.preferredModesError = 'Please select at least one preferred teaching mode.';
      return;
    }
    var payload = {
      name: self.editStudentForm.name,
      school: self.editStudentForm.school || '',
      educationLevel: self.editStudentSubjects.length ? self.editStudentSubjects[0].level : (self.editingStudent.educationLevel || ''),
      subjectSelect: self.formatSubjectCombos(self.editStudentSubjects),
      learningGoal: self.editStudentForm.learningGoal || null
    };
    var preferredModes = self.preferredRight.map(function (m) { return m.mode; });
    var editingId = self.editingStudent.id;
    StudentService.update(editingId, payload).then(function () {
      return StudentService.updatePreferredModes(editingId, preferredModes);
    }).then(function (res) {
      res.data.subjectCombos = self.parseSubjectCombos(res.data.subjectSelect, res.data.educationLevel);
      var idx = self.students.findIndex(function (s) { return s.id === editingId; });
      if (idx >= 0) self.students[idx] = res.data;
      self.cancelEditStudent();
    }, function () {
      self.preferredModesError = 'Failed to save changes. Please try again.';
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

  self.onNewStudentPreferredDropToRight = function (item) {
    if (self.newStudentPreferredRight.some(function (m) { return m.mode === item.mode; })) return;
    self.newStudentPreferredLeft = self.newStudentPreferredLeft.filter(function (m) { return m.mode !== item.mode; });
    self.newStudentPreferredRight.push(item);
  };

  self.onNewStudentPreferredDropToLeft = function (item) {
    if (self.newStudentPreferredLeft.some(function (m) { return m.mode === item.mode; })) return;
    self.newStudentPreferredRight = self.newStudentPreferredRight.filter(function (m) { return m.mode !== item.mode; });
    self.newStudentPreferredLeft.push(item);
  };

  self.removeNewStudentPreferredMode = function (mode) {
    self.onNewStudentPreferredDropToLeft(mode);
  };

  self.onNewStudentPreferredDropOnItem = function (item, targetIndex) {
    var fromIndex = self.newStudentPreferredRight.findIndex(function (m) { return m.mode === item.mode; });
    if (fromIndex !== -1) {
      self.newStudentPreferredRight.splice(fromIndex, 1);
      if (fromIndex < targetIndex) targetIndex -= 1;
    } else {
      self.newStudentPreferredLeft = self.newStudentPreferredLeft.filter(function (m) { return m.mode !== item.mode; });
    }
    if (targetIndex > self.newStudentPreferredRight.length) targetIndex = self.newStudentPreferredRight.length;
    if (targetIndex < 0) targetIndex = 0;
    self.newStudentPreferredRight.splice(targetIndex, 0, item);
  };

  self.moveNewStudentPreferredModeUp = function (index) {
    if (index <= 0) return;
    var arr = self.newStudentPreferredRight;
    var tmp = arr[index - 1];
    arr[index - 1] = arr[index];
    arr[index] = tmp;
  };

  self.moveNewStudentPreferredModeDown = function (index) {
    if (index >= self.newStudentPreferredRight.length - 1) return;
    var arr = self.newStudentPreferredRight;
    var tmp = arr[index + 1];
    arr[index + 1] = arr[index];
    arr[index] = tmp;
  };

  // Add student
  self.studentSubjectsError = '';
  self.studentNameError = '';
  self.studentGoalError = '';
  self.validateStudentName = function () {
    self.studentNameError = NameValidationService.validate(self.studentForm.name);
  };

  self.createStudent = function () {
    self.studentSubjectsError = '';
    self.newStudentPreferredModesError = '';
    if (!self.studentForm.name.trim()) return;
    self.validateStudentName();
    if (self.studentNameError) return;
    self.studentGoalError = ProfanityFilterService.validate(self.studentForm.learningGoal);
    if (self.studentGoalError) return;
    if (!self.studentSubjects.length) {
      self.studentSubjectsError = 'Please add at least one subject before creating the profile.';
      return;
    }
    if (!self.newStudentPreferredRight.length) {
      self.newStudentPreferredModesError = 'Please select at least one preferred teaching mode.';
      return;
    }

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
      var preferredModes = self.newStudentPreferredRight.map(function (m) { return m.mode; });
      StudentService.create(payload).then(function (res) {
        return StudentService.updatePreferredModes(res.data.id, preferredModes).then(function (modesRes) {
          return modesRes.data;
        });
      }).then(function (student) {
        student.subjectCombos = self.parseSubjectCombos(student.subjectSelect, student.educationLevel);
        self.students.push(student);
        self.newlyAddedStudentId = student.id;
        self.studentSuccess = true;
        self.studentForm = { name: '', birthDate: '', school: '', learningGoal: '', photoUrl: '' };
        self.studentNameError = '';
        self.studentGoalError = '';
        self.studentSubjects = [];
        self.newStudentSubjectCombo = { country: '', selectedOption: null };
        self.newStudentPreferredLeft = TeachingModesCatalog.slice();
        self.newStudentPreferredRight = [];
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
  self.payingBooking = null;

  // Sessions & Activity's "Pay Invoice" button opens this review panel first
  // (same invoice-summary layout as the preset-class booking summary on the Find
  // Tutors page) rather than charging immediately on click.
  self.openPaySummary = function (b) { self.payingBooking = b; };

  self.confirmPayInvoice = function () {
    if (!self.payingBooking) return;
    var inv = self.getInvoiceForBooking(self.payingBooking.id);
    self.payingBooking = null;
    if (inv) self.payInvoice(inv.id);
  };

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
    self.issueError = '';
  };

  self.submitIssue = function () {
    self.issueError = ProfanityFilterService.validate(self.issueForm.details);
    if (self.issueError) return;
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
    }, function (err) {
      self.issueError = (err.data && err.data.message) ? err.data.message : 'Failed to submit. Please try again.';
    });
  };

  // Chat — a parent can only contact tutors they have an accepted (confirmed or
  // completed) booking with, not the entire public catalog. Computed once when
  // bookings load (not a template-called function) — a function called from the
  // view that allocates a new array/objects every digest never stabilizes and
  // trips Angular's infinite-digest guard.
  self.contactableTutors = [];
  self.activeTutor = null;
  self.unreadCounts = {}; // { tutorId: count } — sidebar badges, see ChatController.GetUnreadCounts

  self.loadUnreadCounts = function () {
    ChatService.getUnreadCounts().then(function (res) { self.unreadCounts = res.data; });
  };

  function computeContactableTutors() {
    var seen = {};
    var list = [];
    (self.bookings || []).forEach(function (b) {
      if ((b.status !== 'confirmed' && b.status !== 'completed') || seen[b.tutorId]) return;
      seen[b.tutorId] = true;
      list.push({ id: b.tutorId, name: b.tutorName, imageUrl: b.tutorImageUrl });
    });
    self.contactableTutors = list;
  }

  self.loadChat = function (tutorId) {
    self.activeTutorId = tutorId;
    self.activeTutor = self.contactableTutors.find(function (t) { return t.id === tutorId; }) || null;
    ChatService.getMessages(tutorId, self.user.userId).then(function (res) {
      self.chatMessages = res.data;
      // Opening the thread just marked its unread messages read server-side
      // (see ChatController.GetMessages) — clear the badge immediately rather
      // than waiting for the next poll tick.
      self.unreadCounts[tutorId] = 0;
      scrollChatToBottom();
    });
  };

  self.chatError = '';

  self.sendMessage = function () {
    if (!self.chatText.trim() || !self.activeTutorId) return;
    self.chatError = ProfanityFilterService.validate(self.chatText);
    if (self.chatError) return;
    ChatService.send({ tutorId: self.activeTutorId, parentUserId: self.user.userId, text: self.chatText })
      .then(function (res) {
        self.chatMessages.push(res.data);
        self.chatText = '';
        scrollChatToBottom();
      }, function (err) {
        self.chatError = (err.data && err.data.message) ? err.data.message : 'Failed to send. Please try again.';
      });
  };

  // Auto-refresh the open conversation so a reply shows up without the parent
  // needing to reload the page. Polls rather than pushing over a live socket —
  // simplest option for this app's scale, mirrors the existing booking poll in
  // tutor.controller.js. Only appends messages the client hasn't already seen
  // (by id) so an in-progress read/scroll position isn't disrupted by replacing
  // the whole array every tick.
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
    self.loadUnreadCounts();
    if (!self.activeTutorId) return;
    ChatService.getMessages(self.activeTutorId, self.user.userId).then(function (res) {
      mergeNewChatMessages(res.data);
      self.unreadCounts[self.activeTutorId] = 0;
    });
  }, 4000);
  $scope.$on('$destroy', function () { $interval.cancel(_chatPollInterval); });

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

  var BOOKING_STATUS_ORDER = { pending: 0, countered: 1, confirmed: 2, completed: 3 };

  self.nonCancelledBookings = function () {
    return self.bookings
      .filter(function (b) { return b.status !== 'cancelled'; })
      .slice()
      .sort(function (a, b) {
        var rankA = BOOKING_STATUS_ORDER.hasOwnProperty(a.status) ? BOOKING_STATUS_ORDER[a.status] : 99;
        var rankB = BOOKING_STATUS_ORDER.hasOwnProperty(b.status) ? BOOKING_STATUS_ORDER[b.status] : 99;
        return rankA - rankB;
      });
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
