using AGILE2024_BE.Data;
using AGILE2024_BE.Models;
using AGILE2024_BE.Models.Identity;
using AGILE2024_BE.Models.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations.Schema;
using AGILE2024_BE.Models.Enums;
using Microsoft.EntityFrameworkCore;
using AGILE2024_BE.Models.Response.Feedback;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using AGILE2024_BE.Models.Entity.Adaptation;
using Azure.Core;
using Azure.Storage.Blobs;
using System.Reflection.Metadata;
namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AdaptationController : ControllerBase
    {
        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;

        public AdaptationController(UserManager<ExtendedIdentityUser> um, IConfiguration co, RoleManager<IdentityRole> rm, AgileDBContext db)
        {
            this.userManager = um;
            this.config = co;
            this.roleManager = rm;
            this.dbContext = db;
        }

        [HttpGet("GetAdaptations")]
        [Authorize(Roles = RolesDef.Veduci)]
        public async Task<IActionResult> Adaptations()
        {
            try
            {
                ExtendedIdentityUser? user = await userManager.FindByEmailAsync(User.Identity?.Name!);

                var adaptations = await dbContext.Adaptations
                     .Include(a => a.Employee)
                         .ThenInclude(e => e.User)
                     .Include(a => a.CreatedEmployee)
                         .ThenInclude(e => e.User)
                     .Include(a => a.State)
                     .Where(a => a.CreatedEmployee.User.Id == user.Id)
                     .Select(a => new AdaptationResponse
                     {
                         id = a.Id,
                         nameEmployee = $"{a.Employee.User.Name} {a.Employee.User.Surname}",
                         nameSupervisor = $"{a.CreatedEmployee.User.Name} {a.CreatedEmployee.User.Surname}",
                         readyDate = a.ReadyDate == DateOnly.MinValue ? null : a.ReadyDate,
                         endDate = a.EndDate == DateOnly.MinValue ? null: a.EndDate,
                         taskState = a.State.DescriptionState
                     })
                     .ToListAsync();
                if (!adaptations.Any())
                    return NoContent();

                return Ok(adaptations);
            } 
            catch (Exception ex)
            {
                return StatusCode(500, "Chyba pri nacitavani.");
            }
        }


        [HttpGet("GetMyAdaptation")]
        [Authorize(Roles = RolesDef.Zamestnanec + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> GetMyAdaptation()
        {
            try
            {
                ExtendedIdentityUser? user = await userManager.FindByEmailAsync(User.Identity?.Name!);

                var loggedEmployee = await dbContext.EmployeeCards
                    .Include(ec => ec.User)
                    .Where(ec => ec.User.Id == user.Id).FirstOrDefaultAsync();

                if (loggedEmployee == null)
                    return NotFound("Zamestnanec nenájdený.");


                var adaptation = await dbContext.Adaptations
                    .Include(a => a.Employee)
                     .ThenInclude(e => e.User)
                    .Include(a => a.CreatedEmployee)
                     .ThenInclude(e => e.User)
                    .Include(a => a.State)
                    .FirstOrDefaultAsync(a => a.Employee.Id == loggedEmployee.Id);

                if (adaptation == null)
                    return NotFound("Adaptaia nebola najdena.");

                var existingTasks = await dbContext.AdaptationTasks
                   .Where(t => t.Adaptation.Id == adaptation.Id)
                   .ToListAsync();

                var existingDocs = await dbContext.AdaptationDocs
                   .Where(d => d.Adaptation.Id == adaptation.Id)
                   .ToListAsync();


                var employee = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Include(ec => ec.Level)
                .ThenInclude(l => l.JobPosition)
                .Include(ec => ec.Department)
                .ThenInclude(d => d.Organization)
                .Where(ec => ec.User.Id == adaptation.Employee.User.Id).FirstOrDefaultAsync();


                var creator = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Include(ec => ec.Level)
                .ThenInclude(l => l.JobPosition)
                .Include(ec => ec.Department)
                .ThenInclude(d => d.Organization)
                .Where(ec => ec.User.Id == adaptation.CreatedEmployee.User.Id).FirstOrDefaultAsync();


                var employyeCard = new EmployeeCardResponse
                {
                    EmployeeId = employee.Id,
                    Email = employee.User.Email,
                    Name = employee.User.Name ?? string.Empty,
                    TitleBefore = employee.User.Title_before ?? string.Empty,
                    TitleAfter = employee.User.Title_after ?? string.Empty,
                    Department = employee.Department.Name ?? "N/A",
                    Organization = employee.Department.Organization.Name,
                    JobPosition = employee.Level.JobPosition.Name,
                    Surname = employee.User.Surname ?? string.Empty,
                    MiddleName = employee.User.MiddleName
                };

                var creatorCard = new EmployeeCardResponse
                {
                    EmployeeId = creator.Id,
                    Email = creator.User.Email,
                    Name = creator.User.Name ?? string.Empty,
                    TitleBefore = creator.User.Title_before ?? string.Empty,
                    TitleAfter = creator.User.Title_after ?? string.Empty,
                    Department = creator.Department.Name ?? "N/A",
                    Organization = creator.Department.Organization.Name,
                    JobPosition = creator.Level.JobPosition.Name,
                    Surname = creator.User.Surname ?? string.Empty,
                    MiddleName = creator.User.MiddleName
                };


                var response = new AdaptationFullResponse
                {
                    id = adaptation.Id,
                    employee = employyeCard,
                    creator = creatorCard,
                    employeeName = $"{adaptation.Employee.User.Name} {adaptation.Employee.User.Surname}",
                    creatorName = $"{adaptation.CreatedEmployee.User.Name} {adaptation.CreatedEmployee.User.Surname}",
                    readyDate = adaptation.ReadyDate,
                    endDate = adaptation.EndDate,

                    tasks = existingTasks.Select(t => new TaskResponse
                    {
                        id = t.Id,
                        description = t.DescriptionTask,
                        finishDate = t.FinishDate,
                        isDone = t.IsDone
                    }).ToList(),

                    documents = existingDocs.Select(d => new DocumentResponse
                    {
                        id = d.Id,
                        description = d.DescriptionDocs,
                        file = d.FilePath
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Chyba pri nacitavani.");
            }
        }

        [HttpPost("UploadAdaptationDoc")]
        [Authorize]
        public async Task<IActionResult> UploadAdaptationDoc([FromForm] UploadAdaptationDocRequest req)
        {
            if (req.File == null || req.File.Length == 0)
                return BadRequest("No file uploaded.");

            try
            {
                var blobClient = new BlobServiceClient(config.GetSection("Blob")["BlobConnect"]);
                var containerClient = blobClient.GetBlobContainerClient("adaptationdocs");

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



        [HttpPost("Create")]
        public async Task<IActionResult> CreateAdaptation([FromBody] CreateAdaptationRequest data)
        {
            var adaptationId = Guid.NewGuid();


            var employeeEmployeeCard = await dbContext.EmployeeCards
                .FirstOrDefaultAsync(ec => ec.Id == data.EmployeeId);


            var createEmployeeCard = await dbContext.EmployeeCards
                .FirstOrDefaultAsync(ec => ec.Id == data.CreatedEmployeeId);


            var state = await dbContext.AdaptationStates
                 .FirstOrDefaultAsync(s => s.DescriptionState == "Nezadané"); ;
           
            if (data.Tasks.Count == 0)
            {
                state = await dbContext.AdaptationStates
                 .FirstOrDefaultAsync(s => s.DescriptionState == "Nezadané");
            } else if (data.Tasks.Count > 0) {
                state = await dbContext.AdaptationStates
                     .FirstOrDefaultAsync(s => s.DescriptionState == "Zadané");

                if (data.Tasks.All(t => t.IsDone))
                {
                    state = await dbContext.AdaptationStates
                        .FirstOrDefaultAsync(s => s.DescriptionState == "Splnené ");
                } else {
                    if (data.Tasks.Any(t => t.IsDone)) 
                    { 
                        state = await dbContext.AdaptationStates
                        .FirstOrDefaultAsync(s => s.DescriptionState == "V procese");
                    }
                }
            }
            
            DateOnly endDate = new DateOnly();


            var adaptation = new Adaptation
            {
                Id = adaptationId,
                Employee = employeeEmployeeCard,
                CreatedEmployee = createEmployeeCard,
                ReadyDate = data.Tasks.Count > 0 ? data.Tasks.Max(t => t.FinishDate) : DateOnly.MinValue,
                EndDate = data.Tasks.Count > 0 && data.Tasks.All(t => t.IsDone) ? DateOnly.FromDateTime(DateTime.Now) : DateOnly.MinValue,
                State = state,
            };

            dbContext.Adaptations.Add(adaptation);
            await dbContext.SaveChangesAsync();
          
            var taskEntities = data.Tasks.Select(t => new AdaptationTask
            {
                Id = Guid.NewGuid(),
                Adaptation = adaptation,
                DescriptionTask = t.Description,
                IsDone = t.IsDone,
                FinishDate = t.FinishDate.ToDateTime(TimeOnly.MinValue)
            }).ToList();

            var docEntities = data.Documents?
                .Where(d => !string.IsNullOrWhiteSpace(d.FilePath))
                .Select(d => new AdaptationDoc
                {
                    Id = Guid.NewGuid(),
                    Adaptation = adaptation,
                    DescriptionDocs = d.Description,
                    FilePath = d.FilePath
                }).ToList();


            await dbContext.AdaptationTasks.AddRangeAsync(taskEntities);
            await dbContext.AdaptationDocs.AddRangeAsync(docEntities);
            await dbContext.SaveChangesAsync();

            return Ok(new { adaptationId });
        }

        [HttpGet("GetById/{id}")]
        public async Task<IActionResult> GetAdaptationById(Guid id)
        {
            try
            {
                var adaptation = await dbContext.Adaptations
                   .Include(a => a.Employee)
                    .ThenInclude(e => e.User)
                   .Include(a => a.CreatedEmployee)
                    .ThenInclude(e => e.User)
                   .Include(a => a.State)
                   .FirstOrDefaultAsync(a => a.Id == id);

                if (adaptation == null)
                    return NotFound("Adaptaia nebola najdena.");

                var existingTasks = await dbContext.AdaptationTasks
                   .Where(t => t.Adaptation.Id == id)
                   .ToListAsync();

                var existingDocs = await dbContext.AdaptationDocs
                   .Where(d => d.Adaptation.Id == id)
                   .ToListAsync();


                var employee = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Include(ec => ec.Level)
                .ThenInclude(l => l.JobPosition)
                .Include(ec => ec.Department)
                .ThenInclude(d => d.Organization)
                .Where(ec => ec.User.Id == adaptation.Employee.User.Id).FirstOrDefaultAsync();


                var creator = await dbContext.EmployeeCards
                .Include(ec => ec.User)
                .Include(ec => ec.Level)
                .ThenInclude(l => l.JobPosition)
                .Include(ec => ec.Department)
                .ThenInclude(d => d.Organization)
                .Where(ec => ec.User.Id == adaptation.CreatedEmployee.User.Id).FirstOrDefaultAsync();


                var employyeCard = new EmployeeCardResponse
                {
                    EmployeeId = employee.Id,
                    Email = employee.User.Email,
                    Name = employee.User.Name ?? string.Empty,
                    TitleBefore = employee.User.Title_before ?? string.Empty,
                    TitleAfter = employee.User.Title_after ?? string.Empty,
                    Department = employee.Department.Name ?? "N/A",
                    Organization = employee.Department.Organization.Name,
                    JobPosition = employee.Level.JobPosition.Name,
                    Surname = employee.User.Surname ?? string.Empty,
                    MiddleName = employee.User.MiddleName
                };

                var creatorCard = new EmployeeCardResponse
                {
                    EmployeeId = creator.Id,
                    Email = creator.User.Email,
                    Name = creator.User.Name ?? string.Empty,
                    TitleBefore = creator.User.Title_before ?? string.Empty,
                    TitleAfter = creator.User.Title_after ?? string.Empty,
                    Department = creator.Department.Name ?? "N/A",
                    Organization = creator.Department.Organization.Name,
                    JobPosition = creator.Level.JobPosition.Name,
                    Surname = creator.User.Surname ?? string.Empty,
                    MiddleName = creator.User.MiddleName
                };


                var response = new AdaptationFullResponse
                {
                    id = adaptation.Id,
                    employee = employyeCard,
                    creator = creatorCard,
                    employeeName = $"{adaptation.Employee.User.Name} {adaptation.Employee.User.Surname}",
                    creatorName = $"{adaptation.CreatedEmployee.User.Name} {adaptation.CreatedEmployee.User.Surname}",
                    readyDate = adaptation.ReadyDate,
                    endDate = adaptation.EndDate,

                    tasks = existingTasks.Select(t => new TaskResponse
                    {
                        id = t.Id,
                        description = t.DescriptionTask,
                        finishDate = t.FinishDate,
                        isDone = t.IsDone
                    }).ToList(),

                    documents = existingDocs.Select(d => new DocumentResponse
                    {
                        id = d.Id,
                        description = d.DescriptionDocs,
                        file = d.FilePath
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Chyba pri nacitavani.");
            }
        }


        [HttpPut("Update/{id}")]
        public async Task<IActionResult> UpdateAdaptation(Guid id, [FromBody] CreateAdaptationRequest data)
        {
            try
            {
                var adaptation = await dbContext.Adaptations
                    .Include(a => a.Employee)
                    .Include(a => a.CreatedEmployee)
                    .Include(a => a.State)
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (adaptation == null)
                    return NotFound("Adaptácia nebola nájdená.");

               var existiongTasks = await dbContext.AdaptationTasks
                    .Where(a => a.Adaptation.Id == id)
                    .ToListAsync();

                var existiongDocs = await dbContext.AdaptationDocs
                    .Where(a => a.Adaptation.Id == id)
                    .ToListAsync();

                dbContext.AdaptationTasks.RemoveRange(existiongTasks);
                dbContext.AdaptationDocs.RemoveRange(existiongDocs);
                await dbContext.SaveChangesAsync();

                var newTasks = data.Tasks.Select(t => new AdaptationTask
                {
                    Id = Guid.NewGuid(),
                    Adaptation = adaptation,
                    DescriptionTask = t.Description,
                    FinishDate = t.FinishDate.ToDateTime(TimeOnly.MinValue),
                    IsDone = t.IsDone
                }).ToList();

               
                var newDocs = data.Documents?
                .Where(d => !string.IsNullOrWhiteSpace(d.FilePath))
                .Select(d => new AdaptationDoc
                {
                    Id = Guid.NewGuid(),
                    Adaptation = adaptation,
                    DescriptionDocs = d.Description,
                    FilePath = d.FilePath
                }).ToList();

                await dbContext.AdaptationTasks.AddRangeAsync(newTasks);
                await dbContext.AdaptationDocs.AddRangeAsync(newDocs);

                // Update state and dates
                var state = await dbContext.AdaptationStates
                    .FirstOrDefaultAsync(s => s.DescriptionState == "Nezadané");

                if (newTasks.Count > 0)
                {
                    state = await dbContext.AdaptationStates
                        .FirstOrDefaultAsync(s => s.DescriptionState == "Zadané");

                    if (newTasks.All(t => t.IsDone))
                    {
                        state = await dbContext.AdaptationStates
                            .FirstOrDefaultAsync(s => s.DescriptionState == "Splnené ");
                        adaptation.EndDate = DateOnly.FromDateTime(DateTime.Now);
                    }
                    else if (newTasks.Any(t => t.IsDone))
                    {
                        state = await dbContext.AdaptationStates
                            .FirstOrDefaultAsync(s => s.DescriptionState == "V procese");
                        adaptation.EndDate = DateOnly.MinValue;
                    }
                    else
                    {
                        adaptation.EndDate = DateOnly.MinValue;
                    }

                    adaptation.ReadyDate = newTasks.Max(t => DateOnly.FromDateTime(t.FinishDate));
                }
                else
                {
                    adaptation.ReadyDate = DateOnly.MinValue;
                    adaptation.EndDate = DateOnly.MinValue;
                }

                adaptation.State = state;

                await dbContext.SaveChangesAsync();

                return Ok(new { message = "Adaptácia bola úspešne upravená." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Chyba pri úprave adaptácie: {ex.Message}");
            }
        }


       
    }


    public class CreateAdaptationRequest
    {
        public Guid EmployeeId { get; set; }
        public Guid CreatedEmployeeId { get; set; }
        public Guid StateId { get; set; }

        public List<AdaptationTaskRequest> Tasks { get; set; }
        public List<AdaptationDocRequest> Documents { get; set; }
    }

    public class AdaptationTaskRequest
    {
        public string Description { get; set; }
        public DateOnly FinishDate { get; set; }
        public bool IsDone { get; set; }
    }

    public class AdaptationDocRequest
    {
        public string Description { get; set; }
        public string FilePath { get; set; }
    }

    public class UploadAdaptationDocRequest
    {
        public IFormFile File { get; set; }
    }

}
