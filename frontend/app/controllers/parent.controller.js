'use strict';

angular.module('learnSphereApp')
.controller('ParentCtrl', ['$location', '$timeout', 'AuthService', 'TutorService',
  'StudentService', 'BookingService', 'InvoiceService', 'ChatService', 'AdminService',
function ($location, $timeout, AuthService, TutorService, StudentService, BookingService, InvoiceService, ChatService, AdminService) {
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
    date: '',
    startTime: '04:00 PM',
    endTime: '05:00 PM',
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
    subjectSelect: '',
    learningGoal: '',
    photoUrl: ''
  };
  self.studentSuccess = false;
  self.newlyAddedStudentId = null;

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

  // Load data
  function init() {
    TutorService.getAll().then(function (res) { self.tutors = res.data; });
    StudentService.getMyStudents().then(function (res) {
      self.students = res.data;
      if (res.data.length) self.bookingForm.studentId = res.data[0].id;
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
    if (self.students.length) self.bookingForm.studentId = self.students[0].id;
  };

  self.clearSelectedTutor = function () { self.selectedTutor = null; };

  // Book a tutor
  self.submitBooking = function () {
    if (!self.selectedTutor) return;
    var student = self.students.find(function (s) { return s.id === self.bookingForm.studentId; });
    var payload = {
      tutorId: self.selectedTutor.id,
      studentId: self.bookingForm.studentId,
      subject: self.bookingForm.subject + ' - ' + (student ? student.educationLevel : 'Sec 3'),
      mode: self.selectedTutor.modes[0],
      date: self.bookingForm.date,
      time: self.bookingForm.startTime + ' - ' + self.bookingForm.endTime,
      durationHours: self.bookingForm.duration,
      message: self.bookingForm.message,
      totalPrice: self.selectedTutor.pricePerSession * self.bookingForm.duration
    };
    BookingService.create(payload).then(function (res) {
      self.bookings.unshift(res.data);
      self.bookingSuccess = true;
      $timeout(function () {
        self.bookingSuccess = false;
        self.selectedTutor = null;
        $location.path('/parent/sessions');
      }, 2000);
    });
  };

  // Add student
  self.createStudent = function () {
    if (!self.studentForm.name.trim()) return;
    var payload = {
      name: self.studentForm.name,
      birthDate: self.studentForm.birthDate || null,
      school: self.studentForm.school || '',
      educationLevel: self.studentForm.educationLevel,
      subjectSelect: self.studentForm.subjectSelect || '',
      learningGoal: self.studentForm.learningGoal,
      photoUrl: self.studentForm.photoUrl || null
    };
    StudentService.create(payload).then(function (res) {
      self.students.push(res.data);
      self.newlyAddedStudentId = res.data.id;
      self.studentSuccess = true;
      self.studentForm = { name: '', birthDate: '', school: '', educationLevel: '', subjectSelect: '', learningGoal: '', photoUrl: '' };
      $timeout(function () { self.studentSuccess = false; }, 5000);
    });
  };

  // Pay an invoice
  self.payInvoice = function (invoiceId) {
    InvoiceService.pay(invoiceId).then(function () {
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
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.issueSuccess = false;
        self.issueForm.bookingId = null;
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
    return self.bookings.filter(function (b) { return b.studentId === studentId && b.date === dayStr; });
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

  self.acceptCounterProposal = function (booking) {
    BookingService.updateStatus(booking.id, 'confirmed', null)
      .then(function () {
        BookingService.getAll().then(function (res) { self.bookings = res.data; });
        InvoiceService.getAll().then(function (res) { self.invoices = res.data; });
      });
  };

  // Load chat messages on chat page
  if ($location.path() === '/parent/chat') {
    TutorService.getAll().then(function (res) {
      if (res.data.length) self.loadChat(res.data[0].id);
    });
  }
}]);
