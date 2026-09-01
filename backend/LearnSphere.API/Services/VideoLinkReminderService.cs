using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using LearnSphere.API.Data;
using LearnSphere.API.Models;
using Microsoft.EntityFrameworkCore;

namespace LearnSphere.API.Services;

// Polls every 15 minutes for confirmed Online bookings still missing a video
// conference link and nudges the tutor (in-app notification + email) at 24h,
// 6h, and then roughly hourly until the class starts. Registered as a
// singleton hosted service — AppDbContext and IEmailService are both scoped,
// so each poll resolves them through a fresh IServiceScope rather than
// injecting them into the constructor (see CheckAndSendRemindersAsync).
public class VideoLinkReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VideoLinkReminderService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    // VideoLinkReminderStatus (none -> 24h_sent -> 6h_sent) is the durable,
    // persisted record of the 24h/6h milestones — it survives a restart. Once a
    // booking reaches 6h_sent, the spec calls for roughly hourly nudges on top of
    // that with no further status change, but the schema has no per-reminder
    // timestamp column to pace those against. Tracked here instead, in-memory,
    // per booking id: good enough for real ~60-minute spacing during a single
    // process's lifetime, and simply resumes (next poll just sends one) after a
    // restart rather than needing a migration for a timestamp column.
    private readonly ConcurrentDictionary<int, DateTime> _lastHourlySentUtc = new();

    public VideoLinkReminderService(IServiceScopeFactory scopeFactory, ILogger<VideoLinkReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VideoLinkReminderService poll failed");
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (TaskCanceledException) { }
        }
    }

    private async Task CheckAndSendRemindersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var bookings = await context.Bookings
            .Include(b => b.Tutor).ThenInclude(t => t.User)
            .Include(b => b.Classes)
            .Where(b => b.Status == "confirmed" && b.Mode == "Online"
                && (b.VideoConferenceLink == null || b.VideoConferenceLink == ""))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var booking in bookings)
        {
            var nextSession = GetNextSessionUtc(booking);
            if (nextSession == null) continue;

            var hoursUntil = (nextSession.Value - now).TotalHours;
            if (hoursUntil <= 0) continue; // class has started — stop reminding

            string? newStatus = null;
            var isFirst24h = false;

            if (booking.VideoLinkReminderStatus == "none" && hoursUntil <= 24)
            {
                newStatus = "24h_sent";
                isFirst24h = true;
            }
            else if (booking.VideoLinkReminderStatus == "24h_sent" && hoursUntil <= 6)
            {
                newStatus = "6h_sent";
            }
            else if (booking.VideoLinkReminderStatus == "6h_sent")
            {
                var lastSent = _lastHourlySentUtc.TryGetValue(booking.Id, out var t) ? t : (DateTime?)null;
                if (lastSent != null && now - lastSent.Value < TimeSpan.FromMinutes(55)) continue;
            }
            else
            {
                continue; // not yet due for its next milestone
            }

            var tutor = booking.Tutor!;
            var tutorName = tutor.User?.Name ?? "Tutor";
            var tutorEmail = tutor.User?.Email;
            var timeLabel = FormatHoursUntil(hoursUntil);

            string subject, body, notifTitle;
            if (isFirst24h)
            {
                subject = $"Action required: Video conference link for your {booking.Subject} class";
                body = $"Hi {tutorName},\n\nYour online {booking.Subject} class is scheduled in approximately 24 hours.\nPlease log in to LearnSphere and add your video conference link before the class begins.\n\nRegards,\nLearnSphere Team";
                notifTitle = "Video conference link needed";
            }
            else
            {
                subject = $"Urgent: Missing video conference link for your {booking.Subject} class";
                body = $"Hi {tutorName},\n\nYour online {booking.Subject} class is starting in approximately {timeLabel} and your video conference link is still missing.\nPlease log in immediately and add your link.\n\nRegards,\nLearnSphere Team";
                notifTitle = "Urgent: video conference link missing";
            }

            context.Notifications.Add(new Notification
            {
                UserId = tutor.UserId,
                Title = notifTitle,
                Message = $"Your {booking.Subject} class starts in {timeLabel} — please add your video conference link.",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd hh:mm tt"),
                Type = "system",
                IsRead = false
            });

            if (!string.IsNullOrWhiteSpace(tutorEmail))
            {
                try { await emailService.SendAsync(tutorEmail, subject, body); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to send video link reminder email to {Email}", tutorEmail); }
            }

            if (newStatus != null) booking.VideoLinkReminderStatus = newStatus;
            if (booking.VideoLinkReminderStatus == "6h_sent") _lastHourlySentUtc[booking.Id] = now;
            changed = true;
        }

        if (changed) await context.SaveChangesAsync(ct);
    }

    // Mirrors the frontend's combineDateTime() helper (tutor.controller.js /
    // parent.controller.js) — pulls the first "H:MM AM/PM" out of a
    // BookingClass.Time string, which works whether Time is a single start time
    // (legacy parent-offer bookings) or a "start - end" range (preset bookings),
    // combined with Date (stored "YYYY-MM-DD"). Returns the earliest class on the
    // booking that's still ahead of now, across every BookingClass row — both
    // booking flows populate BookingClass, so no separate PresetSlots handling
    // is needed here.
    private static DateTime? GetNextSessionUtc(Booking booking)
    {
        DateTime? next = null;
        foreach (var c in booking.Classes)
        {
            if (!DateTime.TryParse(c.Date, out var datePart)) continue;
            var dt = datePart.Date;
            var m = Regex.Match(c.Time ?? "", @"(\d{1,2}):(\d{2})\s*(AM|PM)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var h = int.Parse(m.Groups[1].Value);
                var min = int.Parse(m.Groups[2].Value);
                var ampm = m.Groups[3].Value.ToUpperInvariant();
                if (ampm == "PM" && h != 12) h += 12;
                if (ampm == "AM" && h == 12) h = 0;
                dt = dt.AddHours(h).AddMinutes(min);
            }
            if (dt >= DateTime.UtcNow && (next == null || dt < next.Value)) next = dt;
        }
        return next;
    }

    private static string FormatHoursUntil(double hoursUntil)
    {
        if (hoursUntil < 1) return Math.Max(1, (int)Math.Round(hoursUntil * 60)) + " minutes";
        var h = (int)Math.Round(hoursUntil);
        return h + (h == 1 ? " hour" : " hours");
    }
}
