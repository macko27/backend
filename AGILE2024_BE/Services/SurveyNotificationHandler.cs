
using AGILE2024_BE.Data;
using AGILE2024_BE.Models.Enums;
using AGILE2024_BE.Models.Identity;
using AGILE2024_BE.Models.Survey;
using AGILE2024_BE.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AGILE2024_BE.Helpers;

namespace AGILE2024_BE.Services
{
    public class SurveyNotificationHandler : INotificationHandler
    {
        private readonly AgileDBContext dbContext;
        private readonly IHubContext<NotificationHub> hubContext;

        public SurveyNotificationHandler(AgileDBContext dbContext, IHubContext<NotificationHub> hubContext)
        {
            this.dbContext = dbContext;
            this.hubContext = hubContext;
        }


        public async Task HandleNotificationAsync(CancellationToken cancellationToken)
        {
            //azure pouziva UTC pasmo
            var utcTime = DateTime.UtcNow;

            await ActivateScheduledSurveys(cancellationToken, utcTime);
            await HandleSurveyExpiration(cancellationToken, utcTime);
        }



        //**********************************************************************************
        // Posielanie notifikacii pre ankety ktore su vytvorene do buducnosti
        //**********************************************************************************
        private async Task ActivateScheduledSurveys(CancellationToken cancellationToken, DateTime utcTime)
        {
            //var now = DateTime.Now;
            //var nowTruncated = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            //var surveysToActivate = await dbContext.Surveys
            //    .Include(s => s.Recipients)
            //    .Include(s => s.createdBy)
            //    .Where(s =>
            //        s.status == EnumSurveyState.Neaktívna && EF.Functions.DateDiffMinute(s.start, now) >= 0)
            //    .ToListAsync(cancellationToken);

            

            var surveysToActivate = await dbContext.Surveys
                .Include(s => s.Recipients)
                .Include(s => s.createdBy)
                .Where(s =>
                    s.status == EnumSurveyState.Neaktívna &&
                    s.start <= utcTime)
                .ToListAsync(cancellationToken);

            foreach (var survey in surveysToActivate)
            {
                survey.status = EnumSurveyState.Aktívna;

                var users = await GetSurveyRecipientUsersAsync(survey);

                var notifications = new List<Notification>();

                foreach (var user in users)
                {
                    var notification = new Notification
                    {
                        Id = Guid.NewGuid(),
                        User = user,
                        ReferencedItemId = survey.Id,
                        Message = $"Anketa '{survey.name}' je teraz aktívna. Prosím vyplňte ju.",
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        NotificationType = EnumNotificationType.SurveyAssignedNotificationType
                    };

                    notifications.Add(notification);

                    var response = new NotificationResponse
                    {
                        Id = notification.Id,
                        Message = notification.Message,
                        Title = NotificationHelpers.GetNotificationTitle(notification.NotificationType),
                        ReferencedItem = survey.Id.ToString(),
                        NotificationType = notification.NotificationType,
                        CreatedAt = notification.CreatedAt,
                        IsRead = notification.IsRead
                    };

                    await hubContext.Clients.User(user.Id)
                        .SendAsync("ReceiveNotification", response, cancellationToken);
                }

                if (notifications.Any())
                {
                    dbContext.Notifications.AddRange(notifications);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }



        //**********************************************************************************
        // Ak ankete vyprsi cas tak je uavreta a je poslana notifikacia
        //**********************************************************************************
        private async Task HandleSurveyExpiration(CancellationToken cancellationToken, DateTime utcTime)
        {
            //var now = DateTime.Now;
            //var nowTruncated = new DateTime(
            //    now.Year, now.Month, now.Day,
            //    now.Hour, now.Minute, 0);

            //var expiredSurveys = await dbContext.Surveys
            //    .Include(s => s.createdBy)
            //        .ThenInclude(c => c.User)
            //    .Where(s =>
            //        s.status == EnumSurveyState.Aktívna && EF.Functions.DateDiffMinute(s.end, nowTruncated) >= 0)
            //    .ToListAsync(cancellationToken);


            var expiredSurveys = await dbContext.Surveys
                .Include(s => s.createdBy)
                    .ThenInclude(c => c.User)
                .Where(s =>
                    s.status == EnumSurveyState.Aktívna &&
                    s.end <= utcTime)
                .ToListAsync(cancellationToken);

            foreach (var survey in expiredSurveys)
            {
                var alreadyNotified = await dbContext.Notifications
                    .AnyAsync(n =>
                        n.ReferencedItemId == survey.Id &&
                        n.NotificationType == EnumNotificationType.SurveyExpiredNotificationType,
                        cancellationToken);

                if (alreadyNotified)
                    continue;

                survey.status = EnumSurveyState.Uzavretá;

                var creatorUser = survey.createdBy.User;
                if (creatorUser == null)
                    continue;

                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    User = creatorUser,
                    ReferencedItemId = survey.Id,
                    Message = $"Platnosť ankety '{survey.name}' uplynula a anketa bola automaticky uzavretá.",
                    CreatedAt = DateTime.Now,
                    IsRead = false,
                    NotificationType = EnumNotificationType.SurveyExpiredNotificationType
                };

                dbContext.Notifications.Add(notification);
                await dbContext.SaveChangesAsync(cancellationToken);

                await SendRealtimeNotification(notification, creatorUser.Id, cancellationToken);
            }
        }



        //**********************************************************************************
        // pocet vsetkych prijemcov
        //**********************************************************************************
        private async Task<int> CalculateTotalRecipientsAsync(Survey survey)
        {
            var users = await GetSurveyRecipientUsersAsync(survey);
            return users.Count;
        }



        //**********************************************************************************
        // pocet vsetkych prijemcov co zahlasovali
        //**********************************************************************************
        private async Task<int> GetTotalThatSubmittedVote(Survey survey)
        {
            return await dbContext.SurveyAnswers
                .Where(a => a.survey.Id == survey.Id)
                .Select(a => a.user.Id)
                .Distinct()
                .CountAsync();
        }



        //**********************************************************************************
        // poslanie notifikacie real-time
        //**********************************************************************************
        private async Task SendRealtimeNotification(Notification notification, string userId, CancellationToken cancellationToken)
        {
            var response = new NotificationResponse
            {
                Id = notification.Id,
                Message = notification.Message,
                Title = NotificationHelpers.GetNotificationTitle(notification.NotificationType),
                ReferencedItem = notification.ReferencedItemId.ToString(),
                NotificationType = notification.NotificationType,
                CreatedAt = notification.CreatedAt,
                IsRead = notification.IsRead
            };

            await hubContext.Clients.User(userId)
                .SendAsync("ReceiveNotification", response, cancellationToken);
        }



        //**********************************************************************************
        // ziskanie vsetkych unikatnych prijemcov
        //**********************************************************************************
        private async Task<List<ExtendedIdentityUser>> GetSurveyRecipientUsersAsync(Survey survey)
        {
            var userList = new List<ExtendedIdentityUser>();

            foreach (var r in survey.Recipients)
            {
                if (r.Type == "employee")
                {
                    var user = await dbContext.EmployeeCards
                        .Include(e => e.User)
                        .Where(e => e.Id == r.EmployeeCardId)
                        .Select(e => e.User)
                        .FirstOrDefaultAsync();

                    if (user != null)
                        userList.Add(user);
                }
                else if (r.Type == "department")
                {
                    var departmentUsers = await dbContext.EmployeeCards
                        .Include(e => e.User)
                        .Where(e => e.Department.Id == r.EmployeeCardId)
                        .Select(e => e.User)
                        .ToListAsync();

                    userList.AddRange(departmentUsers);
                }
            }

            return userList
                .GroupBy(u => u.Id)
                .Select(g => g.First())
                .ToList();
        }
    }

}

