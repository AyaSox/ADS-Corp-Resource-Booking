using System.Linq;
using ResourceBooking.Models;
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace ResourceBooking.Data
{
    public class DataSeeder
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<DataSeeder> _logger;

        public DataSeeder(ApplicationDbContext db, ILogger<DataSeeder> logger) 
        { 
            _db = db; 
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                // Database should already be created by Program.cs
                _logger.LogInformation("Starting data seeding...");

                // Check if Resources table exists and has data
                if (!await _db.Resources.AnyAsync())
                {
                    _logger.LogInformation("Seeding resources...");
                    var resources = new List<Resource>
                    {
                        new Resource { Name = "Oak Boardroom", Description = "Main boardroom with video conferencing", Location = "4th Floor", Capacity = 14, IsAvailable = true },
                        new Resource { Name = "Vehicle - Nissan NP200", Description = "Company utility vehicle", Location = "Parking Bay 1", Capacity = 2, IsAvailable = true },
                        new Resource { Name = "Training Room B", Description = "Training room with whiteboard and projector", Location = "3rd Floor", Capacity = 8, IsAvailable = true },
                        new Resource { Name = "Meeting Pod 1", Description = "Small meeting space for 1-on-1s", Location = "1st Floor", Capacity = 4, IsAvailable = true },
                        new Resource { Name = "Conference Hall", Description = "Large hall for company events", Location = "Ground Floor", Capacity = 50, IsAvailable = true },
                        new Resource { Name = "Vehicle - Toyota Corolla", Description = "Executive vehicle", Location = "Parking Bay 2", Capacity = 4, IsAvailable = false, UnavailabilityReason = "Scheduled maintenance", UnavailabilityType = Models.UnavailabilityType.Maintenance }
                    };

                    await _db.Resources.AddRangeAsync(resources);
                    await _db.SaveChangesAsync();
                    _logger.LogInformation($"Seeded {resources.Count} resources");
                }

                // Wait for users to be created by Identity seeder, then add sample bookings
                var users = await _db.Users.ToListAsync();
                if (users.Any() && !await _db.Bookings.AnyAsync())
                {
                    _logger.LogInformation($"Found {users.Count} users, seeding bookings...");
                    
                    var resources = await _db.Resources.Where(r => r.IsAvailable).ToListAsync();
                    if (resources.Any())
                    {
                        var user = users.First();
                        var today = DateTime.Today;
                        var thisMonth = new DateTime(today.Year, today.Month, 1);

                        var sampleBookings = new List<Booking>
                        {
                            // Today's bookings
                            new Booking 
                            { 
                                ResourceId = resources[0].Id, 
                                UserId = user.Id, 
                                Purpose = "Morning standup meeting", 
                                StartTime = DateTime.SpecifyKind(today.AddHours(9), DateTimeKind.Utc), 
                                EndTime = DateTime.SpecifyKind(today.AddHours(10), DateTimeKind.Utc), 
                                Cancelled = false 
                            },
                            new Booking 
                            { 
                                ResourceId = resources[1].Id, 
                                UserId = user.Id, 
                                Purpose = "Client site visit", 
                                StartTime = DateTime.SpecifyKind(today.AddHours(14), DateTimeKind.Utc), 
                                EndTime = DateTime.SpecifyKind(today.AddHours(17), DateTimeKind.Utc), 
                                Cancelled = false 
                            },

                            // This week's bookings
                            new Booking 
                            { 
                                ResourceId = resources[0].Id, 
                                UserId = user.Id, 
                                Purpose = "Project planning session", 
                                StartTime = DateTime.SpecifyKind(today.AddDays(-1).AddHours(10), DateTimeKind.Utc), 
                                EndTime = DateTime.SpecifyKind(today.AddDays(-1).AddHours(12), DateTimeKind.Utc), 
                                Cancelled = false 
                            },
                            new Booking 
                            { 
                                ResourceId = resources.Count > 2 ? resources[2].Id : resources[0].Id, 
                                UserId = user.Id, 
                                Purpose = "Team training session", 
                                StartTime = DateTime.SpecifyKind(today.AddDays(-2).AddHours(13), DateTimeKind.Utc), 
                                EndTime = DateTime.SpecifyKind(today.AddDays(-2).AddHours(16), DateTimeKind.Utc), 
                                Cancelled = false 
                            },

                            // This month's bookings
                            new Booking 
                            { 
                                ResourceId = resources[0].Id, 
                                UserId = user.Id, 
                                Purpose = "Board meeting", 
                                StartTime = DateTime.SpecifyKind(thisMonth.AddDays(5).AddHours(9), DateTimeKind.Utc), 
                                EndTime = DateTime.SpecifyKind(thisMonth.AddDays(5).AddHours(11), DateTimeKind.Utc), 
                                Cancelled = false 
                            },
                            new Booking 
                            { 
                                ResourceId = resources[1].Id, 
                                UserId = user.Id, 
                                Purpose = "Equipment delivery", 
                                StartTime = DateTime.SpecifyKind(thisMonth.AddDays(8).AddHours(14), DateTimeKind.Utc), 
                                EndTime = DateTime.SpecifyKind(thisMonth.AddDays(8).AddHours(15), DateTimeKind.Utc), 
                                Cancelled = false 
                            },
                            new Booking 
                            { 
                                ResourceId = resources.Count > 2 ? resources[2].Id : resources[0].Id, 
                                UserId = user.Id, 
                                Purpose = "Workshop session", 
                                StartTime = DateTime.SpecifyKind(thisMonth.AddDays(12).AddHours(10), DateTimeKind.Utc), 
                                EndTime = DateTime.SpecifyKind(thisMonth.AddDays(12).AddHours(16), DateTimeKind.Utc), 
                                Cancelled = false 
                            },
                            new Booking 
                            { 
                                ResourceId = resources.Count > 3 ? resources[3].Id : resources[0].Id, 
                                UserId = user.Id, 
                                Purpose = "One-on-one meeting", 
                                StartTime = DateTime.SpecifyKind(thisMonth.AddDays(15).AddHours(15), DateTimeKind.Utc), 
                                EndTime = DateTime.SpecifyKind(thisMonth.AddDays(15).AddHours(16), DateTimeKind.Utc), 
                                Cancelled = false 
                            },
                            new Booking 
                            { 
                                ResourceId = resources[0].Id, 
                                UserId = user.Id, 
                                Purpose = "Monthly review meeting", 
                                StartTime = DateTime.SpecifyKind(thisMonth.AddDays(20).AddHours(9), DateTimeKind.Utc), 
                                EndTime = DateTime.SpecifyKind(thisMonth.AddDays(20).AddHours(12), DateTimeKind.Utc), 
                                Cancelled = false 
                            }
                        };

                        // Add more bookings with different users if available
                        if (users.Count > 1)
                        {
                            var user2 = users[1];
                            sampleBookings.AddRange(new[]
                            {
                                new Booking 
                                { 
                                    ResourceId = resources.Count > 4 ? resources[4].Id : resources[0].Id, 
                                    UserId = user2.Id, 
                                    Purpose = "Quarterly presentation", 
                                    StartTime = DateTime.SpecifyKind(thisMonth.AddDays(18).AddHours(14), DateTimeKind.Utc), 
                                    EndTime = DateTime.SpecifyKind(thisMonth.AddDays(18).AddHours(16), DateTimeKind.Utc), 
                                    Cancelled = false 
                                },
                                new Booking 
                                { 
                                    ResourceId = resources[1].Id, 
                                    UserId = user2.Id, 
                                    Purpose = "Client pickup", 
                                    StartTime = DateTime.SpecifyKind(today.AddDays(1).AddHours(10), DateTimeKind.Utc), 
                                    EndTime = DateTime.SpecifyKind(today.AddDays(1).AddHours(12), DateTimeKind.Utc), 
                                    Cancelled = false 
                                }
                            });
                        }

                        await _db.Bookings.AddRangeAsync(sampleBookings);
                        await _db.SaveChangesAsync();
                        
                        _logger.LogInformation($"Seeded {sampleBookings.Count} sample bookings");
                    }
                    else
                    {
                        _logger.LogWarning("No available resources found for booking seeding");
                    }
                }
                else if (!users.Any())
                {
                    _logger.LogWarning("No users found - bookings will be seeded later");
                }
                else
                {
                    _logger.LogInformation("Bookings already exist, skipping seeding");
                }

                _logger.LogInformation("Data seeding completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data seeding: {Message}", ex.Message);
                throw; // Re-throw to let Program.cs handle the failure
            }
        }

        // Keep the old Seed method for backward compatibility
        public void Seed()
        {
            SeedAsync().GetAwaiter().GetResult();
        }
    }
}}