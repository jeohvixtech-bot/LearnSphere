'use strict';

angular.module('learnSphereApp')
.controller('TutorCtrl', ['$location', '$timeout', 'AuthService', 'TutorService',
  'BookingService', 'ChatService', 'InvoiceService',
function ($location, $timeout, AuthService, TutorService, BookingService, ChatService, InvoiceService) {
  var self = this;
  var user = AuthService.getCurrentUser();
  self.user = user;
  self.tutor = null;
  self.bookings = [];
  self.invoices = [];
  self.chatMessages = [];

  // Report forms
  self.reportBooking = null;
  self.reportForm = { covered: '', performance: '', homework: '' };
  self.reportSuccess = false;

  self.editBooking = null;
  self.editForm = { covered: '', performance: '', homework: '', changesMade: '' };
  self.editSuccess = false;

  self.counterBooking = null;
  self.counterForm = { date: '', time: '', message: '' };
  self.counterSuccess = false;

  self.chatText = '';
  self.selectedCalDay = new Date().getDate();

  function init() {
    TutorService.getByUser(user.userId).then(function (res) {
      self.tutor = res.data;
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
    return self.bookings.filter(function (b) { return b.tutorId === self.tutor.id && b.status === 'confirmed'; });
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

  self.bookingsOnDay = function (dayNum) {
    if (!self.tutor) return [];
    var now = new Date();
    var y = now.getFullYear();
    var m = String(now.getMonth() + 1).padStart(2, '0');
    var dayStr = y + '-' + m + '-' + (dayNum < 10 ? '0' : '') + dayNum;
    return self.bookings.filter(function (b) { return b.tutorId === self.tutor.id && b.date === dayStr; });
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
    self.counterForm = { date: '', time: '', message: '' };
    self.counterSuccess = false;
  };

  self.submitCounter = function () {
    if (!self.counterBooking) return;
    BookingService.updateStatus(self.counterBooking.id, 'countered', {
      date: self.counterForm.date,
      time: self.counterForm.time,
      message: self.counterForm.message
    }).then(function () {
      self.counterSuccess = true;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.counterSuccess = false;
        self.counterBooking = null;
      }, 2000);
    });
  };

  self.cancelCounter = function () { self.counterBooking = null; };

  self.startReport = function (booking) {
    self.reportBooking = booking;
    self.reportForm = { covered: '', performance: '', homework: '' };
    self.reportSuccess = false;
  };

  self.submitReport = function () {
    if (!self.reportBooking) return;
    BookingService.submitLessonReport(self.reportBooking.id, self.reportForm).then(function () {
      self.reportSuccess = true;
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.reportSuccess = false;
        self.reportBooking = null;
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
      BookingService.getAll().then(function (res) { self.bookings = res.data; });
      $timeout(function () {
        self.editSuccess = false;
        self.editBooking = null;
      }, 2000);
    });
  };

  self.cancelEdit = function () { self.editBooking = null; };

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
