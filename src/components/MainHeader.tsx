import React from 'react';
import { BookOpen, User, Bell, Shield, Award, Users, RefreshCw } from 'lucide-react';

interface MainHeaderProps {
  currentMode: 'interactive' | 'blueprint';
  onChangeMode: (mode: 'interactive' | 'blueprint') => void;
  userRole: 'parent' | 'tutor' | 'admin';
  onChangeRole: (role: 'parent' | 'tutor' | 'admin') => void;
  notificationsCount: number;
  onOpenNotifications: () => void;
}

export const MainHeader: React.FC<MainHeaderProps> = ({
  currentMode,
  onChangeMode,
  userRole,
  onChangeRole,
  notificationsCount,
  onOpenNotifications
}) => {
  return (
    <header className="sticky top-0 z-50 bg-white border-b border-gray-100 shadow-xs px-4 py-3">
      <div className="max-w-7xl mx-auto flex flex-col md:flex-row md:items-center md:justify-between gap-4">
        {/* Brand Logo */}
        <div className="flex items-center space-x-3">
          <div className="bg-blue-600 text-white p-2 rounded-xl flex items-center justify-center">
            <BookOpen className="h-6 w-6" id="logo-icon" />
          </div>
          <div>
            <h1 className="font-display text-xl font-bold tracking-tight text-gray-900 flex items-center gap-1">
              LearnSphere <span className="text-xs bg-blue-50 text-blue-600 font-sans px-2 py-0.5 rounded-full border border-blue-100 font-medium">Demo Workspace</span>
            </h1>
            <p className="text-xs text-gray-500 font-sans">Learn Continuously, Grow Confidently.</p>
          </div>
        </div>

        {/* Workspace Mode Switcher */}
        <div className="flex bg-slate-100 p-1 rounded-xl items-center self-start md:self-auto">
          <button
            id="mode-btn-interactive"
            onClick={() => onChangeMode('interactive')}
            className={`px-4 py-1.5 rounded-lg text-xs font-medium transition-all flex items-center gap-1 ${
              currentMode === 'interactive'
                ? 'bg-white text-blue-600 shadow-xs font-semibold'
                : 'text-gray-600 hover:text-gray-900'
            }`}
          >
            <RefreshCw className="h-3 w-3" />
            Live Marketplace Workflow
          </button>
          <button
            id="mode-btn-blueprint"
            onClick={() => onChangeMode('blueprint')}
            className={`px-4 py-1.5 rounded-lg text-xs font-medium transition-all flex items-center gap-1 ${
              currentMode === 'blueprint'
                ? 'bg-white text-blue-600 shadow-xs font-semibold'
                : 'text-gray-600 hover:text-gray-900'
            }`}
          >
            📋 Blueprint Catalog (27 Modules)
          </button>
        </div>

        {/* Roles controls */}
        <div className="flex items-center space-x-3 self-end md:self-auto">
          {currentMode === 'interactive' && (
            <div className="flex items-center space-x-1.5 border-r border-gray-200 pr-3 mr-1">
              <span className="text-xs text-gray-500 font-sans hidden sm:inline">Act As:</span>
              <select
                id="role-dropdown-switcher"
                value={userRole}
                onChange={(e) => onChangeRole(e.target.value as any)}
                className="bg-gray-50 border border-gray-200 text-gray-700 rounded-lg text-xs py-1.5 px-2 font-medium focus:outline-none focus:ring-1 focus:ring-blue-500 focus:border-blue-500 cursor-pointer"
              >
                <option value="parent">👤 Parent Profile (Sarah)</option>
                <option value="tutor">👨‍🏫 Tutor Profile (Mr. Lim)</option>
                <option value="admin">🛡️ Admin Dashboard</option>
              </select>
            </div>
          )}

          {/* Notifications Trigger */}
          <button
            id="notif-bell-trigger"
            onClick={onOpenNotifications}
            className="p-1.5 hover:bg-slate-50 text-gray-600 hover:text-blue-600 duration-200 rounded-lg relative cursor-pointer"
          >
            <Bell className="h-5 w-5" />
            {notificationsCount > 0 && (
              <span className="absolute top-1 right-1 bg-red-500 text-white text-[10px] w-4 h-4 rounded-full flex items-center justify-center font-bold">
                {notificationsCount}
              </span>
            )}
          </button>

          {/* Simulation status */}
          <div className="flex items-center space-x-2 text-xs bg-emerald-50 text-emerald-700 px-3 py-1.5 rounded-lg border border-emerald-100 font-mono">
            <span className="w-2 h-2 bg-emerald-500 rounded-full animate-pulse"></span>
            <span>SYSTEM ACTIVE</span>
          </div>
        </div>
      </div>
    </header>
  );
};
