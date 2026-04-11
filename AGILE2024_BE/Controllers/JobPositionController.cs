using AGILE2024_BE.Data;
using AGILE2024_BE.Models;
using AGILE2024_BE.Models.Identity;
using AGILE2024_BE.Models.Requests;
using AGILE2024_BE.Models.Requests.JobPositionRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace AGILE2024_BE.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize(Roles = RolesDef.Spravca)]
    public class JobPositionController : ControllerBase
    {
        private UserManager<ExtendedIdentityUser> userManager;
        private RoleManager<IdentityRole> roleManager;
        private IConfiguration config;
        private AgileDBContext dbContext;

        public JobPositionController(UserManager<ExtendedIdentityUser> um, IConfiguration co, RoleManager<IdentityRole> rm, AgileDBContext db)
        {
            this.userManager = um;
            this.config = co;
            this.roleManager = rm;
            this.dbContext = db;
        }


        [HttpPost("Create")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Create([FromBody] CreateJobPositionRequest createRequest)
        {
            if(createRequest.Levels?.Count == 0 || createRequest.Levels == null)
            {
                return BadRequest("Musíte zadať aspoň jeden level");
            }
            if (createRequest.OrganizationsID?.Count == 0 || createRequest.OrganizationsID == null)
            {
                return BadRequest("Musíte zadať aspoň jednu organizáciu");
            }
            var position = new JobPosition
            {
                Name = createRequest.Name,
                Code = createRequest.Code,
                Archived = false,
                Created = DateTime.Now,
                LastEdited = DateTime.Now
            };
            await dbContext.JobPositions.AddAsync(position);
            foreach (var level in createRequest.Levels)
            {
                var newLevel = new Level
                {
                    Name = level,
                    JobPosition = position
                };
                await dbContext.Levels.AddAsync(newLevel);
            }
            foreach (var orgId in createRequest.OrganizationsID)
            {
                var org = await dbContext.Organizations.FindAsync(orgId);
                if (org == null)
                {
                    return BadRequest("Organizácia s ID " + orgId + " nebola nájdená");
                }
                position.Organizations.Add(org);
            }
            await dbContext.SaveChangesAsync();
            return Ok("Pozícia práce vytvorená");
        }

        [HttpPost("Archived")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Archived(string jobPostionID)
        {
            Guid guid = Guid.Parse(jobPostionID);
            var position = await dbContext.JobPositions.FindAsync(guid);
            if (position == null)
            {
                return NotFound("Pozícia práce nebola nájdená");
            }
            position.Archived = true;
            await dbContext.SaveChangesAsync();
            return Ok("Pozícia práce archivovaná");
        }

        [HttpPost("UnArchived")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> UnArchived(string jobPostionID)
        {
            Guid guid = Guid.Parse(jobPostionID);
            var position = await dbContext.JobPositions.FindAsync(guid);
            if (position == null)
            {
                return NotFound("Pozícia práce nebola nájdená");
            }
            position.Archived = false;
            await dbContext.SaveChangesAsync();
            return Ok("Pozícia práce obnovená");
        }

        [HttpPost("Edit")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Edit([FromBody] EditJobPostitionRequest editRequest)
        {
            var position = await dbContext.JobPositions
                                    .Include( pos => pos.Levels)
                                    .Include(pos => pos.Organizations)
                                    .SingleOrDefaultAsync(pos => pos.Id == editRequest.ID);

            if (position == null)
            {
                return BadRequest("Pozícia práce nebola nájdená");
            }
            if (editRequest.Levels?.Count == 0 || editRequest.Levels == null)
            {
                return BadRequest("Musíte zadať aspoň jeden level");
            }
            if (editRequest.OrganizationsID?.Count == 0 || editRequest.OrganizationsID == null)
            {
                return BadRequest("Musíte zadať aspoň jednu organizáciu");
            }
            position.Name = editRequest.Name;
            position.Code = editRequest.Code;
            position.LastEdited = DateTime.Now;
            //prejdi všetky levely v databáze a porovnaj ich s levelmi v parametri
            foreach (var level in position.Levels)
            {
                if (!editRequest.Levels.Contains(level.Name))
                {
                    dbContext.Levels.Remove(level);
                }
            }
            //prejdi všetky levely v parametri a porovnaj ich s levelmi v databáze
            foreach (var level in editRequest.Levels)
            {
                var findLevel = position.Levels.Where(x => x.Name == level).FirstOrDefault();
                if (findLevel == null)
                {
                    var newLevel = new Level
                    {
                        Name = level,
                        JobPosition = position
                    };
                    await dbContext.Levels.AddAsync(newLevel);
                }
            }
            //prejdi všetky organizácie v databáze a porovnaj ich s organizáciami v parametri
            foreach (var org in position.Organizations)
            {
                if (!editRequest.OrganizationsID.Contains(org.Id))
                {
                    org.JobPositions.Remove(position);
                }
            }
            //prejdi všetky organizácie v parametri a porovnaj ich s organizáciami v databáze
            foreach (var orgId in editRequest.OrganizationsID)
            {
                var findOrg = position.Organizations.Where(x => x.Id == orgId).FirstOrDefault();
                if (findOrg == null)
                {
                    var org = await dbContext.Organizations.FindAsync(orgId);
                    if (org == null)
                    {
                        return BadRequest("Organizácia s ID " + orgId + " nebola nájdená");
                    }
                    position.Organizations.Add(org);
                }
            }
            await dbContext.SaveChangesAsync();
            return Ok("Pozícia práce upravená");
        }

        [HttpGet("GetAll")]
        [Authorize(Roles = RolesDef.Spravca + ", " + RolesDef.Veduci + ", " + RolesDef.Zamestnanec + "," + RolesDef.ShopAdmin)]
        public async Task<IActionResult> GetAllAsync()
        {
            var data = await dbContext.JobPositions
                                        .Include(jp => jp.Levels)
                                        .Include(jp => jp.Organizations)
                                        .ToListAsync();
            return Ok(data);
        }

        [HttpGet("Get")]
        public async Task<IActionResult> Get(string id)
        {
            Guid guid = Guid.Parse(id);
            var jobPosition = await dbContext.JobPositions
                        .Include(jp => jp.Levels)
                        .Include(jp => jp.Organizations)
                        .SingleOrDefaultAsync(jp => jp.Id == guid);
            return Ok(jobPosition);
        }

        [HttpGet("GetAllUnarchived")]
        public List<JobPosition> GetAllUnarchived()
        {
            return dbContext.JobPositions.Where(x => x.Archived == false).ToList();
        }

        [HttpDelete("Delete")]
        [Authorize(Roles = RolesDef.Spravca)]
        public async Task<IActionResult> Delete(string id)
        {
            Guid guid = Guid.Parse(id);
            var position = await dbContext.JobPositions
                        .Include(pos => pos.Levels)
                            .ThenInclude(l => l.EmployeeCards)
                        .Include(pos => pos.Organizations)
                        .SingleOrDefaultAsync(pos => pos.Id == guid);
            if (position == null)
            {
                return BadRequest("Pozícia práce nebola nájdená");
            }
            //Delete job from organization 
            foreach (var org in position.Organizations)
            {
                org.JobPositions = null;
            }
            //Delete levels
            foreach (var level in position.Levels)
            {
                //Delete levels from employee cards
                foreach (var empCard in level.EmployeeCards)
                {
                    empCard.Level = null;
                }
                dbContext.Levels.Remove(level);
            }
            //Delete job position
            dbContext.JobPositions.Remove(position);
            await dbContext.SaveChangesAsync();
            return Ok("Pozícia práce vymazaná");
        }
    }
}
