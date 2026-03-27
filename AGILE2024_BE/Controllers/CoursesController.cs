using AGILE2024_BE.Data;
using AGILE2024_BE.Models.Entity.Adaptation;
using AGILE2024_BE.Models.Entity.Courses;
using AGILE2024_BE.Models.Identity;
using AGILE2024_BE.Models.Requests;
using AGILE2024_BE.Models.Requests.EmployeeCardRequests;
using AGILE2024_BE.Models.Response;
using Azure.Core;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController : ControllerBase
    {
        private readonly IHubContext<NotificationHub> hubContext;
        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;

        public CoursesController(UserManager<ExtendedIdentityUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration config, AgileDBContext dbContext, IHubContext<NotificationHub> hubContext)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.config = config;
            this.dbContext = dbContext;
            this.hubContext = hubContext;
        }

        [HttpGet("Get/{selectedUserId}")]
        public async Task<ActionResult<List<CourseEmployeeResponse>>> GetCourseEmployees(string selectedUserId)
        {
            var courseEmployees = await dbContext.CourseEmployees
                .Include(ce => ce.Course)
                    .ThenInclude(c => c.Type)
                .Include(ce => ce.Course)
                    .ThenInclude(c => c.CreatedEmployee)
                        .ThenInclude(e => e.Department)
                .Include(ce => ce.Course)
                    .ThenInclude(c => c.CreatedEmployee)
                        .ThenInclude(c => c.User)
                .Include(ce => ce.Employee)
                    .ThenInclude(e => e.User)
                .Include(ce => ce.State)
                .Where(ce => ce.Employee.User.Id == selectedUserId)
                .ToListAsync();

            var all = await dbContext.CourseEmployees
                .Include(x => x.Course)
                .Include(x => x.Employee)
                    .ThenInclude(xy => xy.User)
                .ToListAsync();

            var result = courseEmployees.Select(ce => new CourseEmployeeResponse
            {
                Id = ce.Id,
                CourseName = ce.Course.Name,
                ExpirationDate = ce.Course.ExpirationDate,
                CourseType = ce.Course.Type.DescriptionType,
                Version = ce.Course.Version,
                DetailDescription = ce.Course.DetailDescription,
                EmployeeId = ce.Employee.User.Id,
                EmployeeName = ce.Employee.User.Name,
                CourseState = ce.State.DescriptionState,
                CompletedDate = ce.CompletedDate,
                CreatedBy = ce.Course.CreatedEmployee.User.Name + " " + ce.Course.CreatedEmployee.User.Surname,
                Department = ce.Course.CreatedEmployee.Department.Name
            }).ToList();

            return Ok(result);
        }


        [HttpDelete("DeleteCourseEmployee/{id}")]
        [Authorize(Roles = RolesDef.Veduci)]
        public async Task<IActionResult> DeleteCourseEmployee(Guid id)
        {
            var courseEmployee = await dbContext.CourseEmployees.FindAsync(id);

            if (courseEmployee == null)
            {
                return NotFound(new { message = "CourseEmployee not found." });
            }

            dbContext.CourseEmployees.Remove(courseEmployee);
            await dbContext.SaveChangesAsync();

            return Ok(new { message = "Deleted successfully." });
        }


        [HttpGet("getAll")]
        public async Task<ActionResult<List<CourseEmployeeResponse>>> GetAllCourseEmployees()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            bool isVeduci = role == "Vedúci zamestnanec";

            if (!Guid.TryParse(userId, out Guid userGuid))
            {
                return BadRequest("Invalid user ID");
            }

            if (!isVeduci)
            {
                return await GetCourseEmployees(userId);
            }
            try
            {
                var courseEmployeesQuery = dbContext.CourseEmployees
                    .Include(ce => ce.Course)
                        .ThenInclude(c => c.Type)
                    .Include(ce => ce.Course)
                        .ThenInclude(c => c.CreatedEmployee)
                            .ThenInclude(e => e.User)
                    .Include(ce => ce.Course)
                        .ThenInclude(c => c.CreatedEmployee)
                            .ThenInclude(c => c.Department)
                    .Include(ce => ce.Employee)
                        .ThenInclude(e => e.User)
                    .Include(ce => ce.Employee)
                        .ThenInclude(e => e.Department)
                    .Include(ce => ce.State);

                var courseEmployees = await courseEmployeesQuery
                    .Where(ce => ce.Course.CreatedEmployee.User.Id == userId).ToListAsync();

                var result = courseEmployees.Select(ce => new CourseEmployeeResponse
                {
                    Id = ce.Id,
                    CourseName = ce.Course.Name,
                    ExpirationDate = ce.Course.ExpirationDate,
                    CourseType = ce.Course.Type.DescriptionType,
                    Version = ce.Course.Version,
                    DetailDescription = ce.Course.DetailDescription,
                    EmployeeId = ce.Employee.User.Id,
                    EmployeeName = ce.Employee.User.Name,
                    CourseState = ce.State.DescriptionState,
                    CompletedDate = ce.CompletedDate,
                    CreatedBy = ce.Course.CreatedEmployee.User.Name + " " + ce.Course.CreatedEmployee.User.Surname,
                    Department = ce.Course.CreatedEmployee.Department.Name
                }).ToList();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("CloseCourseEmployee/{courseEmployeeId}")]
        [Authorize]
        public async Task<IActionResult> CloseCourse(string courseEmployeeId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var employee = await dbContext.EmployeeCards.Include(x => x.User).FirstAsync(x => x.User.Id == userId);

            if (!Guid.TryParse(userId, out Guid userGuid))
            {
                return BadRequest("Invalid user ID.");
            }

            var courseEmployee = await dbContext.CourseEmployees
                .Include(ce => ce.Employee)
                    .ThenInclude(e => e.User)
                .Include(ce => ce.State)
                .Include(ce => ce.Course)
                .FirstOrDefaultAsync(ce => ce.Id.ToString() == courseEmployeeId && ce.Employee.Id == employee.Id);

            if (courseEmployee == null)
            {
                return NotFound(new { message = "CourseEmployee entry not found." });
            }

            var closedState = await dbContext.CourseStates.FirstOrDefaultAsync(cs => cs.DescriptionState == "Splnený");

            if (closedState == null)
            {
                return StatusCode(500, "State 'Splnený' not found in the database.");
            }
            if (closedState == courseEmployee.State)
            {
                return Ok();
            }

            courseEmployee.State = closedState;
            courseEmployee.CompletedDate = DateOnly.FromDateTime(DateTime.UtcNow);

            await dbContext.SaveChangesAsync();

            return Ok(new { message = "Course marked as closed." });
        }

        [HttpPost("StartCourseEmployee/{courseEmployeeId}")]
        [Authorize]
        public async Task<IActionResult> StartCourse(string courseEmployeeId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var employee = await dbContext.EmployeeCards.Include(x => x.User).FirstAsync(x => x.User.Id == userId);

            if (!Guid.TryParse(userId, out Guid userGuid))
            {
                return BadRequest("Invalid user ID.");
            }

            var courseEmployee = await dbContext.CourseEmployees
                .Include(ce => ce.Employee)
                    .ThenInclude(e => e.User)
                .Include(ce => ce.State)
                .Include(ce => ce.Course)
                .FirstOrDefaultAsync(ce => ce.Id.ToString() == courseEmployeeId && ce.Employee == employee);

            if (courseEmployee == null)
            {
                return NotFound(new { message = "CourseEmployee entry not found." });
            }

            var newState = await dbContext.CourseStates.FirstOrDefaultAsync(cs => cs.DescriptionState == "Prebiehajúci");

            if (newState == null)
            {
                return StatusCode(500, "State 'Prebiehajúci' not found in the database.");
            }

            courseEmployee.State = newState;

            await dbContext.SaveChangesAsync();

            return Ok(new { message = "Course marked as open." });
        }

        [HttpPost("CreateCourse")]
        public async Task<IActionResult> CreateCourse([FromBody] CreateCourseRequest createCourse)
        {
            if (createCourse == null)
            {
                return BadRequest(new { message = "Nesprávne dáta." });
            }
            if (string.IsNullOrEmpty(createCourse.Name))
                return BadRequest(new { message = "Nevyplnil sa názov kurzu/školenia" });
            if (createCourse.Employees.Count == 0)
            {
                return BadRequest(new { message = "Neboli priradený žiadny zamestnanci" });
            }

            if (createCourse.Files.Count == 0)
            {
                return BadRequest(new { message = "Nebol priradený súbor" });
            }

            var createdEmployee = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .FirstOrDefaultAsync(ec => ec.User.Id == createCourse.CreatedUserId);
            if (createdEmployee == null)
            {
                return NotFound("Creator not found.");
            }
            var courseType = await dbContext.CoursesTypes
                .FirstOrDefaultAsync(ct => ct.DescriptionType == createCourse.Type);
            if (courseType == null)
            {
                return NotFound("Course type not found.");
            }

            DateTime? date = null;

            if (!string.IsNullOrWhiteSpace(createCourse.ExpirationDate))
            {
                if (!DateTime.TryParse(createCourse.ExpirationDate, out var parsedDate))
                {
                    return BadRequest(new { message = "Neplatný formát dátumu" });
                }
                date = parsedDate;
            }
            else
            {
                return BadRequest(new { message = "Chýba dátum expirácie" });
            }

            if (date < DateTime.UtcNow)
            {
                return BadRequest(new { message = "Dátum expirácie nesmie byť v minulosit" });
            }
            var course = new Course
            {
                Name = createCourse.Name,
                DetailDescription = createCourse.DetailDescription,
                CreatedEmployee = createdEmployee,
                Type = courseType,
                Version = createCourse.Version,
                ExpirationDate = DateOnly.FromDateTime(date.Value)
            };
            dbContext.Courses.Add(course);
            var courseState = await dbContext.CourseStates
                .FirstOrDefaultAsync(cs => cs.DescriptionState == "Nezačatý");
            foreach (var employee in createCourse.Employees)
            {
                var employeeCard = await dbContext.EmployeeCards
                    .Include(ec => ec.User)
                    .FirstOrDefaultAsync(ec => ec.Id.ToString() == employee);
                if (employeeCard == null)
                {
                    return NotFound($"Employee with ID {employee} not found.");
                }
                var courseEmployee = new CourseEmployee
                {
                    Course = course,
                    Employee = employeeCard,
                    State = courseState
                };
                dbContext.CourseEmployees.Add(courseEmployee);
            }
            var docEntities = createCourse.Files?
                .Where(f => !string.IsNullOrWhiteSpace(f.FilePath))
                .Select(f => new CoursesDoc
                {
                    Id = Guid.NewGuid(),
                    Course = course,
                    FilePath = f.FilePath,
                    DescriptionDocs = f.Description,
                }).ToList();
            await dbContext.CoursesDocs.AddRangeAsync(docEntities);
            await dbContext.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("UploadFile")]
        public async Task<IActionResult> UploadAdaptationDoc([FromForm] FileUpload req)
        {
            if (req.File == null || req.File.Length == 0)
                return BadRequest("No file uploaded.");

            try
            {
                var blobClient = new BlobServiceClient(config.GetSection("Blob")["BlobConnect"]);
                var containerClient = blobClient.GetBlobContainerClient("coursefiles");

                await containerClient.CreateIfNotExistsAsync();

                var extension = Path.GetExtension(req.File.FileName);
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(req.File.FileName);
                var blobName = $"{fileNameWithoutExtension}_{Guid.NewGuid()}{extension}";
                var blob = containerClient.GetBlobClient(blobName);
                await blob.UploadAsync(req.File.OpenReadStream(), overwrite: true);



                var blobUrl = blob.Uri.ToString();

                return Ok(new { filePath = blobUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"File upload failed: {ex.Message}");
            }
        }

        [HttpGet("GetCourseFiles")]
        public async Task<IActionResult> GetCourseFiles(string CourseID)
        {
            Guid guid = Guid.Parse(CourseID);
            var courseEmployee = await dbContext.CourseEmployees
                .Include(x => x.Course)
                .FirstOrDefaultAsync(x => x.Id == guid);
            if (courseEmployee == null) 
            { 
                return NotFound("Files not found.");
            }
            var files = await dbContext.CoursesDocs.Where(x => x.Course.Id == courseEmployee.Course.Id).ToListAsync();
            if (files == null)
            {
                return NotFound("Files not found.");
            }
            List<FileRequest> response = new List<FileRequest>();
            foreach (var file in files)
            {
                response.Add(new FileRequest
                {
                    Description = file.DescriptionDocs,
                    FilePath = file.FilePath
                });
            }
            return Ok(response);
        }

        [HttpPost("SetCoursesAfterDate")]
        public async Task<IActionResult> SetCoursesAfterDate()
        {
            var courseEmployees = await dbContext.CourseEmployees
                .Include(ce => ce.Course)
                .Where(ce => ce.CompletedDate == null && ce.Course.ExpirationDate < DateOnly.FromDateTime(DateTime.UtcNow))
                .ToListAsync();
            var expiredState = await dbContext.CourseStates.Where(x => x.DescriptionState == "Nesplnený").FirstOrDefaultAsync();
            if (expiredState == null)
            {
                return NotFound("State 'Nesplnený' not found in the database.");
            }
            foreach (var courseEmployee in courseEmployees)
            {
                courseEmployee.State = expiredState;
            }
            await dbContext.SaveChangesAsync();
            return Ok();
        }
    }
}
