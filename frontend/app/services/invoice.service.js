'use strict';

angular.module('learnSphereApp')
.service('InvoiceService', ['$http', 'API_URL', 'AuthService', function ($http, API_URL, AuthService) {
  var self = this;
  var h = function () { return { headers: AuthService.authHeader() }; };

  self.getAll = function () {
    return $http.get(API_URL + '/invoices', h());
  };

  self.pay = function (id) {
    return $http.post(API_URL + '/invoices/' + id + '/pay', {}, h());
  };
}]);
