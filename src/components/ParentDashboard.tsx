import React, { useState, useRef } from 'react';
import { 
  Users, Search, Calendar, CreditCard, MessageSquare, Plus, Star, BookOpen, 
  Clock, MapPin, CheckCircle, AlertTriangle, ShieldCheck, FileText, ChevronRight, Send, ArrowRight,
  ChevronDown, Building, Globe, Lock, Upload, Camera
} from 'lucide-react';
import { Tutor, Student, Booking, ChatMessage, Invoice } from '../types';
import { INSTITUTIONS, Institution } from '../data/institutions';

interface ParentDashboardProps {
  students: Student[];
  onAddStudent: (student: Student) => void;
  tutors: Tutor[];
  bookings: Booking[];
  onAddBooking: (booking: Booking) => void;
  onUpdateBookingStatus: (id: string, status: Booking['status'], extra?: any) => void;
  onReportIssue: (bookingId: string, issueType: string, details: string) => void;
  chatMessages: ChatMessage[];
  onSendChatMessage: (text: string) => void;
  invoices: Invoice[];
  onPayInvoice: (id: string) => void;
  initialActiveSection?: string;
}

export const ParentDashboard: React.FC<ParentDashboardProps> = ({
  students,
  onAddStudent,
  tutors,
  bookings,
  onAddBooking,
  onUpdateBookingStatus,
  onReportIssue,
  chatMessages,
  onSendChatMessage,
  invoices,
  onPayInvoice,
  initialActiveSection = 'dashboard'
}) => {
  const [activeTab, setActiveTab] = useState<string>(initialActiveSection);
  
  // Find Tutors search/filter states
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedSubject, setSelectedSubject] = useState('All');
  const [selectedMode, setSelectedMode] = useState('All');
  const [selectedTutor, setSelectedTutor] = useState<Tutor | null>(null);
  
  // Helper to add 1 hour for default end time
  const addOneHour = (timeStr: string): string => {
    try {
      const match = timeStr.trim().match(/^(\d+):(\d+)\s*(AM|PM)$/i);
      if (!match) return '05:00 PM';
      let hours = parseInt(match[1], 10);
      const minutes = match[2];
      const ampm = match[3].toUpperCase();
      hours += 1;
      let newAmpm = ampm;
      if (hours === 12) {
        newAmpm = ampm === 'AM' ? 'PM' : 'AM';
      } else if (hours > 12) {
        hours = hours - 12;
      }
      return `${String(hours).padStart(2, '0')}:${minutes} ${newAmpm}`;
    } catch (e) {
      return '05:00 PM';
    }
  };

  const handlePhotoUpload = (file: File) => {
    if (!file.type.startsWith('image/')) return;
    const reader = new FileReader();
    reader.onload = (e) => {
      if (e.target?.result) {
        setNewChildPhoto(e.target.result as string);
      }
    };
    reader.readAsDataURL(file);
  };

  const renderStudentAvatar = (student: Student, sizeClass: string = "w-10 h-10 text-xs") => {
    if (student.photoUrl) {
      return (
        <img 
          src={student.photoUrl} 
          alt={student.name} 
          className={`${sizeClass} rounded-full object-cover border border-slate-200 shadow-2xs shrink-0`} 
        />
      );
    }

    const getInitials = (n: string) => {
      if (!n) return "?";
      const parts = n.trim().split(/\s+/);
      if (parts.length > 1) {
        return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
      }
      return parts[0].substring(0, 2).toUpperCase();
    };

    const colors = [
      'bg-indigo-50 text-indigo-700 border-indigo-200/60',
      'bg-emerald-50 text-emerald-700 border-emerald-200/60',
      'bg-blue-50 text-blue-700 border-blue-200/60',
      'bg-amber-50 text-amber-700 border-amber-200/60',
      'bg-rose-50 text-rose-700 border-rose-200/60',
      'bg-violet-50 text-violet-700 border-violet-200/60',
    ];
    let sum = 0;
    for (let i = 0; i < student.name.length; i++) {
      sum += student.name.charCodeAt(i);
    }
    const colorClass = colors[sum % colors.length];

    return (
      <div className={`${sizeClass} rounded-full border ${colorClass} flex items-center justify-center font-bold tracking-tight font-mono shadow-2xs shrink-0`}>
        {getInitials(student.name)}
      </div>
    );
  };

  const renderStudentCalendar = (student: Student) => {
    const studentBookings = bookings.filter(b => b.studentId === student.id);
    const isExpanded = expandedCalStudentId === student.id;
    
    // Find first booked day or default to June 8, 2026
    const firstBookedDay = studentBookings.length > 0 
      ? parseInt(studentBookings[0].date.split('-')[2], 10) 
      : 8;
    const activeSelectedDay = selectedStudentCalDay[student.id] !== undefined 
      ? selectedStudentCalDay[student.id] 
      : firstBookedDay;

    const toggleCalendar = () => {
      setExpandedCalStudentId(isExpanded ? null : student.id);
    };

    return (
      <div className="mt-3 border-t border-slate-100 pt-3">
        <button
          onClick={toggleCalendar}
          className={`flex items-center justify-between w-full px-3 py-1.5 rounded-lg text-[11px] font-bold transition duration-200 uppercase font-mono tracking-wider ${
            isExpanded 
              ? 'bg-indigo-50 text-indigo-700 border border-indigo-100' 
              : 'bg-slate-50 text-slate-700 border border-slate-100 hover:bg-slate-100/60'
          }`}
        >
          <span className="flex items-center gap-1.5">
            <Calendar className="h-3.5 w-3.5" />
            June 2026 Study Calendar 
            {studentBookings.length > 0 && (
              <span className="bg-slate-200 text-slate-800 px-1.5 py-0.2 rounded text-[9px] font-bold">
                {studentBookings.length} Class{studentBookings.length !== 1 ? 'es' : ''}
              </span>
            )}
          </span>
          <span className="text-[10px] font-bold">
            {isExpanded ? 'Collapse ▲' : 'Expand Calendar ▼'}
          </span>
        </button>

        {isExpanded && (
          <div className="mt-3 bg-white border border-slate-100 rounded-xl p-3.5 space-y-3 shadow-xs animate-in fade-in duration-200">
            <div className="flex flex-col sm:flex-row justify-between sm:items-center gap-2 pb-2.5 border-b border-slate-50">
              <div>
                <span className="text-[8px] bg-indigo-50 text-indigo-700 px-1.5 py-0.2 rounded font-extrabold uppercase font-mono tracking-wider">MONTHLY STUDY TRACKER</span>
                <h4 className="font-bold text-gray-900 text-[11px] mt-0.5">Lesson & Schedule status</h4>
              </div>
              <div className="font-mono bg-indigo-50/60 text-indigo-950 px-2 py-0.5 rounded text-[10px] font-bold self-start sm:self-auto">
                📆 June 2026
              </div>
            </div>

            <p className="text-[9.5px] text-gray-450 leading-relaxed font-sans">
              Color status code is mapped to parent checkout: <strong className="text-emerald-600 font-semibold">Emerald confirmed & paid</strong> classes vs <strong className="text-amber-600 font-semibold">Amber awaiting checkout</strong>. Paid bookings are instantly updated on payment.
            </p>

            {/* Weekdays bar */}
            <div className="grid grid-cols-7 gap-1 text-center font-mono font-bold text-[8.5px] text-gray-400 pb-1.5 border-b border-slate-50">
              <span>MON</span>
              <span>TUE</span>
              <span>WED</span>
              <span>THU</span>
              <span>FRI</span>
              <span>SAT</span>
              <span>SUN</span>
            </div>

            {/* 30 Days June Grid */}
            <div className="grid grid-cols-7 gap-1">
              {Array.from({ length: 30 }, (_, i) => {
                const dayNum = i + 1;
                const dateString = `2026-06-${String(dayNum).padStart(2, '0')}`;
                const dayBookings = studentBookings.filter(b => b.date === dateString);
                
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
                const isSelected = activeSelectedDay === dayNum;

                let bgClass = "bg-slate-50 hover:bg-slate-100 text-gray-700";
                let borderClass = "border border-slate-100";

                if (hasPaid) {
                  bgClass = "bg-emerald-50 text-emerald-900 font-bold hover:bg-emerald-100/60";
                  borderClass = "border-2 border-emerald-500";
                } else if (hasUnpaid) {
                  bgClass = "bg-amber-50 text-amber-950 font-bold hover:bg-amber-100/60 animate-pulse";
                  borderClass = "border border-dashed border-amber-400";
                }

                if (isSelected) {
                  borderClass = "border-2 border-indigo-650 ring-2 ring-indigo-50/50";
                }

                return (
                  <button
                    key={dayNum}
                    onClick={() => setSelectedStudentCalDay(prev => ({ ...prev, [student.id]: dayNum }))}
                    className={`min-h-[38px] p-1 rounded-lg transition-all relative flex flex-col justify-between text-left cursor-pointer ${bgClass} ${borderClass}`}
                  >
                    <div className="flex justify-between items-center w-full">
                      <span className="text-[10px] font-bold font-mono pl-0.5">{dayNum}</span>
                      <div className="flex gap-0.5">
                        {hasPaid && <span className="w-1.5 h-1.5 bg-emerald-500 rounded-full" />}
                        {hasUnpaid && <span className="w-1.5 h-1.5 bg-amber-500 rounded-full" />}
                      </div>
                    </div>
                  </button>
                );
              })}
            </div>

            {/* Selected Day details section */}
            {activeSelectedDay !== null && (() => {
              const dayString = `2026-06-${String(activeSelectedDay).padStart(2, '0')}`;
              const dayBookings = studentBookings.filter(b => b.date === dayString);

              return (
                <div className="bg-slate-50 border border-slate-100/60 rounded-xl p-3">
                  <div className="flex justify-between items-center mb-2 pb-1.5 border-b border-slate-100/40">
                    <h5 className="text-[10px] font-bold text-gray-950 uppercase font-mono tracking-wide flex items-center gap-1">
                      📅 Day June {activeSelectedDay} Outline
                    </h5>
                    <span className="text-[8px] bg-slate-200 text-slate-800 font-bold px-1.5 py-0.2 rounded font-mono">
                      {dayBookings.length} Lesson{dayBookings.length !== 1 ? 's' : ''}
                    </span>
                  </div>

                  {dayBookings.length === 0 ? (
                    <p className="text-[10px] text-gray-400 italic text-center py-2 font-sans">
                      No tutorial classes listed for this student today. Touch calendar numbers above to view schedules.
                    </p>
                  ) : (
                    <div className="space-y-2">
                      {dayBookings.map(b => {
                        const invoice = invoices.find(inv => inv.bookingId === b.id);
                        const isPaid = invoice && invoice.status === 'Paid';
                        const tutor = tutors.find(t => t.id === b.tutorId);

                        return (
                          <div key={b.id} className="bg-white border border-slate-100 rounded-lg p-2.5 shadow-3xs space-y-2">
                            <div className="flex justify-between items-start">
                              <div>
                                <span className="bg-indigo-50 text-indigo-700 text-[8px] font-extrabold font-mono px-1.5 py-0.2 rounded uppercase">
                                  {b.subject}
                                </span>
                                <h6 className="font-bold text-gray-900 text-[11px] mt-1">Instructor: {tutor?.name || 'Tutor'}</h6>
                                <p className="text-[9.5px] text-gray-500 mt-0.5 font-sans">Hours: {b.time} ({b.durationHours} hrs)</p>
                              </div>
                              <div className="text-right">
                                <span className={`text-[8.5px] px-1.5 py-0.2 rounded font-black uppercase tracking-wide inline-block ${
                                  isPaid 
                                    ? 'bg-emerald-100 text-emerald-800 border border-emerald-200' 
                                    : 'bg-amber-150 text-amber-800 border border-amber-200 animate-pulse'
                                }`}>
                                  {isPaid ? '🟢 Verified Paid' : '⏳ Checkout Pending'}
                                </span>
                                <strong className="block text-[11px] font-bold text-gray-950 mt-1">${b.totalPrice}.00</strong>
                              </div>
                            </div>

                            {/* Payment actions directly embedded inside the student calendar */}
                            <div className="text-[10px] leading-relaxed pt-1 border-t border-slate-50">
                              {isPaid ? (
                                <p className="text-emerald-700 font-semibold flex items-center gap-1">
                                  ✓ Class schedule verified. Learning materials and interactive lesson links fully unlocked.
                                </p>
                              ) : (
                                <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-1.5 pt-1">
                                  <p className="text-amber-700 font-semibold italic flex items-center gap-1">
                                    ⏳ Invoice #{invoice?.id || 'pending'} outstanding.
                                  </p>
                                  {invoice && (
                                    <button
                                      onClick={() => onPayInvoice(invoice.id)}
                                      className="bg-blue-600 hover:bg-blue-700 text-white font-bold text-[9px] px-2.5 py-1 rounded-md transition duration-150 self-end shadow-3xs hover:shadow-2xs active:scale-95 cursor-pointer uppercase font-mono tracking-wider shrink-0"
                                    >
                                      💳 Pay Tuition (${invoice.amount}.00)
                                    </button>
                                  )}
                                </div>
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
        )}
      </div>
    );
  };

  // Booking Form states
  const [bookingDate, setBookingDate] = useState('2026-06-08');
  const [bookingTime, setBookingTime] = useState('04:00 PM');
  const [bookingEndTime, setBookingEndTime] = useState('05:00 PM');
  const [bookingDuration, setBookingDuration] = useState(1);
  const [bookingMessage, setBookingMessage] = useState('');
  const [bookingSuccess, setBookingSuccess] = useState(false);
  const [bookingStudentId, setBookingStudentId] = useState('');
  const [bookingSubject, setBookingSubject] = useState('');
  
  // Session starting state (QR matching simulation)
  const [activeBookingForQR, setActiveBookingForQR] = useState<Booking | null>(null);
  const [timerRemaining, setTimerRemaining] = useState('09:47');
  
  // New child state
  const [newChildName, setNewChildName] = useState('');
  const [newChildSchool, setNewChildSchool] = useState('');
  const [newChildGrade, setNewChildGrade] = useState('Secondary 3');
  const [newChildGoals, setNewChildGoals] = useState('');
  const [childSuccess, setChildSuccess] = useState(false);
  const [newlyAddedStudentId, setNewlyAddedStudentId] = useState<string | null>(null);
  const [newChildPhoto, setNewChildPhoto] = useState<string | undefined>(undefined);
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [expandedCalStudentId, setExpandedCalStudentId] = useState<string | null>(null);
  const [selectedStudentCalDay, setSelectedStudentCalDay] = useState<Record<string, number | null>>({});

  // School selection list states
  const [isSchoolDropdownOpen, setIsSchoolDropdownOpen] = useState(false);
  const [schoolCountryFilter, setSchoolCountryFilter] = useState<'All' | 'Singapore' | 'Malaysia'>('All');
  const [schoolTypeFilter, setSchoolTypeFilter] = useState<'All' | 'Primary' | 'Secondary' | 'Junior College' | 'Polytechnic/Vocational' | 'University/Tertiary'>('All');

  // Chat message state
  const [messageText, setMessageText] = useState('');

  // Conflict details state
  const [reportBookingId, setReportBookingId] = useState('');
  const [issueType, setIssueType] = useState('Tutor was absent (No show)');
  const [issueDetails, setIssueDetails] = useState('');
  const [issueSuccess, setIssueSuccess] = useState(false);

  // View Lesson Report state
  const [selectedLessonReport, setSelectedLessonReport] = useState<Booking | null>(null);

  const filteredTutors = tutors.filter(t => {
    const matchesQuery = t.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
                         t.subjects.some(s => s.toLowerCase().includes(searchQuery.toLowerCase()));
    const matchesSubject = selectedSubject === 'All' || t.subjects.includes(selectedSubject);
    const matchesMode = selectedMode === 'All' || t.modes.includes(selectedMode);
    return matchesQuery && matchesSubject && matchesMode;
  });

  const handleBookingSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTutor) return;

    const chosenStudentId = bookingStudentId || students[0]?.id || 'guest';
    const chosenStudent = students.find(s => s.id === chosenStudentId) || students[0];
    const chosenSubject = bookingSubject || selectedTutor.subjects[0];

    const newBooking: Booking = {
      id: `booking-${Date.now()}`,
      tutorId: selectedTutor.id,
      studentId: chosenStudentId,
      subject: `${chosenSubject} - ${chosenStudent?.educationLevel || 'Sec 3'}`,
      mode: selectedTutor.modes[0],
      date: bookingDate,
      time: `${bookingTime} - ${bookingEndTime}`,
      durationHours: bookingDuration,
      message: bookingMessage,
      totalPrice: selectedTutor.pricePerSession * bookingDuration,
      status: 'pending'
    };

    onAddBooking(newBooking);
    setBookingSuccess(true);
    setTimeout(() => {
      setBookingSuccess(false);
      setSelectedTutor(null);
      setActiveTab('sessions');
    }, 2000);
  };

  const handleCreateChild = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newChildName.trim()) return;

    const studentId = `student-${Date.now()}`;
    const newStudent: Student = {
      id: studentId,
      name: newChildName,
      birthDate: '2013-08-20',
      school: newChildSchool || 'No School Specified',
      educationLevel: newChildGrade,
      subjectSelect: 'Mathematics',
      learningGoal: newChildGoals,
      photoUrl: newChildPhoto // if undefined, it will trigger the initials fallback
    };

    onAddStudent(newStudent);
    setChildSuccess(true);
    setNewlyAddedStudentId(studentId);
    setNewChildName('');
    setNewChildSchool('');
    setNewChildGoals('');
    setNewChildPhoto(undefined);
    setTimeout(() => {
      setChildSuccess(false);
    }, 5000);
  };

  const activeBooking = bookings.find(b => b.status === 'confirmed');
  const pendingConfirmedPayments = invoices.filter(
    inv => inv.status === 'Unpaid' && bookings.some(b => b.id === inv.bookingId && b.status === 'confirmed')
  );

  return (
    <div className="max-w-7xl mx-auto px-4 py-6">
      <div className="lg:grid lg:grid-cols-12 lg:gap-8">
        
        {/* Parent Nav Sidebar */}
        <aside className="lg:col-span-3 mb-6 lg:mb-0">
          <div className="bg-white border border-gray-100 rounded-2xl p-4 shadow-xs space-y-1">
            <div className="p-3 border-b border-gray-100 mb-2 flex items-center space-x-3">
              <img 
                src="https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&q=80&w=100" 
                alt="Sarah" 
                className="w-10 h-10 rounded-full border object-cover" 
              />
              <div>
                <h3 className="font-display font-bold text-gray-950 text-sm">Sarah Tan</h3>
                <span className="text-[10px] text-gray-400 font-sans">Primary parent account</span>
              </div>
            </div>

            <nav className="space-y-1">
              {[
                { id: 'dashboard', name: 'Dashboard Overview', icon: Users },
                { id: 'children', name: 'Setup Student Profile', icon: Plus },
                { id: 'search', name: 'Find Tutors Catalog', icon: Search },
                { id: 'sessions', name: 'Sessions & Activity', icon: Calendar },
                { id: 'billing', name: 'Billing & Invoices', icon: CreditCard },
                { id: 'chat', name: 'Direct Messages', icon: MessageSquare }
              ].map(item => (
                <button
                  key={item.id}
                  onClick={() => setActiveTab(item.id)}
                  className={`w-full text-left px-3 py-2.5 rounded-xl text-xs font-semibold flex items-center gap-2.5 transition duration-150 cursor-pointer ${
                    activeTab === item.id 
                      ? 'bg-blue-600 text-white shadow-xs' 
                      : 'text-gray-600 hover:bg-slate-50 hover:text-gray-900'
                  }`}
                >
                  <item.icon className="h-4 w-4" />
                  {item.name}
                </button>
              ))}
            </nav>
            
            <div className="pt-4 border-t border-gray-100 mt-4">
              <div className="p-3 bg-blue-50/50 rounded-xl border border-blue-100/50">
                <span className="text-[9px] bg-blue-100 text-blue-700 px-1.5 py-0.5 rounded font-bold uppercase font-mono">SUPPORT</span>
                <p className="text-[10px] text-gray-700 mt-1.5 font-medium">Need immediate assistance with a class match?</p>
                <button onClick={() => setActiveTab('chat')} className="text-[10px] text-blue-600 font-bold mt-1 hover:underline flex items-center gap-0.5">
                  Write to Support <ArrowRight className="h-3 w-3" />
                </button>
              </div>
            </div>
          </div>
        </aside>

        {/* Dynamic Section Contents */}
        <main className="lg:col-span-9 space-y-6">
          
          {/* Confirmed Class Escrow Alert / Immediate Payment Module Trigger */}
          {pendingConfirmedPayments.length > 0 && (
            <div className="bg-gradient-to-r from-emerald-700 via-indigo-700 to-blue-800 border border-emerald-500/20 p-5 rounded-2xl text-white shadow-lg">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div className="flex items-start gap-3.5">
                  <div className="p-2.5 bg-white/10 rounded-xl shrink-0 mt-0.5">
                    <CreditCard className="h-5 w-5 text-emerald-250 animate-pulse" />
                  </div>
                  <div>
                    <span className="text-[9px] bg-emerald-500/30 text-emerald-200 border border-emerald-400/20 px-2 py-0.5 rounded-full font-bold uppercase tracking-wider">
                      ★ ACTION REQUIRED
                    </span>
                    <h4 className="font-display font-extrabold text-white text-xs sm:text-sm tracking-tight mt-1.5">Class Appointment Confirmed by Tutor</h4>
                    <p className="text-[11px] text-slate-100 mt-1 leading-relaxed max-w-2xl">
                      Your requested reservation has been officially certified by the tutor. To finalise matching coordinates and secure session schedules, please complete the matching escrow deposit transaction below.
                    </p>
                    <div className="flex gap-2 mt-2.5 flex-wrap items-center">
                      {pendingConfirmedPayments.map(inv => (
                        <span key={inv.id} className="text-[9.5px] font-bold font-mono bg-black/20 text-emerald-200 rounded-lg px-2.5 py-1 border border-white/5 flex items-center gap-1.5">
                          {inv.subject} • <strong className="text-white">${inv.amount}.00</strong>
                        </span>
                      ))}
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-2.5 shrink-0 w-full md:w-auto self-end md:self-center">
                  <button 
                    onClick={() => {
                      if (pendingConfirmedPayments[0]) {
                        onPayInvoice(pendingConfirmedPayments[0].id);
                      }
                    }}
                    className="bg-white hover:bg-slate-50 text-slate-950 border-none rounded-xl text-xs font-black px-5 py-3 shadow-md transition-all duration-150 cursor-pointer flex items-center justify-center gap-1.5 w-full md:w-auto hover:translate-y-[-1px] active:translate-y-0"
                  >
                    💳 Escrow Instant Payment (${pendingConfirmedPayments.reduce((total, cur) => total + cur.amount, 0)}.00)
                  </button>
                </div>
              </div>
            </div>
          )}
          
          {/* Dashboard Hub Tab */}
          {activeTab === 'dashboard' && (
            <div className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                
                {/* Upcoming session overview card */}
                <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-xs">
                  <div className="flex justify-between items-center mb-4">
                    <h3 className="font-display font-bold text-gray-900 text-sm">Upcoming Tutorials</h3>
                    <button onClick={() => setActiveTab('sessions')} className="text-xs text-blue-600 hover:underline">View All</button>
                  </div>
                  
                  {bookings.filter(b => b.status === 'confirmed' || b.status === 'countered').length === 0 ? (
                    <div className="p-6 text-center bg-gray-50 rounded-2xl border border-dashed border-gray-200">
                      <p className="text-xs text-gray-500">No upcoming tutorial sessions scheduled.</p>
                      <button onClick={() => setActiveTab('search')} className="mt-3 bg-blue-600 text-white px-3 py-1.5 rounded-lg text-[10.5px] font-bold">Find a Tutor</button>
                    </div>
                  ) : (
                    <div className="space-y-3">
                      {bookings.filter(b => b.status === 'confirmed' || b.status === 'countered').map(booking => {
                        const tutor = tutors.find(t => t.id === booking.tutorId);
                        return (
                          <div key={booking.id} className="p-3 border rounded-xl flex items-center justify-between bg-slate-50/50">
                            <div className="flex items-center space-x-3">
                              {tutor && <img src={tutor.imageUrl} alt={tutor.name} className="w-9 h-9 rounded-full object-cover" />}
                              <div>
                                <span className="text-[9px] bg-blue-100 text-blue-700 font-bold px-1.5 py-0.2 rounded uppercase">{booking.subject}</span>
                                <h4 className="font-bold text-gray-900 text-xs mt-1">{tutor?.name || 'Tutor'}</h4>
                                <p className="text-[10px] text-gray-500 flex items-center mt-0.5"><Clock className="h-3 w-3 mr-1" /> {booking.date} • {booking.time}</p>
                              </div>
                            </div>
                            <div className="text-right">
                              {booking.status === 'countered' ? (
                                <span className="bg-amber-100 text-amber-800 text-[9px] px-2 py-0.5 rounded font-bold uppercase border border-amber-200 animate-pulse">PROPOS_ADJUSTED</span>
                              ) : (
                                <span className="bg-emerald-100 text-emerald-800 text-[9px] px-2 py-0.5 rounded font-bold uppercase border border-emerald-200">CONFIRMED</span>
                              )}
                              <button onClick={() => setActiveTab('sessions')} className="block text-xs font-semibold text-blue-600 mt-1 hover:underline text-right">Details</button>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>

                {/* Progress Indicators */}
                <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-xs">
                  <h3 className="font-display font-bold text-gray-900 text-sm mb-4">Jessica's Progress Breakdown</h3>
                  <div className="space-y-4">
                    <div>
                      <div className="flex justify-between text-xs text-gray-600 mb-1">
                        <span className="font-medium">Secondary Geometry & Shapes</span>
                        <span className="font-bold">85% Mastery</span>
                      </div>
                      <div className="w-full bg-gray-100 h-2 rounded-full overflow-hidden">
                        <div className="bg-blue-600 h-full rounded-full" style={{ width: '85%' }}></div>
                      </div>
                    </div>
                    <div>
                      <div className="flex justify-between text-xs text-gray-600 mb-1">
                        <span className="font-medium">Linear Algebra & Graph Solutions</span>
                        <span className="font-bold">76% Mastery</span>
                      </div>
                      <div className="w-full bg-gray-100 h-2 rounded-full overflow-hidden">
                        <div className="bg-emerald-500 h-full rounded-full" style={{ width: '76%' }}></div>
                      </div>
                    </div>
                    <div>
                      <div className="flex justify-between text-xs text-gray-600 mb-1">
                        <span className="font-medium">Quadratic Calculations Speed</span>
                        <span className="font-bold">62% Mastery</span>
                      </div>
                      <div className="w-full bg-gray-100 h-2 rounded-full overflow-hidden">
                        <div className="bg-amber-500 h-full rounded-full" style={{ width: '62%' }}></div>
                      </div>
                    </div>
                  </div>
                </div>

              </div>

              {/* Children Profiles Mini Card */}
              <div className="bg-white border border-gray-100 rounded-2xl p-5 shadow-xs">
                <div className="flex justify-between items-center mb-4">
                  <h3 className="font-display font-bold text-gray-900 text-sm">Active Children Profiles</h3>
                  <button onClick={() => setActiveTab('children')} className="text-xs text-blue-600 hover:underline flex items-center gap-0.5"><Plus className="h-3.5 w-3.5" /> Add Student</button>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {students.map(student => (
                    <div key={student.id} className="p-4 border border-slate-100 rounded-2xl flex flex-col gap-3 bg-white hover:border-slate-200 transition">
                      <div className="flex items-start space-x-3">
                        {renderStudentAvatar(student, "w-12 h-12 text-sm")}
                        <div>
                          <h4 className="font-bold text-gray-950 text-xs">{student.name}</h4>
                          <p className="text-[10px] text-gray-500 mt-0.5">{student.educationLevel} • {student.school}</p>
                          {student.learningGoal && <p className="text-[10px] text-gray-400 mt-1 font-sans italic">Goal: "{student.learningGoal}"</p>}
                        </div>
                      </div>
                      {renderStudentCalendar(student)}
                    </div>
                  ))}
                </div>
              </div>

              {/* Announcements Banner */}
              <div className="bg-linear-to-r from-blue-50 to-indigo-50 border border-blue-100 p-4 rounded-xl flex items-center justify-between gap-4">
                <div className="flex items-center gap-3">
                  <span className="bg-amber-100 text-amber-800 p-2 rounded-lg"><AlertTriangle className="h-4 w-4" /></span>
                  <div>
                    <h5 className="font-bold text-slate-900 text-xs">System Maintenance scheduled</h5>
                    <p className="text-[10.5px] text-slate-600 mt-0.5">The dashboard server will undergo security maintenance on June 15th, 2:00 AM - 4:00 AM SGT.</p>
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Setup Student Profile Tab & Profiles Side-by-Side */}
          {activeTab === 'children' && (
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
              
              {/* Left Column: Profiling System Form (col-span-7) */}
              <div className="bg-white border border-gray-100 rounded-2xl p-6 shadow-xs lg:col-span-7">
                <span className="text-[10px] font-bold text-blue-600 uppercase">Profiling System</span>
                <h2 className="font-display font-extrabold text-lg text-gray-950 mt-1">Add Child / Student Profile</h2>
                <p className="text-xs text-gray-500 mt-1">
                  Customize their education tier, schools, and targeted subjects to match with NIE teachers.
                </p>

                {childSuccess && (
                  <div className="mt-4 bg-emerald-50 border border-emerald-100 text-emerald-800 p-3.5 rounded-xl text-center flex items-center justify-center gap-2 animate-bounce">
                    <CheckCircle className="h-5 w-5 text-emerald-600" />
                    <span className="text-xs font-bold font-sans">New student profile has been created and displayed beside!</span>
                  </div>
                )}

                <form onSubmit={handleCreateChild} className="mt-6 space-y-4">
                  {/* Child Full Name is extended to 50% width and Current Education Institution maintains the same extended 80% width! */}
                  <div className="flex flex-col gap-4">
                    <div className="w-full md:w-1/2">
                      <label className="text-[11px] text-gray-500 block font-semibold">Child Full Name</label>
                      <input 
                        type="text" 
                        required
                        placeholder="e.g. Jessica Tan"
                        value={newChildName}
                        onChange={(e) => setNewChildName(e.target.value)}
                        className="w-full bg-slate-50 border border-gray-200 rounded-lg py-2 px-3 text-xs mt-1 focus:bg-white" 
                      />
                    </div>
                    <div className="w-full md:w-[80%] relative">
                      <label className="text-[11px] text-gray-500 block font-semibold">Current Education Institution</label>
                      
                      <div className="relative mt-1">
                        <div className="absolute inset-y-0 left-0 pl-2.5 flex items-center pointer-events-none">
                          <Building className="h-3.5 w-3.5 text-gray-400" />
                        </div>
                        <input 
                          type="text" 
                          placeholder="Search or select school in SG/MY..."
                          value={newChildSchool}
                          onChange={(e) => {
                            setNewChildSchool(e.target.value);
                            setIsSchoolDropdownOpen(true);
                          }}
                          onFocus={() => setIsSchoolDropdownOpen(true)}
                          className="w-full bg-slate-50 border border-gray-200 rounded-lg py-2 pl-8 pr-8 text-xs focus:bg-white focus:outline-none focus:ring-1 focus:ring-blue-500" 
                        />
                        <button
                          type="button"
                          onClick={() => setIsSchoolDropdownOpen(!isSchoolDropdownOpen)}
                          className="absolute inset-y-0 right-0 pr-2.5 flex items-center text-gray-400 hover:text-gray-600"
                        >
                          <ChevronDown className="h-4 w-4" />
                        </button>
                      </div>

                      {/* Click away backdrop */}
                      {isSchoolDropdownOpen && (
                        <div 
                          className="fixed inset-0 z-40 bg-transparent" 
                          onClick={() => setIsSchoolDropdownOpen(false)} 
                        />
                      )}

                      {/* Dropdown panel */}
                      {isSchoolDropdownOpen && (
                        <div className="absolute left-0 right-0 mt-1 bg-white border border-gray-200 rounded-xl shadow-xl z-50 p-3 max-h-80 overflow-y-auto flex flex-col gap-2.5 animate-in fade-in-50 slide-in-from-top-2 duration-150">
                          {/* Country Filter Tabs */}
                          <div className="flex items-center justify-between border-b pb-2">
                            <span className="text-[9px] text-gray-400 font-bold uppercase">Country Filter</span>
                            <div className="flex gap-1">
                              {(['All', 'Singapore', 'Malaysia'] as const).map(country => (
                                <button
                                  key={country}
                                  type="button"
                                  onClick={() => setSchoolCountryFilter(country)}
                                  className={`px-2 py-0.5 rounded text-[9px] font-bold ${
                                    schoolCountryFilter === country 
                                      ? 'bg-blue-600 text-white' 
                                      : 'bg-slate-100 text-gray-600 hover:bg-slate-200'
                                  }`}
                                >
                                  {country === 'Singapore' ? 'SG 🇸🇬' : country === 'Malaysia' ? 'MY 🇲🇾' : 'All'}
                                </button>
                              ))}
                            </div>
                          </div>

                          {/* Level Filter Dropdown/Tabs */}
                          <div className="flex items-center justify-between border-b pb-2">
                            <span className="text-[9px] text-gray-400 font-bold uppercase">Type/Level</span>
                            <select
                              value={schoolTypeFilter}
                              onChange={(e) => setSchoolTypeFilter(e.target.value as any)}
                              className="bg-slate-50 border border-gray-200 rounded px-1.5 py-0.5 text-[9px] font-bold text-gray-600"
                            >
                              <option value="All">All Types</option>
                              <option value="Primary">Primary</option>
                              <option value="Secondary">Secondary</option>
                              <option value="Junior College">Junior College</option>
                              <option value="Polytechnic/Vocational">Polytechnic / Vocational</option>
                              <option value="University/Tertiary">University / Tertiary</option>
                            </select>
                          </div>

                          {/* Institutions list */}
                          <div className="space-y-1 overflow-y-auto max-h-48 pr-1">
                            {/* Query filtered insts */}
                            {(() => {
                              const filtered = INSTITUTIONS.filter(inst => {
                                const matchesSearch = inst.name.toLowerCase().includes(newChildSchool.toLowerCase()) ||
                                                      inst.type.toLowerCase().includes(newChildSchool.toLowerCase()) ||
                                                      inst.country.toLowerCase().includes(newChildSchool.toLowerCase());
                                const matchesCountry = schoolCountryFilter === 'All' || inst.country === schoolCountryFilter;
                                const matchesType = schoolTypeFilter === 'All' || inst.type === schoolTypeFilter;
                                return matchesSearch && matchesCountry && matchesType;
                              });

                              if (filtered.length === 0) {
                                return (
                                  <div className="p-2 text-center text-xs text-gray-400">
                                    <p>No matching institutions.</p>
                                    {newChildSchool.trim() && (
                                      <button
                                        type="button"
                                        onClick={() => setIsSchoolDropdownOpen(false)}
                                        className="mt-2 text-blue-600 font-bold hover:underline"
                                      >
                                        Use Custom: "{newChildSchool}"
                                      </button>
                                    )}
                                  </div>
                                );
                              }

                              return filtered.slice(0, 15).map(inst => (
                                <button
                                  key={inst.name}
                                  type="button"
                                  onClick={() => {
                                    setNewChildSchool(inst.name);
                                    setIsSchoolDropdownOpen(false);
                                  }}
                                  className="w-full text-left p-2 hover:bg-blue-50/70 rounded-lg text-xs transition duration-100 flex items-center justify-between gap-2"
                                >
                                  <span className="font-medium text-gray-800 truncate">{inst.name}</span>
                                  <div className="flex gap-1 shrink-0">
                                    <span className={`text-[8px] font-bold px-1 py-0.2 rounded border ${
                                      inst.country === 'Singapore' 
                                        ? 'bg-blue-50 text-blue-700 border-blue-100' 
                                        : 'bg-indigo-50 text-indigo-700 border-indigo-100'
                                    }`}>
                                      {inst.country === 'Singapore' ? 'SG 🇸🇬' : 'MY 🇲🇾'}
                                    </span>
                                    <span className="text-[8px] bg-slate-100 text-gray-500 px-1 py-0.2 rounded border border-slate-200">
                                      {inst.type}
                                    </span>
                                  </div>
                                </button>
                              ));
                            })()}
                          </div>

                          {/* Custom addition hint */}
                          {newChildSchool.trim() && (
                            <div className="text-[10px] text-gray-400 text-center border-t pt-1.5">
                              Can't find your school? Keep typing to enter a custom name.
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label className="text-[11px] text-gray-500 block font-semibold">Education Level</label>
                      <select 
                        value={newChildGrade}
                        onChange={(e) => setNewChildGrade(e.target.value)}
                        className="w-full bg-slate-50 border border-gray-200 rounded-lg py-2 px-3 text-xs mt-1"
                      >
                        <option>Primary 5-6</option>
                        <option>Secondary 1</option>
                        <option>Secondary 2</option>
                        <option>Secondary 3</option>
                        <option>Secondary 4</option>
                        <option>Junior College (JC)</option>
                      </select>
                    </div>
                    <div>
                      <label className="text-[11px] text-gray-500 block font-semibold flex items-center gap-1">
                        <Camera className="h-3.5 w-3.5 text-blue-500" /> Student Profile Photo
                      </label>
                      <div 
                        onDragOver={(e) => {
                          e.preventDefault();
                          setIsDragging(true);
                        }}
                        onDragLeave={() => setIsDragging(false)}
                        onDrop={(e) => {
                          e.preventDefault();
                          setIsDragging(false);
                          const file = e.dataTransfer.files?.[0];
                          if (file) {
                            handlePhotoUpload(file);
                          }
                        }}
                        onClick={() => fileInputRef.current?.click()}
                        className={`border border-dashed rounded-lg py-2.5 px-3 text-center text-[10px] mt-1 cursor-pointer transition duration-150 flex flex-col items-center justify-center min-h-[50px] ${
                          isDragging 
                            ? 'border-blue-500 bg-blue-50 text-blue-700 font-bold' 
                            : newChildPhoto 
                              ? 'border-emerald-350 bg-emerald-50/25 text-emerald-800 font-bold' 
                              : 'border-gray-300 bg-slate-50 text-gray-400 hover:border-gray-400 hover:bg-slate-100/50'
                        }`}
                      >
                        <input 
                          type="file"
                          ref={fileInputRef}
                          onChange={(e) => {
                            const file = e.target.files?.[0];
                            if (file) {
                              handlePhotoUpload(file);
                            }
                          }}
                          accept="image/*"
                          className="hidden" 
                        />
                        {newChildPhoto ? (
                          <div className="flex items-center gap-2">
                            <img src={newChildPhoto} alt="Upload Preview" className="w-8 h-8 rounded-full object-cover border border-emerald-300" />
                            <div className="text-left leading-none">
                              <span className="text-[9px] font-extrabold text-emerald-700 block uppercase">✓ Photo Selected</span>
                              <span 
                                className="text-[8px] text-rose-500 hover:underline font-semibold mt-0.5 inline-block" 
                                onClick={(e) => {
                                  e.stopPropagation();
                                  setNewChildPhoto(undefined);
                                }}
                              >
                                Remove
                              </span>
                            </div>
                          </div>
                        ) : (
                          <div className="flex items-center gap-1.5 py-0.5 justify-center">
                            <Upload className="h-3 w-3 text-gray-400" />
                            <span>
                              Drag photo or <strong className="text-blue-600 font-bold hover:underline">browse files</strong>
                            </span>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>

                  <div>
                    <label className="text-[11px] text-gray-500 block font-semibold">Learning Milestones & Goals (Optional)</label>
                    <textarea 
                      value={newChildGoals}
                      onChange={(e) => setNewChildGoals(e.target.value)}
                      placeholder="e.g. Improve algebra speed, needs patient explanations for quadratic formula, prepare for national exams..." 
                      className="w-full bg-slate-50 border border-gray-200 rounded-lg py-2 px-3 text-xs mt-1 h-20 focus:bg-white"
                    ></textarea>
                  </div>

                  <button type="submit" className="w-full bg-blue-600 hover:bg-blue-700 text-white font-bold py-2.5 rounded-xl text-xs transition shadow-xs cursor-pointer">
                    Save Student Profile
                  </button>
                </form>
              </div>

              {/* Right Column: Active Profiles List (col-span-5) */}
              <div className="bg-white border border-gray-100 rounded-2xl p-6 shadow-xs lg:col-span-5 space-y-4 max-h-[640px] overflow-y-auto">
                <div>
                  <span className="text-[10px] font-bold text-indigo-600 uppercase font-mono">Registry Records</span>
                  <h3 className="font-display font-black text-gray-950 text-base mt-0.5">Active Student Files</h3>
                  <p className="text-[11px] text-gray-400 mt-1 leading-normal font-sans">
                    Here are the student accounts associated with your SG/MY parent dashboard.
                  </p>
                </div>

                <div className="space-y-3">
                  {students.map(student => {
                    const isNew = student.id === newlyAddedStudentId;
                    return (
                      <div 
                        key={student.id} 
                        className={`p-4 border rounded-xl transition-all duration-500 relative overflow-hidden flex flex-col gap-2 bg-slate-50/20 ${
                          isNew 
                            ? 'border-emerald-500 bg-emerald-50/20 shadow-[0_0_12px_rgba(16,185,129,0.1)] ring-1 ring-emerald-400' 
                            : 'border-slate-100 hover:border-slate-200'
                        }`}
                      >
                        {isNew && (
                          <div className="absolute top-2.5 right-2.5 bg-emerald-600 text-white text-[8px] font-black px-2 py-0.5 rounded-full flex items-center gap-0.5 shadow-xs animate-pulse">
                            <Star className="h-2 w-2 fill-white" /> NEWLY ADDED
                          </div>
                        )}
                        <div className="flex items-start gap-3">
                          {renderStudentAvatar(student, "w-10 h-10 text-xs")}
                          <div className="flex-1 min-w-0">
                            <h4 className="font-bold text-gray-950 text-xs truncate">{student.name}</h4>
                            <p className="text-[9.5px] text-gray-400 font-mono mt-0.5">{student.educationLevel}</p>
                            <div className="mt-1 flex items-center gap-1.5 flex-wrap">
                              <span className="bg-blue-50 text-blue-700 text-[8.5px] font-bold px-1.5 py-0.2 rounded border border-blue-100 flex items-center gap-0.5">
                                <Building className="h-2.5 w-2.5" /> {student.school || "No School Specified"}
                              </span>
                            </div>
                          </div>
                        </div>
                        {student.learningGoal && (
                          <div className="mt-1 p-2 bg-slate-50 rounded-lg border border-slate-100">
                            <span className="text-[8px] text-slate-400 font-bold block uppercase tracking-wide">Target Milestones</span>
                            <p className="text-[9.5px] text-slate-600 mt-0.5 italic leading-relaxed">"{student.learningGoal}"</p>
                          </div>
                        )}
                        {renderStudentCalendar(student)}
                      </div>
                    );
                  })}
                </div>
              </div>

            </div>
          )}

          {/* Find Tutors Area */}
          {activeTab === 'search' && !selectedTutor && (
            <div className="space-y-6">
              
              {/* Search & filters controls */}
              <div className="bg-white p-5 rounded-2xl border border-gray-100 shadow-xs">
                <div className="flex gap-2 mb-4 bg-slate-50 p-2 rounded-xl">
                  <div className="flex-1 relative">
                    <Search className="absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
                    <input 
                      type="text" 
                      placeholder="Search subject areas, curriculum levels, or teacher names..." 
                      className="w-full bg-white border border-gray-200 text-xs pl-9 pr-3 py-2 rounded-lg"
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                    />
                  </div>
                </div>

                <div className="flex flex-wrap items-center gap-4 text-xs font-sans">
                  <div className="flex items-center gap-1.5">
                    <span className="text-gray-500">Subjects:</span>
                    <select 
                      value={selectedSubject} 
                      onChange={(e) => setSelectedSubject(e.target.value)} 
                      className="bg-slate-50 border border-gray-200 rounded-lg py-1 px-2.5 text-xs font-semibold"
                    >
                      <option value="All">All Subjects</option>
                      <option value="Mathematics">Mathematics</option>
                      <option value="Additional Mathematics">Additional Mathematics</option>
                      <option value="Chemistry">Chemistry</option>
                      <option value="Physics">Physics</option>
                      <option value="Science">Science</option>
                    </select>
                  </div>

                  <div className="flex items-center gap-1.5 mr-auto">
                    <span className="text-gray-500">Modes:</span>
                    <select 
                      value={selectedMode} 
                      onChange={(e) => setSelectedMode(e.target.value)} 
                      className="bg-slate-50 border border-gray-200 rounded-lg py-1 px-2.5 text-xs font-semibold"
                    >
                      <option value="All">All Modes</option>
                      <option value="Online">Online</option>
                      <option value="Home Visit">Home Visit</option>
                    </select>
                  </div>
                  
                  <span className="text-[10px] text-gray-400 font-mono font-bold mt-1 sm:mt-0">{filteredTutors.length} Tuters Found</span>
                </div>
              </div>

              {/* Tutors Listing Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {filteredTutors.map(tutor => (
                  <div key={tutor.id} className="bg-white border border-gray-100 hover:border-blue-100 rounded-2xl p-5 shadow-xs flex flex-col justify-between transition hover:shadow-md">
                    <div>
                      <div className="flex items-start gap-3.5">
                        <img src={tutor.imageUrl} alt={tutor.name} className="w-12 h-12 rounded-full object-cover border-2 border-slate-100" />
                        <div>
                          <div className="flex items-center gap-1.5 flex-wrap">
                            <h4 className="font-display font-bold text-gray-900 text-sm leading-tight">{tutor.name}</h4>
                            <span className="bg-amber-50 text-amber-600 border border-amber-100 text-[10px] px-1.5 py-0.2 rounded font-semibold flex items-center gap-0.5">⭐ {tutor.rating}</span>
                          </div>
                          <div className="text-[11.5px] text-gray-500 font-medium mt-0.5 flex flex-wrap items-center gap-x-1 gap-y-0.5">
                            {tutor.subjects.map((sub, idx) => (
                              <React.Fragment key={sub}>
                                <button 
                                  onClick={() => setSelectedSubject(sub)}
                                  className="text-blue-600 hover:text-blue-800 hover:underline cursor-pointer font-semibold inline-block focus:outline-none"
                                  title={`Filter by ${sub}`}
                                >
                                  {sub}
                                </button>
                                {idx < tutor.subjects.length - 1 && <span className="text-gray-400 mr-1">,</span>}
                              </React.Fragment>
                            ))}
                          </div>
                          <p className="text-[10px] text-gray-400 mt-0.5">{tutor.experienceYears} Years Exp • {tutor.modes.join('/')}</p>
                        </div>
                      </div>
                      
                      <p className="text-[11px] text-gray-600 mt-3 line-clamp-2 leading-relaxed">
                        "{tutor.bio}"
                      </p>

                      <div className="mt-4 flex flex-wrap gap-1">
                        {tutor.qualifications.slice(0, 2).map((q, i) => (
                          <span key={i} className="bg-slate-100 text-slate-700 text-[9px] px-2 py-0.5 rounded font-mono font-medium">{q}</span>
                        ))}
                      </div>
                    </div>

                    <div className="border-t border-gray-100 mt-4 pt-4 flex justify-between items-center">
                      <div>
                        <span className="text-[9px] text-gray-400 block font-semibold">TRIAL RATE</span>
                        <span className="text-sm font-bold text-blue-600">${tutor.pricePerSession}/hr</span>
                      </div>
                      <button 
                        onClick={() => {
                          setSelectedTutor(tutor);
                          if (students.length > 0) {
                            setBookingStudentId(students[0].id);
                          } else {
                            setBookingStudentId('');
                          }
                          if (tutor.subjects.length > 0) {
                            setBookingSubject(tutor.subjects[0]);
                          } else {
                            setBookingSubject('');
                          }
                        }}
                        className="bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold px-4 py-2 rounded-xl transition cursor-pointer"
                      >
                        View Profile & Book
                      </button>
                    </div>
                  </div>
                ))}
              </div>

            </div>
          )}

          {/* Book Tutor detail view */}
          {activeTab === 'search' && selectedTutor && (
            <div className="bg-white border border-gray-100 rounded-2xl p-6 shadow-xs max-w-2xl">
              <button onClick={() => setSelectedTutor(null)} className="text-xs text-blue-600 hover:underline mb-4 font-bold flex items-center gap-1">
                ← Return to Tutor Listing
              </button>

              {bookingSuccess ? (
                <div className="bg-emerald-50 border border-emerald-100 text-emerald-800 p-6 rounded-2xl text-center my-6">
                  <CheckCircle className="h-10 w-10 text-emerald-600 mx-auto mb-3" />
                  <h3 className="font-display font-bold text-sm">Booking Request Dispatched!</h3>
                  <p className="text-[11px] mt-1 text-emerald-700">Connecting you with {selectedTutor.name}. Redirecting to your sessions log...</p>
                </div>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-12 gap-6">
                  
                  {/* Tutor info column */}
                  <div className="md:col-span-5 space-y-4">
                    <div className="text-center p-4 bg-slate-50 rounded-2xl border">
                      <img src={selectedTutor.imageUrl} alt={selectedTutor.name} className="w-16 h-16 rounded-full object-cover border-2 border-white shadow-xs mx-auto mb-3" />
                      <h3 className="font-display font-medium text-gray-900 text-sm leading-tight">{selectedTutor.name}</h3>
                      <p className="text-[10px] text-gray-500 mt-0.5">{selectedTutor.subjects[0]}</p>
                      <div className="flex justify-center mt-1">
                        <span className="text-[10px] text-amber-500 font-bold">★ {selectedTutor.rating} ({selectedTutor.reviewCount} reviews)</span>
                      </div>
                    </div>

                    <div className="space-y-2 text-xs">
                      <h4 className="font-bold text-gray-700 uppercase tracking-wide text-[9px]">Credentials</h4>
                      <div className="divide-y divide-gray-100 bg-white p-2 border rounded-xl overflow-hidden shadow-xs">
                        {selectedTutor.qualifications.map((q, i) => (
                          <div key={i} className="py-1.5 px-1 font-mono text-[9px] text-gray-600 flex items-center gap-1.5">
                            <ShieldCheck className="h-3 w-3 text-emerald-500 shrink-0" />
                            {q}
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>

                  {/* Form Submission column */}
                  <div className="md:col-span-7">
                    <span className="text-[9px] bg-slate-100 text-slate-700 px-2.5 py-1 rounded font-mono font-bold uppercase">Pre-Booking Appointment Form</span>
                    <h3 className="font-display font-extrabold text-base text-gray-900 mt-2">Request Tutorial Session</h3>
                    <p className="text-[11px] text-gray-400 mb-4">Rate: ${selectedTutor.pricePerSession}/session (2 hours trial lesson)</p>

                    <form onSubmit={handleBookingSubmit} className="space-y-3 font-sans">
                      <div className="space-y-3">
                        <div>
                          <label className="text-[10px] text-gray-500 block font-semibold">Select Student</label>
                          <select 
                            value={bookingStudentId || (students[0]?.id || '')}
                            onChange={(e) => setBookingStudentId(e.target.value)}
                            className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1 focus:outline-none focus:ring-1 focus:ring-blue-500"
                          >
                            {students.map(std => (
                              <option key={std.id} value={std.id}>{std.name}</option>
                            ))}
                          </select>
                        </div>
                        <div>
                          <label className="text-[10.5px] text-gray-500 block font-semibold">Subject Module</label>
                          <div className="flex flex-wrap gap-2 mt-1.5">
                            {selectedTutor.subjects.map(sub => {
                              const isSelected = (bookingSubject || selectedTutor.subjects[0]) === sub;
                              return (
                                <button
                                  key={sub}
                                  type="button"
                                  onClick={() => setBookingSubject(sub)}
                                  className={`px-3 py-1.5 rounded-lg border text-xs font-semibold cursor-pointer transition-all duration-150 ${
                                    isSelected
                                      ? 'bg-blue-600 border-blue-600 text-white shadow-xs font-bold'
                                      : 'bg-slate-50 border-gray-200 text-gray-600 hover:bg-slate-100 hover:border-gray-300'
                                  }`}
                                >
                                  {sub}
                                </button>
                              );
                            })}
                          </div>
                        </div>
                      </div>

                      <div className="grid grid-cols-3 gap-2">
                        <div>
                          <label className="text-[10px] text-gray-500 block font-semibold">Date</label>
                          <input 
                            type="date" 
                            required
                            value={bookingDate}
                            onChange={(e) => setBookingDate(e.target.value)}
                            className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1 font-mono" 
                          />
                        </div>
                        <div>
                          <label className="text-[10px] text-gray-500 block font-semibold">Start Time</label>
                          <input 
                            type="text" 
                            required
                            placeholder="e.g. 04:00 PM"
                            value={bookingTime}
                            onChange={(e) => {
                              const newStart = e.target.value;
                              setBookingTime(newStart);
                              // Auto calculate end time as start time + 1 hour
                              setBookingEndTime(addOneHour(newStart));
                            }}
                            className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1 font-mono" 
                          />
                        </div>
                        <div>
                          <label className="text-[10px] text-indigo-600 block font-bold">End Time (Auto)</label>
                          <input 
                            type="text" 
                            required
                            placeholder="e.g. 05:00 PM"
                            value={bookingEndTime}
                            onChange={(e) => setBookingEndTime(e.target.value)}
                            className="w-full bg-indigo-50/50 border border-indigo-200 text-indigo-950 font-bold rounded-lg py-1.5 px-3 text-xs mt-1 font-mono" 
                          />
                        </div>
                      </div>

                      <div>
                        <label className="text-[10px] text-gray-500 block font-semibold">Optional Message / Guidelines</label>
                        <textarea 
                          value={bookingMessage}
                          onChange={(e) => setBookingMessage(e.target.value)}
                          placeholder="e.g. Please focus on quadratic equation formulas and systems." 
                          className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1 h-14"
                        ></textarea>
                      </div>

                      <div className="bg-slate-50 p-2 text-xs font-semibold rounded-lg flex justify-between items-center text-[10px] my-3">
                        <span className="text-gray-400">Total Price Calculation</span>
                        <strong className="text-blue-600 block text-xs">${selectedTutor.pricePerSession * bookingDuration} for {bookingDuration} hours</strong>
                      </div>

                      <button type="submit" className="w-full bg-blue-600 hover:bg-blue-700 text-white font-bold py-2 rounded-lg text-xs transition cursor-pointer">
                        Dispatch Request
                      </button>
                    </form>
                  </div>

                </div>
              )}
            </div>
          )}

          {/* Sessions & Activity Tracker Tab */}
          {activeTab === 'sessions' && (
            <div className="space-y-6">
              
              {/* Reports conflict notifier */}
              {issueSuccess ? (
                <div className="p-4 bg-emerald-50 border border-emerald-100 text-emerald-800 rounded-xl flex items-center justify-between gap-3 text-xs">
                  <span className="font-semibold block">Your incident dispatch has been logged in LearnSphere master panel successfully. Administation is investigating.</span>
                  <button onClick={() => { setIssueSuccess(false); setReportBookingId(''); }} className="text-emerald-900 underline font-bold uppercase">Dismiss</button>
                </div>
              ) : reportBookingId ? (
                <div className="bg-white border rounded-2xl p-5 shadow-xs max-w-md">
                  <h3 className="font-display font-medium text-red-600 flex items-center gap-2 text-sm">
                    <AlertTriangle className="h-5 w-5" /> Report Class Conflict
                  </h3>
                  <p className="text-[11px] text-gray-400 mt-1">Let us know if your scheduled tutor didn't fulfill expectations (#booking id: {reportBookingId}).</p>
                  
                  <form className="mt-4 space-y-3" onSubmit={(e) => {
                    e.preventDefault();
                    onReportIssue(reportBookingId, issueType, issueDetails);
                    setIssueSuccess(true);
                  }}>
                    <div>
                      <span className="text-[10px] text-gray-400 font-semibold block">Incident Issue Type</span>
                      <select 
                        value={issueType}
                        onChange={(e) => setIssueType(e.target.value)}
                        className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1"
                      >
                        <option>Tutor was absent (No show)</option>
                        <option>Tutor was late ( {'>'} 20 mins)</option>
                        <option>Other scheduling dispute</option>
                      </select>
                    </div>
                    <div>
                      <span className="text-[10px] text-gray-400 font-semibold block">What happened?</span>
                      <textarea 
                        required
                        value={issueDetails}
                        onChange={(e) => setIssueDetails(e.target.value)}
                        placeholder="Provide details about wait times, response, or other issues..." 
                        className="w-full bg-slate-50 border border-gray-200 rounded-lg py-1.5 px-3 text-xs mt-1 h-16"
                      ></textarea>
                    </div>
                    <div className="flex gap-2.5 pt-1">
                      <button type="button" onClick={() => setReportBookingId('')} className="flex-1 bg-slate-50 border hover:bg-slate-100 text-gray-800 text-xs py-1.5 rounded-lg">Cancel</button>
                      <button type="submit" className="flex-1 bg-red-600 text-white hover:bg-red-700 text-xs py-1.5 rounded-lg font-bold">Dispatch Dispute</button>
                    </div>
                  </form>
                </div>
              ) : null}

              {/* View lesson report detail modal inside page */}
              {selectedLessonReport && selectedLessonReport.lessonReport && (
                <div className="bg-white border-2 border-blue-100 rounded-2xl p-5 shadow-lg max-w-xl">
                  <div className="flex justify-between items-start border-b pb-3 mb-3">
                    <div>
                      <span className="text-[9px] bg-emerald-100 text-emerald-800 px-2 py-0.5 rounded font-bold uppercase">LESSON REPORT SHIFT</span>
                      <h4 className="font-display font-extrabold text-sm text-gray-900 mt-1">{selectedLessonReport.subject}</h4>
                      <p className="text-[10px] text-gray-500 mt-0.5">Tutor: {tutors.find(t => t.id === selectedLessonReport.tutorId)?.name}</p>
                    </div>
                    <button onClick={() => setSelectedLessonReport(null)} className="text-xs text-gray-400 hover:text-gray-900">Close</button>
                  </div>
                  
                  <div className="space-y-4 text-xs leading-relaxed text-gray-700">
                    <div>
                      <span className="font-bold text-gray-500 uppercase tracking-widest text-[8px] block">Syllabus Covered & Modules</span>
                      <div className="bg-slate-50 p-3 rounded-lg mt-1 font-medium italic text-slate-800">
                        "{selectedLessonReport.lessonReport.covered}"
                      </div>
                    </div>
                    <div>
                      <span className="font-bold text-gray-500 uppercase tracking-widest text-[8px] block">Performance Assessment & Readiness</span>
                      <p className="mt-1">
                        {selectedLessonReport.lessonReport.performance}
                      </p>
                    </div>
                    <div>
                      <span className="font-bold text-gray-500 uppercase tracking-widest text-[8px] block">Homework instructions</span>
                      <p className="mt-1 font-semibold text-blue-600 bg-blue-50/50 p-2.5 rounded-xl border border-blue-100">
                        📋 {selectedLessonReport.lessonReport.homework}
                      </p>
                    </div>
                    
                    {selectedLessonReport.lessonReport.editHistory && selectedLessonReport.lessonReport.editHistory.length > 0 && (
                      <div className="border-t pt-2 mt-3 bg-indigo-50/30 p-2 rounded-lg text-[10px]">
                        <span className="font-bold text-indigo-900 block font-mono">Report Edit Logs:</span>
                        {selectedLessonReport.lessonReport.editHistory.map((log, i) => (
                          <p key={i} className="text-gray-600 italic mt-0.5">{log.date} - "{log.changes}"</p>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              )}

              {/* Start Session Active QR Drawer */}
              {activeBookingForQR && (
                <div className="bg-slate-900 text-white rounded-2xl p-5 shadow-lg max-w-sm text-center">
                  <div className="flex justify-between items-center border-b border-slate-800 pb-3 mb-4">
                    <span className="font-bold text-xs">Awaiting QR Verification</span>
                    <button onClick={() => setActiveBookingForQR(null)} className="text-xs text-slate-400 hover:text-white">Cancel</button>
                  </div>
                  <p className="text-xs text-slate-400">Instruct the student to present this code to the tutor or enter it on screen:</p>
                  
                  <div className="my-5 bg-slate-800 p-4 border border-slate-700 rounded-xl inline-block">
                    {/* Visual QR Simulator */}
                    <div className="w-20 h-20 bg-white border-2 border-slate-50 p-1.5 rounded-lg flex items-center justify-center mx-auto mb-2">
                      <div className="grid grid-cols-4 gap-0.5">
                        {Array.from({ length: 16 }).map((_, i) => (
                          <div key={i} className={`w-3 h-3 ${i % 3 === 0 ? 'bg-black': 'bg-slate-200'}`}></div>
                        ))}
                      </div>
                    </div>
                    <span className="font-mono font-bold tracking-widest text-lg text-white block">9852 1734</span>
                  </div>

                  <div className="text-[10px] text-slate-400">
                    Expiry timer Remaining <span className="font-bold text-amber-400">{timerRemaining}</span>
                  </div>
                  <button 
                    onClick={() => {
                      onUpdateBookingStatus(activeBookingForQR.id, 'completed');
                      setActiveBookingForQR(null);
                    }}
                    className="mt-4 bg-blue-600 text-xs font-semibold text-white px-3 py-1 rounded-lg hover:bg-blue-700 transition"
                  >
                    Force Complete Session (Simulate QR Scan)
                  </button>
                </div>
              )}

              {/* Active Bookings Log */}
              <div className="bg-white border rounded-2xl p-5 shadow-xs">
                <h3 className="font-display font-bold text-gray-900 text-sm mb-4">Appointments Status Center</h3>
                
                <div className="space-y-4">
                  {bookings.length === 0 ? (
                    <div className="p-8 text-center bg-slate-50/50 rounded-2xl border border-dashed border-gray-200">
                      <div className="mx-auto w-10 h-10 bg-slate-100 rounded-full flex items-center justify-center text-slate-400 mb-2">
                        <Calendar className="h-5 w-5" />
                      </div>
                      <p className="text-xs text-gray-500 font-medium font-sans">No tutorial sessions or matching appointments has been scheduled yet.</p>
                      <button onClick={() => setActiveTab('search')} className="mt-3.5 bg-blue-600 hover:bg-blue-700 text-white px-4 py-1.5 rounded-xl text-xs font-bold transition cursor-pointer shadow-sm">
                        Find & Match Tutors Catalog
                      </button>
                    </div>
                  ) : (
                    bookings.map(booking => {
                      const tutor = tutors.find(t => t.id === booking.tutorId);
                      return (
                      <div key={booking.id} className="p-4 border border-slate-100 rounded-xl bg-slate-50/50 flex flex-col gap-3">
                        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                          <div className="flex items-start gap-3">
                            {tutor && <img src={tutor.imageUrl} alt={tutor.name} className="w-10 h-10 rounded-full object-cover border" />}
                            <div>
                              <span className="text-[9px] bg-slate-200 text-slate-800 px-2 py-0.5 rounded font-mono font-bold uppercase">{booking.subject}</span>
                              <h4 className="font-bold text-gray-950 text-xs mt-1.5">Tutor: {tutor?.name || 'Tutor'}</h4>
                              <p className="text-[11px] text-gray-500 flex items-center mt-0.5"><Clock className="h-3 w-3 mr-1" /> {booking.date} • {booking.time} • Duration: {booking.durationHours}H</p>
                            </div>
                          </div>

                          {/* Booking actions depending on state */}
                          <div className="flex flex-col items-end gap-2 shrink-0">
                            <div className="flex items-center gap-2">
                              {booking.status === 'pending' && <span className="text-[9px] bg-amber-50 text-amber-700 font-bold border border-amber-200 rounded px-2 py-0.2">PENDING TUTOR CONFIRMATION</span>}
                              {booking.status === 'confirmed' && <span className="text-[9px] bg-emerald-50 text-emerald-700 font-bold border border-emerald-200 rounded px-2 py-0.2 animate-pulse">CLASS CONFIRMED</span>}
                              {booking.status === 'countered' && <span className="text-[9px] bg-blue-50 text-blue-700 font-bold border border-blue-200 rounded px-2 py-0.2">PROPOS_AJDUSTED</span>}
                              {booking.status === 'completed' && <span className="text-[9px] bg-slate-200 text-slate-800 font-bold rounded px-2 py-0.2">COMPLETED</span>}
                              {booking.status === 'cancelled' && <span className="text-[9px] bg-red-100 text-red-800 font-bold rounded px-2 py-0.2">CANCELLED</span>}
                            </div>

                            <div className="flex gap-2">
                              {booking.status === 'countered' && booking.counterProposal && (
                                <div className="bg-amber-50 border p-3.5 rounded-xl text-left max-w-sm mt-1 sm:col-span-2">
                                  <p className="text-[10px] text-amber-800 font-bold tracking-wide">Tutor proposed alternative:</p>
                                  <p className="font-bold text-xs text-gray-800 mt-1">{booking.counterProposal.date} @ {booking.counterProposal.time}</p>
                                  <p className="text-[11px] text-gray-600 italic mt-1 bg-white p-2 rounded">"{booking.counterProposal.message}"</p>
                                  <div className="mt-3 flex gap-2">
                                    <button onClick={() => onUpdateBookingStatus(booking.id, 'cancelled')} className="bg-white border hover:bg-red-50 text-red-600 text-[10px] font-bold px-3 py-1 rounded-lg">Decline</button>
                                    <button onClick={() => onUpdateBookingStatus(booking.id, 'confirmed')} className="bg-blue-600 text-white hover:bg-blue-700 text-[10px] font-bold px-3 py-1 rounded-lg">Accept Proposal</button>
                                  </div>
                                </div>
                              )}

                              {booking.status === 'confirmed' && (() => {
                                const linkedInvoice = invoices.find(inv => inv.bookingId === booking.id);
                                const isPaid = linkedInvoice?.status === 'Paid';
                                return (
                                  <>
                                    {isPaid ? (
                                      <>
                                        <button onClick={() => setReportBookingId(booking.id)} className="text-[11px] font-bold text-red-600 hover:underline px-2.5 py-1 rounded">Report Issue</button>
                                        <button onClick={() => setActiveBookingForQR(booking)} className="bg-blue-600 text-white rounded-lg text-[10.5px] font-bold px-3.5 py-1 shadow-xs hover:bg-blue-700 transition cursor-pointer">Start QR (Scan)</button>
                                      </>
                                    ) : (
                                      <div className="flex items-center gap-1.5 text-[10px] text-amber-700 bg-amber-50 px-2.5 py-1 rounded-lg border border-amber-250/20 font-medium">
                                        <Lock className="h-3 w-3 text-amber-600 shrink-0" />
                                        <span>Unlock QR and Report upon payment</span>
                                      </div>
                                    )}
                                  </>
                                );
                              })()}

                              {booking.status === 'completed' && booking.lessonReport && (
                                <button onClick={() => setSelectedLessonReport(booking)} className="bg-slate-100 hover:bg-blue-50 hover:text-blue-600 text-gray-700 font-bold text-[10.5px] px-3 py-1.5 rounded-lg flex items-center gap-1">
                                  <FileText className="h-3.5 w-3.5" /> View Lesson Report
                                </button>
                              )}
                            </div>
                          </div>
                        </div>

                        {/* Inline Invoice & Payment Module for Confirmed Classes */}
                        {booking.status === 'confirmed' && (() => {
                          const linkedInvoice = invoices.find(inv => inv.bookingId === booking.id);
                          if (!linkedInvoice) return null;
                          return (
                            <div className={`mt-1 p-3 rounded-xl border flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 ${
                              linkedInvoice.status === 'Paid' 
                                ? 'bg-emerald-50/60 border-emerald-100/80 text-emerald-850' 
                                : 'bg-amber-50/80 border-amber-100 text-amber-950 animate-pulse-subtle'
                            }`}>
                              <div className="flex items-center gap-2.5">
                                {linkedInvoice.status === 'Paid' ? (
                                  <div className="p-1 px-1.5 bg-emerald-600 text-white rounded-lg text-xs font-bold shrink-0">✓ PAID</div>
                                ) : (
                                  <div className="p-1 px-1.5 bg-rose-600 text-white rounded-lg text-[10px] font-bold shrink-0">💳 UNPAID</div>
                                )}
                                <div>
                                  <p className="text-[11px] font-bold flex items-center gap-1.5 flex-wrap">
                                    {linkedInvoice.status === 'Paid' ? 'Class Tuition Invoice Settled' : 'Tuition Invoiced - Action Required'}
                                    <span className="text-[9px] font-mono opacity-80 bg-black/5 px-1 rounded">#{linkedInvoice.id}</span>
                                  </p>
                                  <p className="text-[10px] text-gray-500 mt-0.5 leading-normal">
                                    Amount: <span className="font-bold text-gray-800">${linkedInvoice.amount}.00</span> • Direct payment securely processed over LearnSphere peer matching escrow on {booking.date}.
                                  </p>
                                </div>
                              </div>
                              {linkedInvoice.status === 'Unpaid' && (
                                <button
                                  type="button"
                                  onClick={() => onPayInvoice(linkedInvoice.id)}
                                  className="w-full sm:w-auto bg-blue-600 hover:bg-blue-700 text-white text-[11px] font-bold px-3.5 py-2 rounded-xl transition cursor-pointer shadow-sm shrink-0 flex items-center justify-center gap-1"
                                >
                                  💳 Pay Now (${linkedInvoice.amount}.00)
                                </button>
                              )}
                            </div>
                          );
                        })()}
                      </div>
                    );
                  }))}
                </div>
              </div>

            </div>
          )}

          {/* Billing & Receipts Tab */}
          {activeTab === 'billing' && (
            <div className="bg-white border border-gray-100 rounded-2xl p-6 shadow-xs">
              <span className="text-[10px] font-mono text-blue-600 font-bold uppercase">Billing ledger</span>
              <h2 className="font-display font-extrabold text-lg text-gray-950 mt-1">Payments & Receipts History</h2>
              <p className="text-xs text-gray-500 mt-1">Manage active curricula subscriptions, automatic package invoices, and deposit credits.</p>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mt-6">
                <div className="bg-slate-50 border p-5 rounded-2xl h-fit">
                  <p className="text-[9px] text-gray-400 font-bold uppercase tracking-wider">Active matching contract</p>
                  <h4 className="font-display font-bold text-gray-900 text-sm mt-1">Secondary 3 Mathematics Pack</h4>
                  <p className="text-sm font-bold text-blue-600 mt-1">$200.00 / month <span className="text-[10px] text-gray-400 font-light">($50 / hour)</span></p>
                  <p className="text-[10px] text-gray-500 mt-3">Automatic direct charge on next billing period: <strong className="text-gray-800">15 June 2026</strong></p>
                </div>

                <div>
                  <h4 className="font-bold text-[10px] uppercase text-gray-500 tracking-wider mb-3">Deposit Receipts Log</h4>
                  <div className="divide-y divide-gray-100 border rounded-xl overflow-hidden shadow-xs">
                    {invoices.length === 0 ? (
                      <div className="p-8 bg-white text-center text-xs text-gray-500 font-sans">
                        No billing invoices or receipt histories have been registered yet.
                      </div>
                    ) : (
                      invoices.map(inv => (
                        <div key={inv.id} className="p-3 bg-white flex justify-between items-center text-xs hover:bg-slate-50/50 transition duration-150">
                          <div>
                            <p className="font-bold text-gray-800 flex items-center gap-1.5">
                              {inv.date}
                              {inv.subject && <span className="text-[10px] font-semibold text-blue-600 bg-blue-50/60 border border-blue-100 py-0.2 px-1.5 rounded-md">{inv.subject}</span>}
                            </p>
                            <span className="text-[9.5px] text-gray-400 font-mono flex items-center gap-1 flex-wrap">
                              Invoice #{inv.id}
                              {inv.bookingId && <span className="text-[8.5px] font-bold bg-slate-100 text-gray-500 px-1 py-0.2 rounded font-sans border border-slate-200">Session Booking reference</span>}
                            </span>
                          </div>
                          <div className="text-right flex items-center gap-3 shrink-0">
                            <div>
                              <strong className="text-gray-900 block font-bold text-xs">${inv.amount}.00</strong>
                              {inv.status === 'Paid' ? (
                                <span className="bg-emerald-50 text-emerald-800 text-[8.5px] border border-emerald-100 px-1.5 py-0.2 rounded font-bold uppercase block text-center mt-0.5">PAID</span>
                              ) : (
                                <span className="bg-rose-50 text-rose-800 text-[8.5px] border border-rose-100 px-1.5 py-0.2 rounded font-bold uppercase block text-center mt-0.5">UNPAID</span>
                              )}
                            </div>
                            {inv.status === 'Unpaid' && (
                              <button
                                type="button"
                                onClick={() => onPayInvoice(inv.id)}
                                className="bg-rose-600 hover:bg-rose-700 text-white font-bold text-[10.5px] px-3 py-1.5 rounded-lg transition shadow-xs cursor-pointer"
                              >
                                Pay Now
                              </button>
                            )}
                          </div>
                        </div>
                      ))
                    )}
                  </div>
                </div>
              </div>
            </div>
          )}

          {/* Direct Messaging Chat Tab */}
          {activeTab === 'chat' && (
            <div className="bg-white border rounded-2xl overflow-hidden shadow-xs h-[520px] flex flex-col">
              <div className="bg-slate-50 p-4 border-b flex items-center justify-between">
                <div>
                  <h4 className="font-bold text-gray-950 text-xs">Mr. Lim Wei Sheng</h4>
                  <p className="text-[10px] text-emerald-600 flex items-center mt-0.5"><span className="w-1.5 h-1.5 bg-emerald-500 rounded-full mr-1"></span> Math Secondary 3 Class Coordinator • Online</p>
                </div>
                <span className="text-[10px] text-gray-400">Archived thread logs</span>
              </div>

              {/* Check if any payment was made to register chat session */}
              {!invoices.some(inv => inv.status === 'Paid') ? (
                <div className="flex-1 flex flex-col items-center justify-center p-8 bg-slate-50/40 text-center">
                  <div className="w-14 h-14 bg-amber-50 text-amber-600 rounded-2xl flex items-center justify-center mb-4 border border-amber-100 shadow-sm animate-pulse-subtle">
                    <Lock className="h-6 w-6" />
                  </div>
                  <span className="text-[9px] bg-amber-100 text-amber-800 border border-amber-200/65 px-2.5 py-0.5 rounded-md font-bold uppercase tracking-wider font-mono">
                    ⚠️ Escrow Secure Protocol
                  </span>
                  <h3 className="font-display font-extrabold text-gray-950 mt-4 text-xs sm:text-sm tracking-tight">Direct Conversation Locked</h3>
                  <p className="text-[11px] text-gray-500 mt-2 max-w-sm leading-relaxed">
                    Under LearnSphere peer safety compliance guidelines, direct messaging is unlocked instantly **after your initial tuition deposit is processed securely through internal escrow**.
                  </p>

                  <div className="mt-5 p-4 bg-white border rounded-xl shadow-xs max-w-xs w-full text-left">
                    <h5 className="text-[10px] font-bold text-gray-800 border-b pb-1.5 mb-2 uppercase tracking-widest text-center">Outstanding Invoices</h5>
                    {invoices.filter(inv => inv.status === 'Unpaid').length > 0 ? (
                      <div className="space-y-2">
                        {invoices.filter(inv => inv.status === 'Unpaid').map(inv => (
                          <div key={inv.id} className="flex justify-between items-center bg-slate-50 p-2 rounded-lg border text-[10.5px]">
                            <div>
                              <strong className="block font-bold text-gray-850 truncate max-w-[120px]">{inv.subject || 'Tuition Invoice'}</strong>
                              <span className="text-[9px] text-gray-400 font-mono">Invoice #{inv.id}</span>
                            </div>
                            <button
                              onClick={() => onPayInvoice(inv.id)}
                              className="bg-blue-600 hover:bg-blue-700 text-white font-bold text-[9.5px] px-2.5 py-1.5 rounded-lg transition shrink-0"
                            >
                              Settle ${inv.amount}.00
                            </button>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <div className="text-center py-2">
                        <p className="text-[11px] text-gray-400 mb-3">No active invoices. Reserve a class with Mr. Lim Wei Sheng first to trigger payment credentials.</p>
                        <button
                          onClick={() => setActiveTab('search')}
                          className="w-full bg-blue-600 hover:bg-blue-700 text-white font-bold text-[10.5px] py-1.5 rounded-lg transition"
                        >
                          Schedule Session
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              ) : (
                <>
                  {/* Chat Thread */}
                  <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-slate-50/50">
                    {chatMessages.map(msg => (
                      <div key={msg.id} className={`flex flex-col ${msg.sender === 'parent' ? 'items-end' : msg.sender === 'system' ? 'items-center' : 'items-start'}`}>
                        {msg.sender === 'system' ? (
                          <span className="text-[9.5px] bg-blue-50 text-blue-700 leading-snug border border-blue-100/50 rounded-lg px-2.5 py-1 text-center font-semibold font-sans my-2 font-mono">⚠️ LearnSphere Operations: {msg.text}</span>
                        ) : (
                          <div className={`p-3 rounded-2xl max-w-[75%] shadow-xs ${
                            msg.sender === 'parent' ? 'bg-blue-600 text-white rounded-tr-none' : 'bg-white text-gray-800 rounded-tl-none border'
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
                    if (!messageText.trim()) return;
                    onSendChatMessage(messageText);
                    setMessageText('');
                  }} className="p-3 border-t bg-white flex gap-2">
                    <input 
                      type="text" 
                      value={messageText}
                      onChange={(e) => setMessageText(e.target.value)}
                      placeholder="Ask Mr. Lim a question about homework formulas..." 
                      className="flex-1 bg-slate-50 border border-gray-200 rounded-xl px-4 text-xs focus:outline-none focus:ring-1 focus:ring-blue-600 focus:bg-white" 
                    />
                    <button type="submit" className="bg-blue-600 hover:bg-blue-700 text-white rounded-xl px-4 py-2 text-xs font-semibold shrink-0 cursor-pointer flex items-center gap-1">
                      <Send className="h-3 w-3" /> Send Message
                    </button>
                  </form>
                </>
              )}
            </div>
          )}

        </main>
      </div>
    </div>
  );
};
