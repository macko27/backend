using AGILE2024_BE.Models.Requests;
using AGILE2024_BE.Models.Response;
using AGILE2024_BE.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using AGILE2024_BE.Data;
using AGILE2024_BE.Models.Identity;
using AGILE2024_BE.Models.Requests.Review;
using Microsoft.EntityFrameworkCore;
using AGILE2024_BE.Models;
using Azure.Core;

namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReviewController : ControllerBase
    {
        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;

        public ReviewController(UserManager<ExtendedIdentityUser> um, IConfiguration co, RoleManager<IdentityRole> rm, AgileDBContext db)
        {
            this.userManager = um;
            this.config = co;
            this.roleManager = rm;
            this.dbContext = db;
        }

        [HttpGet("Count")]
        public async Task<IActionResult> GetReviewCount()
        {
            try
            {
                int reviewCount = await dbContext.Reviews.CountAsync();
                return Ok(reviewCount);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Chyba servera pri získavaní počtu posudkov.");
            }
        }

        [HttpPost("CreateReview")]
        public async Task<IActionResult> CreateReview([FromBody] ReviewCreateRequest request)
        {
            if (request == null || request.employeeIds == null || !request.employeeIds.Any())
            {
                return BadRequest("Invalid request data");
            }

            var senderUser = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == request.SenderId.ToString());

            if (senderUser == null)
            {
                return BadRequest("Sender user not found");
            }

            var senderEmployeeCard = await dbContext.EmployeeCards
                .FirstOrDefaultAsync(ec => ec.User.Id == senderUser.Id);

            if (senderEmployeeCard == null)
            {
                return BadRequest("Employee card for sender not found");
            }

            Guid idReview = Guid.NewGuid();
            var review = new Review
            {
                id = idReview,
                sender = senderEmployeeCard,
                counter = await dbContext.Reviews.CountAsync() + 1,
                employeeEndDate = request.EmployeeDeadline,
                superiorEndDate = request.SuperiorDeadline,
                createDate = DateTime.UtcNow,
                endDate = null,
            };

            dbContext.Reviews.Add(review);
            await dbContext.SaveChangesAsync();

            var goalAssignments = await dbContext.GoalAssignments
                .Where(ga => request.employeeIds.Contains(ga.employee.Id.ToString()))
                .ToListAsync();

            if (!goalAssignments.Any())
            {
                return BadRequest("No GoalAssignments found for the provided employee IDs.");
            }

            var reviewRecipients = new List<ReviewRecipient>();
            var reviewQuestions = new List<ReviewQuestion>();

            foreach (var ga in goalAssignments)
            {
                var reviewRecipient = new ReviewRecipient
                {
                    id = Guid.NewGuid(),
                    review = review,
                    goalAssignment = ga,
                    superiorDescription = null,
                    employeeDescription = null,
                    isSavedSuperiorDesc = false,
                    isSavedEmployeeDesc = false,
                    isSentSuperiorDesc = false,
                    isSentEmployeeDesc = false
                };

                reviewRecipients.Add(reviewRecipient);

                var reviewQuestion = new ReviewQuestion
                {
                    id = Guid.NewGuid(),
                    goalAssignment = reviewRecipient,
                    superiorDescription = null,
                    employeeDescription = null,
                    isSavedSuperiorDesc = false,
                    isSavedEmployeeDesc = false,
                    isSentSuperiorDesc = false,
                    isSentEmployeeDesc = false,

                };

                reviewQuestions.Add(reviewQuestion);
            }


            dbContext.ReviewRecipents.AddRange(reviewRecipients);
            dbContext.ReviewQuestions.AddRange(reviewQuestions);
            await dbContext.SaveChangesAsync();

            return Ok(new { message = "Review created successfully" });
        }


        [HttpGet("GetReviews")]
        public async Task<IActionResult> GetAllReviews()
        {
            ExtendedIdentityUser? user = await userManager.FindByEmailAsync(User.Identity?.Name!);

            var loggedEmployee = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Where(ec => ec.User.Id == user.Id).FirstOrDefaultAsync();


            if (loggedEmployee == null)
                return NotFound("Zamestnanec nenájdený.");

            var reviewRecipents = await dbContext.ReviewRecipents
                .Include(r => r.review)
                .Include(r => r.goalAssignment)
                .ThenInclude(ga => ga.employee)
                .ThenInclude(emp => emp.User)
                .Where(emp => emp.review.sender.Id == loggedEmployee.Id)
                .ToListAsync();

            var groupedReviews = reviewRecipents
                .GroupBy(rr => rr.review.id)
               .Select(group => new
               {
                   id = group.Key,
                   ReviewId = group.Key,
                   ReviewName = $"Posudok č. PC{group.FirstOrDefault()?.review.counter:D8}",
                   Status = group.FirstOrDefault()?.review.endDate.HasValue == true ? "Dokončený" : "Prebieha",
                   AssignedEmployees = group
                        .Select(rr => new
                        {
                            id = rr.goalAssignment?.employee.Id,
                            name = rr.goalAssignment?.employee.User.Name + " " + rr.goalAssignment?.employee.User.Surname
                        })
                        .DistinctBy(emp => emp.id)
                        .ToList(),
                   CreatedAt = group.FirstOrDefault()?.review.createDate,
                   CompletedAt = group.FirstOrDefault()?.review.endDate,

               })
               .ToList();

            return Ok(groupedReviews);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetReviewById(Guid id)
        {
            var reviewRecipents = await dbContext.ReviewRecipents
                .Include(r => r.review)
                .ThenInclude(s => s.sender)
                .ThenInclude(u => u.User)
                .Include(r => r.goalAssignment)
                .ThenInclude(ga => ga.employee)
                .ThenInclude(emp => emp.User)
                .Where(r => r.review.id == id)
                .ToListAsync();

            if (!reviewRecipents.Any())
            {
                return NotFound("Review not found");
            }

            var review = reviewRecipents.First().review;

            var reviewDetails = new
            {
                id = review.id,
                ReviewId = review.id,
                ReviewName = $"Posudok č. PC{review.counter:D8}",
                SuperiorName = review.sender.User.Name + " " + review.sender.User.Surname,
                Status = review.endDate.HasValue ? "Dokončený" : "Prebieha",
                AssignedEmployees = reviewRecipents
                    .Select(rr => new
                    {
                        id = rr.goalAssignment?.employee.Id,
                        name = rr.goalAssignment?.employee.User.Name + " " + rr.goalAssignment?.employee.User.Surname
                    })
                    .DistinctBy(emp => emp.id)
                    .ToList(),
                CreatedAt = review.createDate,
                CompletedAt = review.endDate,
                EmployeeEndDate = review.employeeEndDate,
                SuperiorEndDate = review.superiorEndDate
            };

            return Ok(reviewDetails);

        }

        [HttpPut("Update/{id}")]
        public async Task<IActionResult> UpdateReview(Guid id, [FromBody] ReviewUpdateRequests request)
        {
            try
            {
                var review = await dbContext.Reviews
                    .FirstOrDefaultAsync(r => r.id == id);

                if (review == null)
                {
                    return NotFound("Review not found");
                }
                if (!string.IsNullOrEmpty(request.EmployeeEndDate))
                {
                    review.employeeEndDate = DateTime.Parse(request.EmployeeEndDate);
                }

                if (!string.IsNullOrEmpty(request.SuperiorEndDate))
                {
                    review.superiorEndDate = DateTime.Parse(request.SuperiorEndDate);
                }
                if (request.Action == "finish")
                {
                    review.endDate = DateTime.UtcNow;
                }
                dbContext.Reviews.Update(review);
                await dbContext.SaveChangesAsync();

                return Ok(new { message = "Review updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while updating the review.");
            }
        }


        [HttpGet("GetReviewText/{userId}/{reviewId}/{employeeId}")]
        public async Task<IActionResult> GetReviewText(Guid userId, Guid reviewId, Guid employeeId)
        {
            try
            {

                var userRoleName = await GetUserRoleAsync(userId);
                bool isSuperior = userRoleName == "Vedúci zamestnanec";

                var reviewRecipients = await dbContext.ReviewRecipents
                    .Include(rr => rr.goalAssignment)
                        .ThenInclude(ga => ga.goal)
                    .Include(rr => rr.review)
                    .Where(rr => rr.review.id == reviewId && rr.goalAssignment.employee.Id == employeeId)
                        .ToListAsync();

                if (reviewRecipients == null)
                {
                    return NotFound("ReviewRecipient not found.");
                }

                var recipientIds = reviewRecipients.Select(rr => rr.id).ToList();

                var reviewQuestions = await dbContext.ReviewQuestions
                    .Where(rq => recipientIds.Contains(rq.goalAssignment.id))
                    .ToListAsync();

                if (reviewQuestions == null)
                {
                    return NotFound("reviewQuestion not found.");
                }

                var response = reviewRecipients.Select(rr =>
                {
                    var reviewQuestion = reviewQuestions.FirstOrDefault(rq => rq.goalAssignment.id == rr.id);

                    return new
                    {
                        reviewRecipientId = rr.id,
                        reviewId = rr.review.id,
                        goalId = rr.goalAssignment.goal.id,
                        goalName = rr.goalAssignment.goal.name,

                        employeeRecDescription = isSuperior
                            ? (rr.isSentEmployeeDesc ? rr.employeeDescription ?? " " : " ")
                            : (rr.isSavedEmployeeDesc || rr.isSentEmployeeDesc ? rr.employeeDescription ?? " " : " "),

                        superiorRecDescription = isSuperior
                            ? (rr.isSavedSuperiorDesc || rr.isSentSuperiorDesc ? rr.superiorDescription ?? " " : " ")
                            : (rr.isSentSuperiorDesc ? rr.superiorDescription ?? " " : " "),

                        employeeQuestionDescription = reviewQuestion != null
                            ? (isSuperior
                                ? (reviewQuestion.isSentEmployeeDesc ? reviewQuestion.employeeDescription ?? " " : " ")
                                : (reviewQuestion.isSavedEmployeeDesc || reviewQuestion.isSentEmployeeDesc ? reviewQuestion.employeeDescription ?? " " : " "))
                            : " ",

                        superiorQuestionDescription = reviewQuestion != null
                            ? (isSuperior
                                ? (reviewQuestion.isSavedSuperiorDesc || reviewQuestion.isSentSuperiorDesc ? reviewQuestion.superiorDescription ?? " " : " ")
                                : (reviewQuestion.isSentSuperiorDesc ? reviewQuestion.superiorDescription ?? " " : " "))
                            : " "
                    };
                });

                return Ok(response);


            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while fetching the review text.");
            }
        }

        [HttpGet("MyReviews")]
        [Authorize(Roles = RolesDef.Zamestnanec + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> GetEmployeeReviews()
        {
            ExtendedIdentityUser? user = await userManager.FindByEmailAsync(User.Identity?.Name!);

            var loggedEmployee = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Where(ec => ec.User.Id == user.Id).FirstOrDefaultAsync();


            if (loggedEmployee == null)
                return NotFound("Zamestnanec nenájdený.");



            var reviewRecipients = await dbContext.ReviewRecipents
                    .Include(rr => rr.review)
                    .Include(rr => rr.goalAssignment)
                        .ThenInclude(ga => ga.employee)
                        .ThenInclude(emp => emp.User)
                    .Where(rr => rr.goalAssignment.employee.Id == loggedEmployee.Id)
                    .ToListAsync();

            if (!reviewRecipients.Any())
                return NotFound("Neboli nájdené žiadne posudky pre tohto zamestnanca.");

            var employeeReviews = reviewRecipients
                .GroupBy(rr => rr.review.id)
                .Select(group => new
                {
                    id = group.Key,
                    ReviewId = group.Key,
                    ReviewName = $"Posudok č. PC{group.FirstOrDefault()?.review.counter:D8}",
                    Status = group.FirstOrDefault()?.review.endDate.HasValue == true ? "Dokončený" : "Prebieha",
                    AssignedEmployees = new
                    {
                        id = loggedEmployee.Id,
                        name = loggedEmployee.User.Name,
                        surname = loggedEmployee.User.Surname
                    },
                    CreatedAt = group.FirstOrDefault()?.review.createDate,
                    CompletedAt = group.FirstOrDefault()?.review.endDate
                })
                .ToList();

            return Ok(employeeReviews);
        }



        [HttpPut("UpdateDescription/{userId}/{reviewId}/{goalId}/{employeeId}")]
        public async Task<IActionResult> UpdateDescription(Guid userId, Guid reviewId, Guid goalId, Guid employeeId, [FromBody] UpdateDescriptionRequest request)
        {
            try
            {
                var userRoleName = await GetUserRoleAsync(userId);

                bool isSuperior = userRoleName == "Vedúci zamestnanec";
                var reviewRecipient = await dbContext.ReviewRecipents
                    .Include(rr => rr.goalAssignment)
                    .ThenInclude(g => g.employee)
                    .FirstOrDefaultAsync(rr => rr.review.id == reviewId && rr.goalAssignment.goal.id == goalId && rr.goalAssignment.employee.Id == employeeId);

                if (reviewRecipient == null)
                {
                    return NotFound("Review recipient not found for this goal.");
                }

                var reviewQuestion = await dbContext.ReviewQuestions
                   .Include(rq => rq.goalAssignment)
                   .FirstOrDefaultAsync(rq => rq.goalAssignment.id == reviewRecipient.id);


                if (reviewQuestion == null)
                {
                    return NotFound("Review question not found for this goal.");
                }


                // Ak je superiorDescription
                if (isSuperior)
                {
                    reviewRecipient.superiorDescription = request.SuperiorDescription;
                    reviewRecipient.isSavedSuperiorDesc = true;
                    reviewQuestion.superiorDescription = request.SuperiorQuestion;
                    reviewQuestion.isSavedSuperiorDesc = true;
                }
                // Ak je employeeDescription
                else if (!isSuperior)
                {
                    reviewRecipient.employeeDescription = request.EmployeeDescription;
                    reviewRecipient.isSavedEmployeeDesc = true;
                    reviewQuestion.employeeDescription = request.EmployeeQuestion;
                    reviewQuestion.isSavedEmployeeDesc = true;
                }
                else
                {
                    return BadRequest("Invalid description type.");
                }

                dbContext.ReviewRecipents.Update(reviewRecipient);
                await dbContext.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while updating the description.");
            }
        }





        [HttpPut("SendDescription/{userId}/{reviewId}/{goalId}/{employeeId}")]
        public async Task<IActionResult> SendDescription(Guid userId, Guid reviewId, Guid goalId, Guid employeeId, [FromBody] UpdateDescriptionRequest request)
        {
            try
            {
                Console.WriteLine("\n-----------------------------------------------------------\n");
                Console.WriteLine("\n\n" + request.SuperiorDescription);
                Console.WriteLine("\n" + request.EmployeeDescription);
                Console.WriteLine("\n" + request.SuperiorQuestion);
                Console.WriteLine("\n" + request.EmployeeQuestion);
                
                var userRoleName = await GetUserRoleAsync(userId);

                bool isSuperior = userRoleName == "Vedúci zamestnanec";

                var reviewRecipient = await dbContext.ReviewRecipents
                    .Include(rr => rr.goalAssignment)
                    .ThenInclude(g => g.employee)
                    .FirstOrDefaultAsync(rr => rr.review.id == reviewId && rr.goalAssignment.goal.id == goalId && rr.goalAssignment.employee.Id == employeeId);

                if (reviewRecipient == null)
                {
                    return NotFound("Review recipient not found for this goal.");
                }

                Console.WriteLine(reviewRecipient);

                var reviewQuestion = await dbContext.ReviewQuestions
                    .Include(rq => rq.goalAssignment)
                    .FirstOrDefaultAsync(rq => rq.goalAssignment.id == reviewRecipient.id);

                Console.WriteLine(reviewQuestion);

                if (reviewQuestion == null)
                {
                    return NotFound("Review question not found for this goal.");
                }

                if (isSuperior)
                {
                    reviewRecipient.superiorDescription = request.SuperiorDescription;
                    reviewRecipient.isSentSuperiorDesc = true;
                    reviewQuestion.superiorDescription = request.SuperiorQuestion;
                    reviewQuestion.isSentSuperiorDesc = true;

                }
                // Ak je employeeDescription
                else if (!isSuperior)
                {
                    reviewRecipient.employeeDescription = request.EmployeeDescription;
                    reviewRecipient.isSentEmployeeDesc = true;
                    reviewQuestion.employeeDescription = request.EmployeeQuestion;
                    reviewQuestion.isSentEmployeeDesc = true;
                }
                else
                {
                    return BadRequest("Invalid description type.");
                }

                dbContext.ReviewRecipents.Update(reviewRecipient);
                dbContext.ReviewQuestions.Update(reviewQuestion);
                await dbContext.SaveChangesAsync();

                return Ok(new { message = "Descriptions sent successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "An error occurred while sending the descriptions.");
            }
        }

        private async Task<string> GetUserRoleAsync(Guid userId)
        {
            var user = await userManager.FindByEmailAsync(User.Identity?.Name!);

            if (user == null)
            {
                throw new Exception("User not found");
            }

            var userRoleId = await dbContext.UserRoles
                .Where(ur => ur.UserId == userId.ToString())
                .Select(ur => ur.RoleId)
                .FirstOrDefaultAsync();

            var userRoleName = await dbContext.Roles
                .Where(r => r.Id == userRoleId)
                .Select(r => r.Name)
                .FirstOrDefaultAsync();

            return userRoleName;
        }

        [HttpGet("GetPendingReview")]
        public async Task<IActionResult> GetPendingFeedback()
        {
            var user = await userManager.FindByEmailAsync(User.Identity?.Name!);
            if (user == null) return BadRequest("User not found.");

            var employeeCard = await dbContext.EmployeeCards
                .FirstOrDefaultAsync(ec => ec.User.Id == user.Id);
            if (employeeCard == null) return BadRequest("Employee card not found.");

            var isVeduci = (await userManager.GetUsersInRoleAsync(RolesDef.Veduci)).Any(u => u.Id == user.Id);


            // Získanie požiadaviek a spätnej väzby
            var reviewRecipients = await GetReviewRecipients(employeeCard.Id);
            var unreadFeedback = await GetUnreadFeedback(employeeCard.Id);

            // Spracovanie review recipients
            var savedButNotSent = FilterReviewRecipients(reviewRecipients, true, isVeduci);
            var notSavedAndNotSent = FilterReviewRecipients(reviewRecipients, false, isVeduci);

            var uniqueReviewIds = GetDistinctReviewIds(savedButNotSent);
            var uniqueReviewIds2 = GetDistinctReviewIds(notSavedAndNotSent);
            var unreadReviewIds = unreadFeedback.Select(f => f.id).ToList();

            // Zostavenie správ pre frontend
            var messages = new List<object>();
            if (uniqueReviewIds.Any())
                messages.Add(CreateMessage(3, uniqueReviewIds, "V sekcii “Posudzovanie cieľov” máte rozpracovaný 1 posudok. \nPre dokončenie operácie je potrebné posudok uložiť a odoslať."));

            if (uniqueReviewIds2.Any())
                messages.Add(CreateMessage(2, uniqueReviewIds2, "Bola Vám priradená požiadavka pre vypracovanie posudku cieľa. \nPosudok cieľa môžete vypracovať v sekcii “Posudzovanie cieľov”."));

            if (unreadReviewIds.Any())
                messages.Add(CreateMessage(1, unreadReviewIds, "Bola Vám priradená požiadavka pre vypísanie spätnej väzby. Spätnú väzbu môžete vypísať v sekcii “Spätná väzba”."));

            return Ok(messages);
        }
        private async Task<List<ReviewRecipient>> GetReviewRecipients(Guid employeeId)
        {
            return await dbContext.ReviewRecipents
                .Include(rr => rr.review)
                .Include(rr => rr.goalAssignment)
                .ThenInclude(ga => ga.employee)
                .Where(rr => rr.goalAssignment.employee.Id == employeeId || rr.review.sender.Id == employeeId)
                .ToListAsync();
        }
        private async Task<List<FeedbackRecipient>> GetUnreadFeedback(Guid employeeId)
        {
            return await dbContext.FeedbackRecipients
                .Include(f => f.employee)
                .Where(f => f.employee.Id == employeeId && f.isRead == false)
                .ToListAsync();
        }
        /* private List<ReviewRecipient> FilterReviewRecipients(List<ReviewRecipient> recipients, bool saved)
         {
             return recipients.Where(rr =>
                 (saved && ((rr.isSavedSuperiorDesc && !rr.isSentSuperiorDesc) || (rr.isSavedEmployeeDesc && !rr.isSentEmployeeDesc))) ||
                 (!saved && ((!rr.isSavedSuperiorDesc && !rr.isSentSuperiorDesc) || (!rr.isSavedEmployeeDesc && !rr.isSentEmployeeDesc)))
             ).ToList();
         }*/
        private List<ReviewRecipient> FilterReviewRecipients(List<ReviewRecipient> recipients, bool saved, bool isVeduci)
        {
            return recipients.Where(rr =>
                (saved &&
                    ((isVeduci && rr.isSavedSuperiorDesc && !rr.isSentSuperiorDesc) ||
                     (!isVeduci && rr.isSavedEmployeeDesc && !rr.isSentEmployeeDesc))) ||
                (!saved &&
                    ((isVeduci && !rr.isSavedSuperiorDesc && !rr.isSentSuperiorDesc && rr.isSentEmployeeDesc) ||
                     (!isVeduci && !rr.isSavedEmployeeDesc && !rr.isSentEmployeeDesc && rr.isSentSuperiorDesc)))
            ).ToList();
        }


        private List<Guid> GetDistinctReviewIds(List<ReviewRecipient> recipients)
        {
            return recipients.Select(rr => rr.review.id).Distinct().ToList();
        }
        private object CreateMessage(int messageType, List<Guid> reviewIds, string message)
        {
            return new { message, reviewIds, messageType };
        }



        public class UpdateDescriptionRequest
        {
            public string EmployeeDescription { get; set; }
            public string SuperiorDescription { get; set; }

            public string EmployeeQuestion { get; set; }
            public string SuperiorQuestion { get; set; }
        }



    }
}
