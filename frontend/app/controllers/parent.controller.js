'use strict';

angular.module('learnSphereApp')
.controller('ParentCtrl', ['$location', '$timeout', '$q', 'AuthService', 'TutorService',
  'StudentService', 'BookingService', 'InvoiceService', 'ChatService', 'AdminService', 'ScheduleService',
function ($location, $timeout, $q, AuthService, TutorService, StudentService, BookingService, InvoiceService, ChatService, AdminService, ScheduleService) {
  var self = this;
  var user = AuthService.getCurrentUser();
  self.user = user;

  self.tutors = [];
  self.students = [];
  self.bookings = [];
  self.invoices = [];
  self.chatMessages = [];
  self.selectedTutor = null;

  // Search / filter state
  self.searchQuery = '';
  self.selectedSubject = 'All';
  self.selectedMode = 'All';
  self.minRating = 0;

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
    educationLevel: '',
    learningGoal: '',
    photoUrl: ''
  };
  self.studentSubjects = [];
  self.newStudentSubject = '';
  self.studentSuccess = false;
  self.newlyAddedStudentId = null;

  // Edit student state
  self.editingStudent = null;
  self.editStudentForm = {};
  self.editStudentSubjects = [];
  self.newEditStudentSubject = '';

  // School dropdown
  self.schoolSearch = '';
  self.schoolDropdownOpen = false;
  self.institutions = [];
  self.countryFilter = 'All';
  self.typeFilter = 'All';

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

  self.startReschedule = function (booking) {
    self.rescheduleBooking = booking;
    self.rescheduleForm.classes = (booking.classes || []).map(function (c) {
      return { originalDate: c.date, date: c.date, time: c.time };
    });
    self.rescheduleSuccess = false;
  };

  self.cancelReschedule = function () {
    self.rescheduleBooking = null;
    self.rescheduleForm = { classes: [] };
  };

  self.submitReschedule = function () {
    if (!self.rescheduleBooking) return;
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
    TutorService.getAll().then(function (res) { self.tutors = res.data; });
    StudentService.getMyStudents().then(function (res) {
      self.students = res.data;
      if (res.data.length) {
        self.bookingForm.studentId = res.data[0].id;
        self.studCal.selectedStudentId = res.data[0].id;
      }
    });
    BookingService.getAll().then(function (res) { self.bookings = res.data; });
    InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
  }
  init();

  // Filtered tutors
  self.filteredTutors = function () {
    return self.tutors.filter(function (t) {
      var q = self.searchQuery.toLowerCase();
      var matchQuery = !q || t.name.toLowerCase().indexOf(q) >= 0 ||
        t.subjects.some(function (s) { return s.toLowerCase().indexOf(q) >= 0; });
      var matchSub = self.selectedSubject === 'All' || t.subjects.indexOf(self.selectedSubject) >= 0;
      var matchMode = self.selectedMode === 'All' || t.modes.indexOf(self.selectedMode) >= 0;
      var matchRating = !self.minRating || t.rating >= self.minRating;
      return matchQuery && matchSub && matchMode && matchRating;
    });
  };

  // Select a tutor to book
  self.selectTutor = function (tutor) {
    self.selectedTutor = tutor;
    self.bookingForm.subject = tutor.subjects[0] || '';
    self.bookingForm.classesPerMonth = 1;
    self.bookingForm.sessions = [{ date: '', startTime: '04:00 PM', endTime: '05:00 PM', recurring: false }];
    if (self.students.length) self.bookingForm.studentId = self.students[0].id;
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
    });
  };

  // Edit student
  self.startEditStudent = function (s) {
    self.editingStudent = s;
    self.editStudentForm = { name: s.name, school: s.school, educationLevel: s.educationLevel, learningGoal: s.learningGoal || '' };
    self.editStudentSubjects = s.subjectSelect
      ? s.subjectSelect.split(',').map(function (x) { return x.trim(); }).filter(function (x) { return x; })
      : [];
    self.newEditStudentSubject = '';
  };

  self.cancelEditStudent = function () {
    self.editingStudent = null;
    self.editStudentForm = {};
    self.editStudentSubjects = [];
    self.newEditStudentSubject = '';
  };

  self.addEditStudentSubject = function () {
    var v = self.newEditStudentSubject && self.newEditStudentSubject.trim();
    if (v && self.editStudentSubjects.indexOf(v) < 0) self.editStudentSubjects.push(v);
    self.newEditStudentSubject = '';
  };

  self.removeEditStudentSubject = function (i) { self.editStudentSubjects.splice(i, 1); };

  self.saveEditStudent = function () {
    if (!self.editingStudent || !self.editStudentForm.name.trim()) return;
    var payload = {
      name: self.editStudentForm.name,
      school: self.editStudentForm.school || '',
      educationLevel: self.editStudentForm.educationLevel,
      subjectSelect: self.editStudentSubjects.join(', '),
      learningGoal: self.editStudentForm.learningGoal || null
    };
    StudentService.update(self.editingStudent.id, payload).then(function (res) {
      var idx = self.students.findIndex(function (s) { return s.id === self.editingStudent.id; });
      if (idx >= 0) self.students[idx] = res.data;
      self.cancelEditStudent();
    });
  };

  // Student subject chips
  self.addStudentSubject = function () {
    var v = self.newStudentSubject && self.newStudentSubject.trim();
    if (v && self.studentSubjects.indexOf(v) < 0) self.studentSubjects.push(v);
    self.newStudentSubject = '';
  };

  self.removeStudentSubject = function (i) { self.studentSubjects.splice(i, 1); };

  // Add student
  self.createStudent = function () {
    if (!self.studentForm.name.trim()) return;
    var payload = {
      name: self.studentForm.name,
      birthDate: self.studentForm.birthDate || null,
      school: self.studentForm.school || '',
      educationLevel: self.studentForm.educationLevel,
      subjectSelect: self.studentSubjects.join(', '),
      learningGoal: self.studentForm.learningGoal,
      photoUrl: self.studentForm.photoUrl || null
    };
    StudentService.create(payload).then(function (res) {
      self.students.push(res.data);
      self.newlyAddedStudentId = res.data.id;
      self.studentSuccess = true;
      self.studentForm = { name: '', birthDate: '', school: '', educationLevel: '', learningGoal: '', photoUrl: '' };
      self.studentSubjects = [];
      self.newStudentSubject = '';
      $timeout(function () { self.studentSuccess = false; }, 5000);
    });
  };

  // Pay an invoice
  self.paySuccess = false;

  self.payInvoice = function (invoiceId) {
    InvoiceService.pay(invoiceId).then(function () {
      self.paySuccess = true;
      InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
      $timeout(function () {
        self.paySuccess = false;
      }, 2500);
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
      country: self.countryFilter !== 'All' ? self.countryFilter : '',
      type: self.typeFilter !== 'All' ? self.typeFilter : ''
    }).then(function (res) { self.institutions = res.data; });
  };

  self.searchSchools = function () {
    AdminService.getInstitutions({
      search: self.schoolSearch,
      country: self.countryFilter !== 'All' ? self.countryFilter : '',
      type: self.typeFilter !== 'All' ? self.typeFilter : ''
    }).then(function (res) { self.institutions = res.data; });
  };

  self.selectSchool = function (name) {
    self.studentForm.school = name;
    self.schoolSearch = name;
    self.schoolDropdownOpen = false;
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
