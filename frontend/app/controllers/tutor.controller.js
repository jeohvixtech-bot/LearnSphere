'use strict';

angular.module('learnSphereApp')
.controller('TutorCtrl', ['$location', '$timeout', '$interval', '$rootScope', 'AuthService', 'TutorService',
  'BookingService', 'ChatService', 'InvoiceService', 'ScheduleService',
function ($location, $timeout, $interval, $rootScope, AuthService, TutorService, BookingService, ChatService, InvoiceService, ScheduleService) {
  var self = this;
  var user = AuthService.getCurrentUser();
  self.user = user;
  self.tutor = null;
  self.bookings = [];
  self.invoices = [];
  self.chatMessages = [];

  // Tab state
  self.activeTab = 'overview';

  // Edit profile form
  self.profileForm = {};
  self.profileSuccess = false;
  self.profileError = '';
  self.uploading = false;
  self.newOffering = { subject: '', level: '', mode: '', qualification: '', price: null };

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
  self.selectedCalDay = new Date().getDate();
  var _now = new Date();
  self.calYear = _now.getFullYear();
  self.calMonth = _now.getMonth(); // 0-indexed

  function init() {
    TutorService.getByUser(user.userId).then(function (res) {
      self.tutor = res.data;
      self.blockedRanges = ScheduleService.getBlocked(res.data.id);
      self.profileForm = {
        imageUrl: res.data.imageUrl,
        bio: res.data.bio,
        experienceYears: res.data.experienceYears,
        offerings: res.data.offerings && res.data.offerings.length
          ? res.data.offerings.map(function(o) {
              return { subject: o.subject, level: o.level, mode: o.mode, qualification: o.qualification, price: o.price || 0 };
            })
          : []
      };
    });
    BookingService.getAll().then(function (res) { self.bookings = res.data; });
    InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
    if ($location.path() === '/tutor/chat') {
      loadChat();
    }
  }
  init();

  function loadChat() {
    if (!self.tutor) {
      $timeout(function () { if (self.tutor) loadChatById(self.tutor.id); }, 1000);
    } else {
      loadChatById(self.tutor.id);
    }
  }

  function loadChatById(tutorId) {
    ChatService.getMessages(tutorId).then(function (res) { self.chatMessages = res.data; });
  }

  // Computed props
  self.pendingRequests = function () {
    if (!self.tutor) return [];
    return self.bookings.filter(function (b) { return b.tutorId === self.tutor.id && b.status === 'pending'; });
  };

  self.confirmedClasses = function () {
    if (!self.tutor) return [];
    return self.bookings.filter(function (b) {
      return b.tutorId === self.tutor.id && (b.status === 'confirmed' || b.status === 'countered');
    });
  };

  self.completedClasses = function () {
    if (!self.tutor) return [];
    return self.bookings.filter(function (b) { return b.tutorId === self.tutor.id && b.status === 'completed'; });
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

  self.confirmBlock = function () {
    var s = self.blockForm.startDate, e = self.blockForm.endDate;
    if (!s || !e) return;
    var start = parseLocalDate(s);
    var end   = parseLocalDate(e);
    if (end < start) return;

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
          conflicts.push({ bookingId: b.id, subject: b.subject, student: b.studentName, date: c.date, time: c.time });
        }
      });
    });

    if (conflicts.length) {
      self.blockConflicts = conflicts;
      return;
    }

    self.blockConflicts = [];
    ScheduleService.addBlock(self.tutor.id, s, e); // normalises to YYYY-MM-DD strings
    self.blockForm.startDate = '';
    self.blockForm.endDate = '';
  };

  self.clearBlockConflicts = function () { self.blockConflicts = []; };

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

  // ── Monthly calendar ────────────────────────────────────────────────
  var _monthNames = ['January','February','March','April','May','June',
                     'July','August','September','October','November','December'];

  self.calMonthName = function () { return _monthNames[self.calMonth]; };

  self.prevMonth = function () {
    if (self.calMonth === 0) { self.calMonth = 11; self.calYear--; }
    else { self.calMonth--; }
    self.selectedCalDay = 0;
  };

  self.nextMonth = function () {
    if (self.calMonth === 11) { self.calMonth = 0; self.calYear++; }
    else { self.calMonth++; }
    self.selectedCalDay = 0;
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

  self.bookingsOnDay = function (dayNum) {
    if (!self.tutor || !dayNum) return [];
    var s = calDayStr(dayNum);
    return self.bookings.filter(function (b) {
      return b.tutorId === self.tutor.id &&
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

  self.startCounter = function (booking) {
    self.counterBooking = booking;
    self.counterForm = {
      message: '',
      classes: (booking.classes || []).map(function (c) {
        return { originalDate: c.date, originalTime: c.time, proposedDate: c.date, proposedTime: c.time };
      })
    };
    self.counterSuccess = false;
  };

  self.submitCounter = function () {
    if (!self.counterBooking) return;
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
      return { originalDate: c.date, proposedDate: c.date, proposedTime: c.time };
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

  self.startReport = function (booking) {
    self.reportBooking = booking;
    self.reportForm = { covered: '', performance: '', homework: '' };
    self.reportSuccess = false;
  };

  self.submitReport = function () {
    if (!self.reportBooking) return;
    BookingService.submitLessonReport(self.reportBooking.id, self.reportForm).then(function () {
      self.reportSuccess = true;
      self.reportBooking = null;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.reportSuccess = false;
      }, 2000);
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
  };

  self.submitEdit = function () {
    if (!self.editBooking) return;
    BookingService.editLessonReport(self.editBooking.id, self.editForm).then(function () {
      self.editSuccess = true;
      self.editBooking = null;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.editSuccess = false;
      }, 2000);
    });
  };

  self.cancelEdit = function () { self.editBooking = null; };

  // Edit profile offerings
  self.addOffering = function () {
    if (!self.newOffering.subject || !self.newOffering.level || !self.newOffering.mode || !self.newOffering.qualification) return;
    self.profileForm.offerings.push({
      subject: self.newOffering.subject,
      level: self.newOffering.level,
      mode: self.newOffering.mode,
      qualification: self.newOffering.qualification,
      price: parseFloat(self.newOffering.price) || 0
    });
    self.newOffering = { subject: '', level: '', mode: '', qualification: '', price: null };
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

  self.saveProfile = function () {
    if (!self.tutor) return;
    self.profileSuccess = false;
    self.profileError = '';
    var payload = {
      imageUrl: self.profileForm.imageUrl,
      bio: self.profileForm.bio,
      experienceYears: self.profileForm.experienceYears,
      offerings: self.profileForm.offerings
    };
    TutorService.update(self.tutor.id, payload).then(function (res) {
      self.tutor = res.data;
      self.profileSuccess = true;
      $timeout(function () { self.profileSuccess = false; }, 3000);
    }, function () {
      self.profileError = 'Failed to save. Please try again.';
    });
  };

  // Poll for booking updates (e.g. parent accepts counter proposal)
  var _pollInterval = $interval(function () {
    if (!self.rescheduleBooking && !self.counterBooking && !self.reportBooking && !self.editBooking) {
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
    }
  }, 15000);
  $rootScope.$on('$destroy', function () { $interval.cancel(_pollInterval); });

  // Chat
  self.sendMessage = function () {
    if (!self.chatText.trim() || !self.tutor) return;
    ChatService.send({ tutorId: self.tutor.id, sender: 'tutor', text: self.chatText })
      .then(function (res) {
        self.chatMessages.push(res.data);
        self.chatText = '';
      });
  };
}]);
