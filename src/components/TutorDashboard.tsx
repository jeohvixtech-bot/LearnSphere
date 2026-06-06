import React, { useState } from 'react';
import { 
  Award, TrendingUp, Sparkles, Clipboard, Wallet, MessageSquare, Clock, Check, 
  X, HelpCircle, FileText, Send, Calendar, AlertCircle, Edit, ArrowRight
} from 'lucide-react';
import { Tutor, Student, Booking, ChatMessage, Payout, Invoice } from '../types';

interface TutorDashboardProps {
  tutor: Tutor;
  bookings: Booking[];
  onUpdateBookingStatus: (id: string, status: Booking['status'], extra?: any) => void;
  onSubmitLessonReport: (bookingId: string, covered: string, performance: string, homework: string) => void;
  onEditLessonReport: (bookingId: string, changesMade: string, revisedFields: { covered: string, performance: string, homework: string }) => void;
  chatMessages: ChatMessage[];
  onSendChatMessage: (text: string) => void;
  payouts: Payout[];
  invoices: Invoice[];
}

export const TutorDashboard: React.FC<TutorDashboardProps> = ({
  tutor,
  bookings,
  onUpdateBookingStatus,
  onSubmitLessonReport,
  onEditLessonReport,
  chatMessages,
  onSendChatMessage,
  payouts,
  invoices
}) => {
  const [activeTab, setActiveTab] = useState<string>('overview');

  // Submit report state variables
  const [reportBooking, setReportBooking] = useState<Booking | null>(null);
  const [reportCovered, setReportCovered] = useState('');
  const [reportPerformance, setReportPerformance] = useState('');
  const [reportHomework, setReportHomework] = useState('');
  const [reportSuccess, setReportSuccess] = useState(false);

  // Edit report state variables
  const [editBooking, setEditBooking] = useState<Booking | null>(null);
  const [editCovered, setEditCovered] = useState('');
  const [editPerformance, setEditPerformance] = useState('');
  const [editHomework, setEditHomework] = useState('');
  const [editChangesNotes, setEditChangesNotes] = useState('');
  const [editSuccess, setEditSuccess] = useState(false);

  // Counter proposal input details
  const [counterBooking, setCounterBooking] = useState<Booking | null>(null);
  const [proposedDate, setProposedDate] = useState('2026-06-12');
  const [proposedTime, setProposedTime] = useState('05:00 PM');
  const [proposedText, setProposedText] = useState('Hi Sarah, I would love to schedule a session, but can we adjust the time? Let me propose coordinates here:');
  const [counterSuccess, setCounterSuccess] = useState(false);

  // Chat input
  const [chatText, setChatText] = useState('');
  
  // High-fidelity active calendar view state (June 2026)
  const [selectedCalDay, setSelectedCalDay] = useState<number | null>(8);

  const tutorBookings = bookings.filter(b => b.tutorId === tutor.id);
  const pendingRequests = tutorBookings.filter(b => b.status === 'pending');
  const confirmedClasses = tutorBookings.filter(b => b.status === 'confirmed');
  const selectedDayDateString = selectedCalDay !== null ? `2026-06-${String(selectedCalDay).padStart(2, '0')}` : null;
  const confirmedClassesOnSelectedDay = confirmedClasses.filter(b => b.date === selectedDayDateString);
  const completedClasses = tutorBookings.filter(b => b.status === 'completed');

  const handlePublishClassReport = (e: React.FormEvent) => {
    e.preventDefault();
    if (!reportBooking) return;

    onSubmitLessonReport(reportBooking.id, reportCovered, reportPerformance, reportHomework);
    setReportSuccess(true);
    setTimeout(() => {
      setReportSuccess(false);
      setReportBooking(null);
      setActiveTab('overview');
    }, 2000);
  };

  const handleUpdatePublishedReport = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editBooking) return;

    onEditLessonReport(editBooking.id, editChangesNotes, {
      covered: editCovered,
      performance: editPerformance,
      homework: editHomework
    });
    setEditSuccess(true);
    setTimeout(() => {
      setEditSuccess(false);
      setEditBooking(null);
    }, 2000);
  };

  const handleSendCounterProposal = (e: React.FormEvent) => {
    e.preventDefault();
    if (!counterBooking) return;

    // Dispatch the counter proposed state
    onUpdateBookingStatus(counterBooking.id, 'countered', {
      date: proposedDate,
      time: proposedTime,
      message: proposedText
    });
    setCounterSuccess(true);
    setTimeout(() => {
      setCounterSuccess(false);
      setCounterBooking(null);
    }, 2000);
  };

  return (
    <div className="max-w-7xl mx-auto px-4 py-6">
      
      {/* Tops stats widget summary */}
      <div className="bg-indigo-950 text-white p-6 rounded-2xl mb-8 flex flex-col md:flex-row md:items-center justify-between gap-6">
        <div className="flex items-center space-x-4">
          <img src={tutor.imageUrl} alt={tutor.name} className="w-14 h-14 rounded-full border-2 border-indigo-400 object-cover" />
          <div>
            <div className="flex items-center gap-1.5 flex-wrap">
              <h2 className="font-display font-bold text-lg">{tutor.name}</h2>
              <span className="bg-amber-400 text-indigo-950 text-[9px] font-extrabold px-1.5 py-0.5 rounded flex items-center gap-0.5"><Award className="h-2.5 w-2.5" /> Gold Teacher</span>
            </div>
            <p className="text-xs text-indigo-200 mt-1">Syllabus Coordinator • {tutor.experienceYears} Years active MOE Teacher</p>
          </div>
        </div>

        {/* Highlight Stats Panels */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-center">
          <div className="bg-indigo-900/60 p-2.5 rounded-xl border border-indigo-800 shrink-0">
            <span className="text-[9px] text-indigo-200 font-semibold block uppercase">Completed Log</span>
            <strong className="text-sm font-bold text-emerald-400 block mt-1">{completedClasses.length} sessions</strong>
          </div>
          <div className="bg-indigo-900/60 p-2.5 rounded-xl border border-indigo-800 shrink-0">
            <span className="text-[9px] text-indigo-200 font-semibold block uppercase">Satisfaction</span>
            <strong className="text-sm font-bold text-amber-400 block mt-1">⭐ {tutor.rating} rating</strong>
          </div>
          <div className="bg-indigo-900/60 p-2.5 rounded-xl border border-indigo-800 shrink-0">
            <span className="text-[9px] text-indigo-200 font-semibold block uppercase">Completion Rate</span>
            <strong className="text-sm font-bold text-indigo-100 block mt-1">98.5%</strong>
          </div>
          <div className="bg-indigo-900/60 p-2.5 rounded-xl border border-indigo-800 shrink-0">
            <span className="text-[9px] text-indigo-200 font-semibold block uppercase">Ready Balance</span>
            <strong className="text-sm font-bold text-white block mt-1">$1,200.00</strong>
          </div>
        </div>
      </div>

      <div className="lg:grid lg:grid-cols-12 lg:gap-8">
        
        {/* Left Side Navigation Menu */}
        <nav className="lg:col-span-3 mb-6 lg:mb-0 bg-white border rounded-2xl p-4 shadow-xs space-y-1">
          {[
            { id: 'overview', name: 'Overview & Schedules', icon: Clipboard },
            { id: 'chat', name: 'Direct Messages Thread', icon: MessageSquare }
          ].map(tab => (
            <button
              key={tab.id}
              onClick={() => {
                setActiveTab(tab.id);
                setReportBooking(null);
                setEditBooking(null);
                setCounterBooking(null);
              }}
              className={`w-full text-left px-3 py-2.5 rounded-xl text-xs font-semibold flex items-center gap-2.5 transition cursor-pointer ${
                activeTab === tab.id 
                  ? 'bg-indigo-600 text-white shadow-xs' 
                  : 'text-gray-600 hover:bg-slate-50'
              }`}
            >
              <tab.icon className="h-4 w-4" />
              {tab.name}
            </button>
          ))}
          
          <div className="pt-4 border-t mt-4 text-[11px] text-gray-400 font-medium px-1">
            Status: <span className="text-emerald-600 font-bold font-mono">● LIVE ON DUTY</span>
          </div>
        </nav>

        {/* Dynamic Panel Context */}
        <main className="lg:col-span-9 space-y-6">

          {/* 1. Overview and Schedules Tab */}
          {activeTab === 'overview' && !reportBooking && !editBooking && !counterBooking && (
            <div className="space-y-6">
              
              {/* Parent requesting bookings pending tutor approval */}
              {pendingRequests.length > 0 && (
                <div className="bg-white border rounded-2xl p-5 shadow-xs">
                  <h3 className="font-display font-black text-gray-950 text-sm mb-4 flex items-center gap-1.5">
                    <span className="w-2.5 h-2.5 bg-amber-400 rounded-full shrink-0"></span> Parents Booking Requests ({pendingRequests.length})
                  </h3>
                  <div className="divide-y divide-gray-100">
                    {pendingRequests.map(booking => (
                      <div key={booking.id} className="py-3 flex flex-col md:flex-row md:items-center justify-between gap-4">
                        <div>
                          <span className="text-[9px] bg-indigo-50 text-indigo-700 font-bold px-1.5 py-0.5 rounded font-mono uppercase">{booking.subject}</span>
                          <h4 className="font-bold text-gray-800 text-xs mt-1.5">Sarah Tan</h4>
                          <p className="text-[10px] text-gray-500 mt-0.5">Date: {booking.date} • {booking.time} ({booking.durationHours} Hours Trial)</p>
                          {booking.message && <p className="text-[10px] text-gray-400 mt-1 italic">Message: "{booking.message}"</p>}
                        </div>

                        <div className="flex gap-2">
                          <button 
                            onClick={() => setCounterBooking(booking)}
                            className="bg-slate-50 border hover:bg-indigo-50 hover:text-indigo-600 text-gray-700 font-semibold text-[11px] px-3.5 py-1.5 rounded-lg transition"
                          >
                            Counter Propose Time
                          </button>
                          <button 
                            onClick={() => onUpdateBookingStatus(booking.id, 'confirmed')}
                            className="bg-emerald-600 hover:bg-emerald-700 text-white font-bold text-[11px] px-3.5 py-1.5 rounded-lg shadow-xs transition"
                          >
                            Confirm Class
                          </button>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Tutor Monthly Calendar View (June 2026) */}
              <div className="bg-white border rounded-2xl p-5 shadow-xs">
                <div className="flex flex-col sm:flex-row justify-between sm:items-center gap-3 pb-4 border-b border-gray-100">
                  <div>
                    <span className="text-[9px] bg-indigo-50 text-indigo-700 px-2 py-0.5 rounded font-extrabold uppercase font-mono tracking-wider">TUTOR MONTHLY SCHEDULER</span>
                    <h3 className="font-display font-black text-gray-950 text-sm mt-1">Calendar & Payout Coordinator</h3>
                  </div>
                  <div className="flex items-center gap-2 text-xs font-semibold text-gray-600">
                    <button className="p-1 px-2.5 rounded-lg border bg-slate-50 cursor-not-allowed opacity-50 text-[10px]" disabled>« May</button>
                    <span className="font-mono bg-indigo-50/60 text-indigo-950 px-3 py-1 rounded-md font-bold text-[11px]">📆 June 2026</span>
                    <button className="p-1 px-2.5 rounded-lg border bg-slate-50 cursor-not-allowed opacity-50 text-[10px]" disabled>July »</button>
                  </div>
                </div>

                <p className="text-[10px] text-gray-400 mt-2.5 mb-4 leading-relaxed">
                  Your teaching scheduler displays active class reservations. Open slots and pending sessions remain in gray/amber lock states. Slots automatically unlock and activate in <strong className="text-emerald-600">green confirmed</strong> status as soon as the corresponding parent payment has been successfully cleared on stripe.
                </p>

                {/* Visual Indicators for Pending Confirmation and Pending Classes to Go */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-5 mt-1">
                  <div className="bg-amber-50/50 border border-amber-100 p-3 rounded-xl flex items-center justify-between">
                    <div>
                      <span className="text-[8px] sm:text-[9px] text-amber-600 font-bold uppercase tracking-wider block font-mono">Pending Tutor Confirmation</span>
                      <strong className="text-sm font-black text-amber-950 font-mono">
                        {pendingRequests.length} Session{pendingRequests.length !== 1 ? 's' : ''}
                      </strong>
                    </div>
                    {pendingRequests.length > 0 ? (
                      <span className="bg-amber-200/60 text-amber-900 font-extrabold text-[9px] px-2 py-0.5 rounded-md animate-pulse">
                        ⏳ Action Required
                      </span>
                    ) : (
                      <span className="bg-gray-100 text-gray-400 font-medium text-[9px] px-2 py-0.5 rounded-md">
                        ✓ All Clear
                      </span>
                    )}
                  </div>

                  <div className="bg-indigo-50/50 border border-indigo-100 p-3 rounded-xl flex items-center justify-between animate-in fade-in duration-300">
                    <div>
                      <span className="text-[8px] sm:text-[9px] text-indigo-650 font-bold uppercase tracking-wider block font-mono">
                        Pending Classes to Go {selectedCalDay ? `(June ${selectedCalDay})` : ''}
                      </span>
                      <strong className="text-sm font-black text-indigo-950 font-mono">
                        {confirmedClassesOnSelectedDay.length} Lesson{confirmedClassesOnSelectedDay.length !== 1 ? 's' : ''}
                      </strong>
                    </div>
                    {confirmedClassesOnSelectedDay.length > 0 ? (
                      <span className="bg-indigo-100 text-indigo-800 font-extrabold text-[9px] px-2 py-0.5 rounded-md flex items-center gap-1 transition-all duration-300">
                        <span className="w-1.5 h-1.5 bg-indigo-500 rounded-full animate-ping" />
                        🚀 Scheduled
                      </span>
                    ) : (
                      <span className="bg-slate-100 text-slate-400 font-medium text-[9px] px-2 py-0.5 rounded-md transition-all duration-300">
                        No sessions today
                      </span>
                    )}
                  </div>
                </div>

                {/* Weekdays indicator bar */}
                <div className="grid grid-cols-7 gap-1 text-center font-mono font-bold text-[9px] text-gray-400 mb-2 border-b pb-1.5 border-slate-50">
                  <span>MON</span>
                  <span>TUE</span>
                  <span>WED</span>
                  <span>THU</span>
                  <span>FRI</span>
                  <span>SAT</span>
                  <span>SUN</span>
                </div>

                {/* 30 June Month Grid */}
                <div className="grid grid-cols-7 gap-1.5">
                  {Array.from({ length: 30 }, (_, i) => {
                    const dayNum = i + 1;
                    const dateString = `2026-06-${String(dayNum).padStart(2, '0')}`;
                    const dayBookings = tutorBookings.filter(b => b.date === dateString);
                    
                    // Check if there are paid or unpaid bookings
                    const paidBookings = dayBookings.filter(b => {
                      const inv = invoices.find(inv => inv.bookingId === b.id);
                      return inv && inv.status === 'Paid';
                    });
                    const unpaidBookings = dayBookings.filter(b => {
                      const inv = invoices.find(inv => inv.bookingId === b.id);
                      return !inv || inv.status !== 'Paid';
                    });

                    const hasPaid = paidBookings.length > 0;
                    const hasUnpaid = unpaidBookings.length > 0;
                    const isSelected = selectedCalDay === dayNum;

                    let bgClass = "bg-slate-50/40 hover:bg-slate-100 text-gray-800";
                    let borderClass = "border border-gray-100/70";

                    if (hasPaid) {
                      bgClass = "bg-emerald-50 text-emerald-900 font-bold hover:bg-emerald-100";
                      borderClass = "border-2 border-emerald-500/80";
                    } else if (hasUnpaid) {
                      bgClass = "bg-amber-50 text-amber-900 font-bold hover:bg-amber-100";
                      borderClass = "border-2 border-dashed border-amber-400/80";
                    }

                    if (isSelected) {
                      borderClass = "border-2 border-indigo-600 ring-2 ring-indigo-100";
                    }

                    return (
                      <button
                        key={dayNum}
                        onClick={() => setSelectedCalDay(dayNum)}
                        className={`min-h-[50px] p-1 rounded-xl transition-all relative flex flex-col justify-between text-left cursor-pointer ${bgClass} ${borderClass}`}
                      >
                        <div className="flex justify-between items-center w-full">
                          <span className="text-[11px] font-bold font-mono pl-0.5">{dayNum}</span>
                          <div className="flex gap-0.5 pr-0.5">
                            {hasPaid && <span className="w-1.5 h-1.5 bg-emerald-500 rounded-full" />}
                            {hasUnpaid && <span className="w-1.5 h-1.5 bg-amber-500 rounded-full animate-pulse" />}
                          </div>
                        </div>

                        {/* Cell dynamic text summary if any */}
                        {dayBookings.length > 0 && (
                          <div className="text-[7.5px] tracking-tight truncate w-full font-sans uppercase mt-1 leading-none font-medium opacity-90 max-w-full">
                            {hasPaid ? (
                              <span className="text-emerald-700 font-extrabold flex items-center gap-0.5">🟢 ACTIVE</span>
                            ) : (
                              <span className="text-amber-700 font-extrabold flex items-center gap-0.5">🔒 LOCKED</span>
                            )}
                          </div>
                        )}
                      </button>
                    );
                  })}
                </div>

                {/* Selected Day Agenda Drawer Details */}
                {selectedCalDay !== null && (() => {
                  const dayString = `2026-06-${String(selectedCalDay).padStart(2, '0')}`;
                  const dayBookings = tutorBookings.filter(b => b.date === dayString);

                  return (
                    <div className="mt-5 bg-slate-50 border border-slate-100 rounded-2xl p-4">
                      <div className="flex justify-between items-center mb-3">
                        <h4 className="text-xs font-bold text-gray-900 flex items-center gap-1.5 uppercase font-mono tracking-wide">
                          📅 Scheduled Classes for June {selectedCalDay}, 2026
                        </h4>
                        <span className="text-[9px] bg-slate-200 text-slate-800 px-2 py-0.5 rounded font-mono font-bold">
                          {dayBookings.length} lesson(s)
                        </span>
                      </div>

                      {dayBookings.length === 0 ? (
                        <p className="text-[11px] text-gray-400 py-2 italic text-center">
                          No scheduled lessons with tuition parents on this day. Click dates above to browse active agenda.
                        </p>
                      ) : (
                        <div className="space-y-3">
                          {dayBookings.map(b => {
                            const invoice = invoices.find(inv => inv.bookingId === b.id);
                            const isPaid = invoice && invoice.status === 'Paid';

                            return (
                              <div key={b.id} className="bg-white border rounded-xl p-3 shadow-2xs divide-y divide-slate-100 space-y-2">
                                <div className="flex justify-between items-start pb-2">
                                  <div>
                                    <span className="bg-indigo-50 text-indigo-700 text-[8px] font-extrabold font-mono px-1.5 py-0.5 rounded uppercase">
                                      {b.subject}
                                    </span>
                                    <h5 className="font-bold text-gray-900 text-xs mt-1">Student: Jessica Tan</h5>
                                    <p className="text-[10px] text-gray-500 mt-0.5">Time duration: {b.time} ({b.durationHours} Hours)</p>
                                  </div>
                                  <div className="text-right">
                                    <span className={`text-[9px] px-2 py-0.5 rounded font-extrabold uppercase tracking-wide inline-block ${
                                      isPaid 
                                        ? 'bg-emerald-100 text-emerald-800 border border-emerald-200' 
                                        : 'bg-amber-100 text-amber-800 border border-amber-200 animate-pulse'
                                    }`}>
                                      {isPaid ? '🟢 Verified Paid' : '🔒 Locked / Unpaid'}
                                    </span>
                                    <strong className="block text-xs font-bold text-gray-900 mt-1">${b.totalPrice}.00</strong>
                                  </div>
                                </div>
                                <div className="pt-2 text-[10.5px] text-slate-500 leading-relaxed">
                                  {isPaid ? (
                                    <p className="text-emerald-700 font-medium">
                                      ✓ Stripe transaction verified successfully. Tutorial curriculum slides, practices sheets, and classroom whiteboard access are completely <strong>unlocked</strong> for both parent and teacher.
                                    </p>
                                  ) : (
                                    <p className="text-amber-700 font-medium">
                                      ⏳ Payment request issued via Invoice <strong className="font-mono">#{invoice?.id || 'pending'}</strong>. Teach workspace and live Whiteboard will unlock as soon as parent initiates checkout.
                                    </p>
                                  )}
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  );
                })()}

              </div>

              {/* Class schedules list */}
              <div className="bg-white border rounded-2xl p-5 shadow-xs">
                <h3 className="font-display font-medium text-gray-900 text-sm mb-4">Confirmed Classes Checklist</h3>
                {confirmedClasses.length === 0 ? (
                  <p className="text-xs text-gray-500 py-4 text-center">No active class appointments on agenda.</p>
                ) : (
                  <div className="space-y-3">
                    {confirmedClasses.map(booking => (
                      <div key={booking.id} className="p-3 border rounded-xl flex items-center justify-between bg-slate-50/50">
                        <div>
                          <span className="text-[9px] bg-emerald-100 text-indigo-800 px-2 py-0.5 rounded font-bold uppercase">{booking.subject}</span>
                          <h4 className="font-bold text-gray-900 text-xs mt-1">Student: Jessica Tan</h4>
                          <p className="text-[10px] text-gray-500 mt-0.5 flex items-center">
                            <Clock className="h-3 w-3 mr-1" /> Tomorrow, {booking.time} • Duration: {booking.durationHours}H
                          </p>
                        </div>
                        <span className="text-[9px] bg-emerald-50 text-emerald-700 border border-emerald-100 rounded px-2 py-0.5 font-bold uppercase">STARTED_SOON</span>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Submitting Lesson Reports history */}
              <div className="bg-white border rounded-2xl p-5 shadow-xs">
                <h3 className="font-display font-medium text-gray-900 text-sm mb-4">Class Reports Portal</h3>
                
                <div className="space-y-4">
                  
                  {/* Needs reporting sessions */}
                  {completedClasses.filter(b => !b.lessonReport).map(booking => (
                    <div key={booking.id} className="p-3 bg-indigo-50/20 border border-indigo-100/50 rounded-xl flex items-center justify-between">
                      <div>
                        <span className="text-[9px] bg-amber-100 text-amber-800 font-bold px-1.5 py-0.5 rounded">REPORTS_REQUIRED</span>
                        <h4 className="font-bold text-xs text-slate-800 mt-1">Math Sec 3 on {booking.date}</h4>
                        <p className="text-[10px] text-gray-400">Student Jessica Tan</p>
                      </div>
                      <button 
                        onClick={() => {
                          setReportBooking(booking);
                          setReportCovered('');
                          setReportPerformance('');
                          setReportHomework('');
                        }}
                        className="bg-indigo-600 hover:bg-indigo-700 text-white font-bold text-[10.5px] px-3.5 py-1.5 rounded-lg shadow-xs"
                      >
                        Submit Lesson Report
                      </button>
                    </div>
                  ))}

                  {/* Submitted Reports editable log */}
                  <div>
                    <h4 className="font-bold text-[9px] text-gray-400 uppercase tracking-wider mb-2">Published Reports Log</h4>
                    {completedClasses.filter(b => b.lessonReport).length === 0 ? (
                      <p className="text-[10.5px] text-gray-400">No reports published yet.</p>
                    ) : (
                      <div className="divide-y divide-gray-100 border rounded-xl overflow-hidden shadow-xs bg-white">
                        {completedClasses.filter(b => b.lessonReport).map(booking => (
                          <div key={booking.id} className="p-3 flex items-center justify-between bg-white text-xs">
                            <div>
                              <p className="font-semibold text-gray-800">{booking.subject} Report</p>
                              <span className="text-[9.5px] text-gray-400 font-mono">Published {booking.lessonReport?.submitDate}</span>
                            </div>
                            <button 
                              onClick={() => {
                                setEditBooking(booking);
                                setEditCovered(booking.lessonReport?.covered || '');
                                setEditPerformance(booking.lessonReport?.performance || '');
                                setEditHomework(booking.lessonReport?.homework || '');
                                setEditChangesNotes('');
                              }}
                              className="text-indigo-600 hover:text-indigo-800 font-bold hover:underline flex items-center gap-0.5 text-[10.5px] cursor-pointer"
                            >
                              <Edit className="h-3.5 w-3.5" /> Edit Report
                            </button>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>

                </div>
              </div>

            </div>
          )}

          {/* Submit lesson report form overlay */}
          {activeTab === 'overview' && reportBooking && (
            <div className="bg-white border rounded-2xl p-6 shadow-xs max-w-xl">
              <button onClick={() => setReportBooking(null)} className="text-xs text-indigo-600 hover:underline mb-4 font-bold flex items-center gap-0.5">
                ← Cancel Report Compilation
              </button>

              {reportSuccess ? (
                <div className="p-6 bg-emerald-50 border border-emerald-100 text-emerald-800 rounded-xl text-center">
                  <Check className="h-10 w-10 text-emerald-600 mx-auto mb-3 border p-1 rounded-full shrink-0" />
                  <p className="text-xs font-bold leading-normal">Lesson Report has been successfully shared with Parent Tan J.</p>
                </div>
              ) : (
                <form onSubmit={handlePublishClassReport} className="space-y-4 font-sans">
                  <span className="text-[9px] bg-slate-100 text-slate-700 px-2 py-0.5 rounded font-mono font-bold uppercase">Lesson evaluation form</span>
                  <h2 className="font-display font-black text-slate-900 text-base leading-snug">Compile Report for Jessica Tan</h2>
                  <p className="text-xs text-gray-400 mt-1">Class: Math - Secondary 3 on {reportBooking.date}</p>

                  <div>
                    <label className="text-[10px] text-gray-500 font-semibold block">Syllabus Covered & Focus Areas</label>
                    <textarea 
                      required
                      placeholder="e.g. Solving simultaneous quadratic equations through substitution and formulas modules..."
                      value={reportCovered}
                      onChange={(e) => setReportCovered(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg text-xs py-2 px-3 mt-1 h-20"
                    />
                  </div>

                  <div>
                    <label className="text-[10px] text-gray-500 font-semibold block">Student's alignment & classroom performance</label>
                    <textarea 
                      required
                      placeholder="e.g. Excellent focus. Solved algebraic tasks correctly, but needs minor supervision on sign conversions..."
                      value={reportPerformance}
                      onChange={(e) => setReportPerformance(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg text-xs py-2 px-3 mt-1 h-20"
                    />
                  </div>

                  <div>
                    <label className="text-[10px] text-gray-500 font-semibold block">Dedicated Homework worksheet instructions</label>
                    <input 
                      type="text" 
                      required
                      placeholder="e.g. Worksheet 4 (Q1-10) quadratic simultaneous. Due next Friday."
                      value={reportHomework}
                      onChange={(e) => setReportHomework(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg text-xs py-2 px-3 mt-1"
                    />
                  </div>

                  <button type="submit" className="w-full bg-indigo-600 hover:bg-indigo-700 text-white font-bold py-2.5 rounded-xl text-xs transition cursor-pointer">
                    Publish lesson Report
                  </button>
                </form>
              )}
            </div>
          )}

          {/* Edit submitted report form overlay */}
          {activeTab === 'overview' && editBooking && editBooking.lessonReport && (
            <div className="bg-white border rounded-2xl p-6 shadow-xs max-w-xl">
              <button onClick={() => setEditBooking(null)} className="text-xs text-indigo-600 hover:underline mb-4 font-bold flex items-center gap-0.5">
                ← Dismiss modifications
              </button>

              {editSuccess ? (
                <div className="p-6 bg-emerald-50 border border-emerald-100 text-emerald-800 rounded-xl text-center">
                  <Check className="h-10 w-10 text-emerald-600 mx-auto mb-3" />
                  <p className="text-xs font-bold">Report modifications have been merged in parent feed successfully.</p>
                </div>
              ) : (
                <form onSubmit={handleUpdatePublishedReport} className="space-y-4 font-sans">
                  <span className="text-[9px] bg-yellow-50 text-yellow-700 border border-yellow-200 px-2.5 py-0.5 rounded font-mono font-bold uppercase">Incident Audit Logs active</span>
                  <h2 className="font-display font-black text-slate-900 text-base">Modify Published Report</h2>
                  <p className="text-xs text-gray-400 mt-1">Class: Math - Secondary 3 on {editBooking.date}</p>

                  <div>
                    <label className="text-[10px] text-gray-500 font-semibold block">Syllabus Covered</label>
                    <textarea 
                      required
                      value={editCovered}
                      onChange={(e) => setEditCovered(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg text-xs py-2 px-3 mt-1 h-16"
                    />
                  </div>

                  <div>
                    <label className="text-[10px] text-gray-500 font-semibold block">Student's alignment</label>
                    <textarea 
                      required
                      value={editPerformance}
                      onChange={(e) => setEditPerformance(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg text-xs py-2 px-3 mt-1 h-16"
                    />
                  </div>

                  <div>
                    <label className="text-[10px] text-gray-500 font-semibold block">Assigned homework guidelines</label>
                    <input 
                      type="text" 
                      required
                      value={editHomework}
                      onChange={(e) => setEditHomework(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg text-xs py-2 px-3 mt-1"
                    />
                  </div>

                  <div className="border-t border-dashed pt-4 mt-2">
                    <label className="text-[10px] text-amber-700 font-bold block bg-amber-50 px-2 py-1 rounded inline-block">Reason for report modification (Will be logged public)</label>
                    <input 
                      type="text" 
                      required
                      placeholder="e.g. Corrected typo in algebra worksheet instruction reference."
                      value={editChangesNotes}
                      onChange={(e) => setEditChangesNotes(e.target.value)}
                      className="w-full bg-yellow-50/50 border border-yellow-200 rounded-lg text-xs py-2 px-3 mt-2 focus:bg-white"
                    />
                  </div>

                  <button type="submit" className="w-full bg-amber-600 hover:bg-amber-700 text-white font-bold py-2.5 rounded-xl text-xs transition cursor-pointer">
                    Commit Updates to Audit Ledger
                  </button>
                </form>
              )}
            </div>
          )}

          {/* Counter proposal adjustment form overlay */}
          {activeTab === 'overview' && counterBooking && (
            <div className="bg-white border rounded-2xl p-6 shadow-xs max-w-md font-sans">
              <button onClick={() => setCounterBooking(null)} className="text-xs text-indigo-600 hover:underline mb-4 font-bold flex items-center gap-0.5">
                ← Cancel adjustment
              </button>

              {counterSuccess ? (
                <div className="p-6 bg-emerald-50 border border-emerald-100 text-emerald-850 rounded-xl text-center">
                  <Check className="h-8 w-8 text-emerald-600 mx-auto mb-2" />
                  <p className="text-xs font-bold leading-normal">Your counter-time proposal has been dispatched. Waiting for parental approval.</p>
                </div>
              ) : (
                <form onSubmit={handleSendCounterProposal} className="space-y-4">
                  <span className="text-[9px] bg-slate-100 text-slate-700 px-2 py-0.5 rounded font-mono font-bold uppercase">Time rescheduling assistant</span>
                  <h3 className="font-display font-black text-gray-900 text-base leading-tight">Counter Propose Booking Slot</h3>
                  <p className="text-[11px] text-gray-400 mt-0.5">Proposed alternative parameters for: {counterBooking.subject}</p>

                  <div className="grid grid-cols-2 gap-2">
                    <div>
                      <span className="text-[10px] text-gray-400 block font-semibold">New Proposed Date</span>
                      <input 
                        type="date" 
                        required
                        value={proposedDate}
                        onChange={(e) => setProposedDate(e.target.value)}
                        className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" 
                      />
                    </div>
                    <div>
                      <span className="text-[10px] text-gray-400 block font-semibold">New Proposed Start Time</span>
                      <input 
                        type="text" 
                        required
                        value={proposedTime}
                        onChange={(e) => setProposedTime(e.target.value)}
                        className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1" 
                      />
                    </div>
                  </div>

                  <div>
                    <span className="text-[10px] text-gray-400 block font-semibold">Rescheduling explanation to Parent Sarah</span>
                    <textarea 
                      required
                      value={proposedText}
                      onChange={(e) => setProposedText(e.target.value)}
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg text-xs py-2 px-3 mt-1 h-14"
                    />
                  </div>

                  <button type="submit" className="w-full bg-indigo-600 hover:bg-indigo-700 text-white font-bold py-2 rounded-lg text-xs transition cursor-pointer">
                    Dispatch Counter Proposal
                  </button>
                </form>
              )}
            </div>
          )}


          {/* 3. Messages Chat Thread with Sarah Tan */}
          {activeTab === 'chat' && (
            <div className="bg-white border rounded-2xl overflow-hidden h-[500px] flex flex-col shadow-xs">
              <div className="bg-slate-50 p-4 border-b flex items-center justify-between">
                <div>
                  <h4 className="font-bold text-gray-950 text-xs">Sarah Tan</h4>
                  <p className="text-[10px] text-emerald-600 flex items-center mt-0.5"><span className="w-1.5 align-middle h-1.5 bg-emerald-500 rounded-full mr-1"></span> Mother of Jessica Tan • Online</p>
                </div>
                <span className="text-[10px] text-gray-400">Direct Parent-Teacher line</span>
              </div>

              {/* Chat Threads */}
              <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-slate-50/50">
                {chatMessages.map(msg => (
                  <div key={msg.id} className={`flex flex-col ${msg.sender === 'tutor' ? 'items-end' : msg.sender === 'system' ? 'items-center' : 'items-start'}`}>
                    {msg.sender === 'system' ? (
                      <span className="text-[9.5px] bg-indigo-50 text-indigo-700 leading-snug border border-indigo-100 bg-linear-to-b px-2.5 py-1 rounded-lg text-center font-mono my-2 font-semibold">⚠️ LearnSphere Operations: {msg.text}</span>
                    ) : (
                      <div className={`p-3 rounded-2xl max-w-[75%] shadow-xs ${
                        msg.sender === 'tutor' ? 'bg-indigo-600 text-white rounded-tr-none' : 'bg-white text-gray-800 rounded-tl-none border'
                      }`}>
                        <p className="text-[11.5px] leading-relaxed pr-1">{msg.text}</p>
                        <span className="text-[8px] opacity-75 block text-right mt-1.5 font-mono">{msg.timestamp.split(' ')[1]} {msg.timestamp.split(' ')[2]}</span>
                      </div>
                    )}
                  </div>
                ))}
              </div>

              {/* Send Area */}
              <form onSubmit={(e) => {
                e.preventDefault();
                if (!chatText.trim()) return;
                onSendChatMessage(chatText);
                setChatText('');
              }} className="p-3 border-t bg-white flex gap-2">
                <input 
                  type="text" 
                  value={chatText}
                  onChange={(e) => setChatText(e.target.value)}
                  placeholder="Draft parent response instructions..." 
                  className="flex-1 bg-slate-50 border border-gray-200 rounded-xl px-4 text-xs focus:outline-none focus:ring-1 focus:ring-indigo-600 focus:bg-white" 
                />
                <button type="submit" className="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl px-4 py-2 text-xs font-semibold shrink-0 cursor-pointer flex items-center gap-1">
                  <Send className="h-3 w-3" /> Reply
                </button>
              </form>
            </div>
          )}

        </main>

      </div>
    </div>
  );
};
