'use strict';

angular.module('learnSphereApp')
.controller('AdminCtrl', ['$location', '$timeout', 'AuthService', 'AdminService',
function ($location, $timeout, AuthService, AdminService) {
  var self = this;
  self.user = AuthService.getCurrentUser();

  self.stats = null;
  self.unverifiedTutors = [];
  self.disputes = [];
  self.systemLogs = [];

  // Scoring config
  self.activeScoringTab = 'threshold';
  self.weightages = [
    { label: 'Tutor Rating', percent: 0 },
    { label: 'Tutor Activeness (Refresh Monthly)', percent: 0 },
    { label: 'Tutor Dispute (Refresh Monthly)', percent: 0 },
    { label: 'Tutor Experience', percent: 0 },
    { label: 'NA', percent: 0 },
    { label: 'NA', percent: 0 }
  ];
  self.ratingScale = [
    { range: '90% - 100%', points: 10 },
    { range: '80% - 90%', points: 9 },
    { range: '70% - 80%', points: 8 },
    { range: '60% - 70%', points: 7 },
    { range: '50% - 60%', points: 6 },
    { range: '40% - 50%', points: 5 },
    { range: '30% - 40%', points: 4 },
    { range: '20% - 30%', points: 3 },
    { range: '10% - 20%', points: 2 },
    { range: '0% - 10%', points: 1 }
  ];
  self.activenessScale = [
    { range: '> 15 classes', points: 5 },
    { range: '10 - 15 classes', points: 3 },
    { range: '5 - 10 classes', points: 1 },
    { range: '< 5 classes', points: 0 }
  ];
  self.disputesScale = [
    { range: '>= 2 disputes', points: -10 },
    { range: '1 dispute', points: -5 },
    { range: '0 disputes', points: 2 }
  ];
  self.experienceScale = [
    { range: '> 15 years', points: 5 },
    { range: '> 10 years', points: 4 },
    { range: '> 5 years', points: 3 },
    { range: '> 3 years', points: 2 },
    { range: '> 1 year', points: 1 }
  ];

  var savedWeightages = localStorage.getItem('ls_scoring_weightages');
  if (savedWeightages) {
    JSON.parse(savedWeightages).forEach(function (saved, i) {
      if (self.weightages[i]) self.weightages[i].percent = saved.percent;
    });
  }

  self.saveWeightages = function () {
    localStorage.setItem('ls_scoring_weightages', JSON.stringify(self.weightages));
    self.weightageSaveSuccess = true;
    $timeout(function () {
      self.weightageSaveSuccess = false;
    }, 2000);
  };

  function init() {
    AdminService.getStats().then(function (res) { self.stats = res.data; });
    AdminService.getUnverifiedTutors().then(function (res) { self.unverifiedTutors = res.data; });
    AdminService.getDisputes().then(function (res) { self.disputes = res.data; });
  }
  init();

  self.verifyTutor = function (tutor) {
    AdminService.verifyTutor(tutor.id).then(function () {
      tutor.isVerified = true;
      self.unverifiedTutors = self.unverifiedTutors.filter(function (t) { return t.id !== tutor.id; });
      self.systemLogs.unshift('Approved tutor: ' + tutor.name + ' (Just now)');
      AdminService.getStats().then(function (res) { self.stats = res.data; });
    });
  };

  self.resolveDispute = function (dispute) {
    AdminService.resolveDispute(dispute.id).then(function () {
      self.disputes = self.disputes.filter(function (d) { return d.id !== dispute.id; });
      self.systemLogs.unshift('Conflict resolved for class: #' + dispute.id + ' (Just now)');
    });
  };
}]);
