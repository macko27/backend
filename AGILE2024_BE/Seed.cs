﻿using AGILE2024_BE.Data;
using AGILE2024_BE.Models;
using AGILE2024_BE.Models.Entity.Adaptation;
using AGILE2024_BE.Models.Entity.Courses;
using AGILE2024_BE.Models.Entity.SuccessionPlan;
using AGILE2024_BE.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;

namespace AGILE2024_BE
{
    public class Seed
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var _roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var _userManager = serviceProvider.GetRequiredService<UserManager<ExtendedIdentityUser>>();
            var context = serviceProvider.GetRequiredService<AgileDBContext>();

            if (_roleManager != null && _userManager != null)
            {
                //Seed AdaptationState
                var adaptationStates = new List<AdaptationState>
                {
                    new AdaptationState { DescriptionState = "Nezadané" },
                    new AdaptationState { DescriptionState = "Zadané" },
                    new AdaptationState { DescriptionState = "V procese" },
                    new AdaptationState { DescriptionState = "Splnené " }
                };
                foreach (var adaptationState in adaptationStates)
                {
                    var existingContractType = await context.AdaptationStates
                        .FirstOrDefaultAsync(ct => ct.DescriptionState == adaptationState.DescriptionState);

                    if (existingContractType == null)
                    {
                        await context.AdaptationStates.AddAsync(adaptationState);
                    }
                }
                //Seed CourseState
                var courseStates = new List<CourseState>
                {
                    new CourseState { DescriptionState = "Splnený" },
                    new CourseState { DescriptionState = "Prebiehajúci" },
                    new CourseState { DescriptionState = "Nezačatý" },
                    new CourseState { DescriptionState = "Nesplnený" }
                };
                foreach (var courseState in courseStates)
                {
                    var existingContractType = await context.CourseStates
                        .FirstOrDefaultAsync(ct => ct.DescriptionState == courseState.DescriptionState);

                    if (existingContractType == null)
                    {
                        await context.CourseStates.AddAsync(courseState);
                    }
                }
                //Seed CoursesType
                var coursesTypes = new List<CoursesType>
                {
                    new CoursesType { DescriptionType = "Kurz" },
                    new CoursesType { DescriptionType = "Školenie" }
                };
                foreach (var coursesType in coursesTypes)
                {
                    var existingContractType = await context.CoursesTypes
                        .FirstOrDefaultAsync(ct => ct.DescriptionType == coursesType.DescriptionType);

                    if (existingContractType == null)
                    {
                        await context.CoursesTypes.AddAsync(coursesType);
                    }
                }
                //Seed ReadyStatus
                var readyStatuses = new List<ReadyStatus>
                {
                    new ReadyStatus { description = "Okamžitá pripravenosť" },
                    new ReadyStatus { description = "1 - 2 roky" },
                    new ReadyStatus { description = "3+ rokov" }
                };
                foreach (var ReadyStatus in readyStatuses)
                {
                    var existingContractType = await context.ReadyStatuses
                        .FirstOrDefaultAsync(ct => ct.description == ReadyStatus.description);

                    if (existingContractType == null)
                    {
                        await context.ReadyStatuses.AddAsync(ReadyStatus);
                    }
                }
                //Seed LeaveType
                var leaveTypes = new List<LeaveType>
                {
                    new LeaveType { description = "Kritický odchod" },
                    new LeaveType { description = "Plánovaný odchod" },
                    new LeaveType { description = "Redundancia tímu" },
                };
                foreach (var leaveType in leaveTypes)
                {
                    var existingContractType = await context.LeaveTypes
                        .FirstOrDefaultAsync(ct => ct.description == leaveType.description);

                    if (existingContractType == null)
                    {
                        await context.LeaveTypes.AddAsync(leaveType);
                    }
                }

                //Seed FeedbackRequestStatuses
                var feedbackRequestStatuses = new List<FeedbackRequestStatus>
                {
                    new FeedbackRequestStatus { description = "Vyplnený" },
                    new FeedbackRequestStatus { description = "Zamietnutý" },
                    new FeedbackRequestStatus { description = "Nevyplnený" }
                };
                foreach (var feedbackRequestStatus in feedbackRequestStatuses)
                {
                    var existingContractType = await context.FeedbackRequestStatuses
                        .FirstOrDefaultAsync(ct => ct.description == feedbackRequestStatus.description);

                    if (existingContractType == null)
                    {
                        await context.FeedbackRequestStatuses.AddAsync(feedbackRequestStatus);
                    }
                }
                // Seed GoalStatuses
                var goalStatuses = new List<GoalStatus>
                {
                    new GoalStatus { description = "Nezačatý" },
                    new GoalStatus { description = "Prebiehajúci" },
                    new GoalStatus { description = "Dokončený" },
                    new GoalStatus { description = "Zrušený" }
                };
                foreach (var goalStatus in goalStatuses)
                {
                    var existingContractType = await context.GoalStatuses
                        .FirstOrDefaultAsync(ct => ct.description == goalStatus.description);

                    if (existingContractType == null)
                    {
                        await context.GoalStatuses.AddAsync(goalStatus);
                    }
                }
                //Seed GoalCategory
                var goalCategories = new List<GoalCategory>
                {
                    new GoalCategory { description = "Výkonostný rozvoj" },
                    new GoalCategory { description = "Osobný rozvoj" },
                    new GoalCategory { description = "Nástupnícky cieľ" }
                };
                foreach (var goalCategory in goalCategories)
                {
                    var existingContractType = await context.GoalCategory
                        .FirstOrDefaultAsync(ct => ct.description == goalCategory.description);

                    if (existingContractType == null)
                    {
                        await context.GoalCategory.AddAsync(goalCategory);
                    }
                }
                // Seed contract types
                var contractTypes = new List<ContractType>
                {
                    new ContractType { Name = "Dohoda o brigádnickej práci študentove" },
                    new ContractType { Name = "Trvalý pracovný pomer" },
                    new ContractType { Name = "Dohoda o vykonaní práce" },
                    new ContractType { Name = "Dohoda o pracovnej činnosti" },
                    new ContractType { Name = "Živnosť" },
                    new ContractType { Name = "Externý zamestnanec" }
                };

                foreach (var contractType in contractTypes)
                {
                    var existingContractType = await context.ContractTypes
                        .FirstOrDefaultAsync(ct => ct.Name == contractType.Name);

                    if (existingContractType == null)
                    {
                        await context.ContractTypes.AddAsync(contractType);
                    }
                }
                await context.SaveChangesAsync();

                // Seed roles
                IdentityRole? role = await _roleManager.FindByNameAsync(RolesDef.Spravca);
                if (role == null)
                {
                    await _roleManager.CreateAsync(new IdentityRole(RolesDef.Spravca));
                }

                IdentityRole? role2 = await _roleManager.FindByNameAsync(RolesDef.Zamestnanec);
                if (role2 == null)
                {
                    await _roleManager.CreateAsync(new IdentityRole(RolesDef.Zamestnanec));
                }

                IdentityRole? role3 = await _roleManager.FindByNameAsync(RolesDef.PowerUser);
                if (role3 == null)
                {
                    await _roleManager.CreateAsync(new IdentityRole(RolesDef.PowerUser));
                }

                IdentityRole? role4 = await _roleManager.FindByNameAsync(RolesDef.Veduci);
                if (role4 == null)
                {
                    await _roleManager.CreateAsync(new IdentityRole(RolesDef.Veduci));
                }

                ExtendedIdentityUser? admin = await _userManager.FindByEmailAsync("matej@email.com");
                if (admin == null)
                {
                    string[] userNames = { "matej", "katarina", "simona" };
                    string[] lastNames = { "macko", "poliakova", "tothova" };

                    for (int i = 0; i < userNames.Length; i++)
                    {
                        ExtendedIdentityUser user = new ExtendedIdentityUser()
                        {
                            UserName = $"{userNames[i]}@email.com",
                            Email = $"{userNames[i]}@email.com",
                            Name = userNames[i],
                            Surname = lastNames[i],
                            Title_before = "Bc."
                        };

                        await _userManager.CreateAsync(user, "AdminAdmin123!");
                        await _userManager.AddToRoleAsync(user, RolesDef.Spravca);
                    }
                }
                ExtendedIdentityUser? zam = await _userManager.FindByEmailAsync("zamestnanec@email.com");
                if (zam == null)
                {
                    ExtendedIdentityUser zamestnanec = new ExtendedIdentityUser()
                    {
                        UserName = "zamestnanec@email.com",
                        Email = "zamestnanec@email.com",
                        Name = "Meno",
                        Surname = "Priezvisko"
                    };

                    await _userManager.CreateAsync(zamestnanec, "AdminAdmin123!");
                    await _userManager.AddToRoleAsync(zamestnanec, RolesDef.Zamestnanec);
                }

                ExtendedIdentityUser? pu = await _userManager.FindByEmailAsync("poweruser@email.com");
                if (pu == null)
                {
                    ExtendedIdentityUser powerUser = new ExtendedIdentityUser()
                    {
                        UserName = "poweruser@email.com",
                        Email = "poweruser@email.com",
                        Name = "Meno",
                        Surname = "Priezvisko"
                    };

                    await _userManager.CreateAsync(powerUser, "AdminAdmin123!");
                    await _userManager.AddToRoleAsync(powerUser, RolesDef.PowerUser);
                }

                ExtendedIdentityUser? ved = await _userManager.FindByEmailAsync("veduci@email.com");
                if (ved == null)
                {
                    ExtendedIdentityUser veduci = new ExtendedIdentityUser()
                    {
                        UserName = "veduci@email.com",
                        Email = "veduci@email.com",
                        Name = "Meno",
                        Surname = "Priezvisko"
                    };

                    await _userManager.CreateAsync(veduci, "AdminAdmin123!");
                    await _userManager.AddToRoleAsync(veduci, RolesDef.Veduci);
                }
            }
        }
    }
}