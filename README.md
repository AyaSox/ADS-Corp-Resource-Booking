# ADS Corp Resource Booking System

## Overview
Professional ASP.NET Core MVC application for booking shared resources (meeting rooms, vehicles, equipment) with comprehensive notification system and analytics.

## Requirements
- .NET 8 SDK
- SQL Server LocalDB (installed with Visual Studio) or SQL Server Express
- Internet connection for Bootstrap Icons CDN

## Quick Setup
1. Clone repository
2. Navigate to project directory:
   ```sh
   cd ResourceBooking
   ```
3. Install dependencies:
   ```sh
   dotnet restore
   ```
4. Update `appsettings.json` connection string if needed
5. Setup database:
   ```sh
   dotnet ef database update
   ```
6. Run the application:
   ```sh
   dotnet run
   ```

Open browser: https://localhost:5001 (or the URL shown)

## Key Features

### Dashboard & Analytics
- Real-time booking overview with today's activity
- Resource utilization statistics and trends
- Popular resource insights with star ratings
- Monthly growth tracking and comparisons
- User activity leaderboards

### Resource Management
- Full CRUD operations for resources (rooms, vehicles, equipment)
- Resource availability tracking with detailed status
- **Soft Delete System**: Resources are deactivated, not deleted (preserves history)
- **Permanent Deactivation**: Mark resources as "Does Not Exist", "Added Mistakenly", or "Retired"
- **Temporary Unavailability**: Set maintenance, repairs, cleaning schedules
- Smart status badges with color coding

### Advanced Booking System
- **Conflict Detection**: Intelligent overlap prevention
- **Recurring Bookings**: Daily, weekly, monthly patterns with flexible end dates
- **Calendar Integration**: Interactive FullCalendar view with resource filtering
- **Time Validation**: Ensures logical start/end times
- **Edit & Cancel**: Full booking lifecycle management
- **Booking Export**: CSV export with filtering and search
- **ICS Calendar Files**: Download individual bookings for external calendars
- **Pagination**: Professional data handling with 15 items per page

### Dual Notification System
- **Email Notifications**: Professional HTML templates for confirmations/cancellations
- **In-App Notifications**: Real-time bell icon with red dot indicators
- **Live Updates**: 15-second polling for instant notifications
- **Toast Notifications**: Popup alerts for new activity
- **Deduplication**: Smart prevention of duplicate notifications
- **Notification Management**: Mark as read, delete, view history

### Smart Search & Filtering
- Global search across resources, bookings, and users
- Advanced filtering by date, resource type, and availability
- Real-time search suggestions
- Export filtered results to CSV

### Professional Email System
- **Welcome Emails**: Onboarding for new users
- **Booking Confirmations**: Detailed booking information
- **Cancellation Notices**: Clear cancellation communications
- **Email Testing**: Built-in test interface for SMTP verification
- **Dual Format**: HTML and plain text versions
- **Company Branding**: Professional templates with ADS Corp styling

### Modern Responsive UI
- **Compact Navbar**: Progressive layout with "More" dropdown
- **Mobile Optimized**: Full hamburger menu for mobile devices
- **Icon-First Design**: Bootstrap Icons with responsive text labels
- **Professional Styling**: ADS Corp branded interface
- **Accessibility**: Screen reader friendly with proper ARIA labels

## Database Architecture

### Core Tables
- **Resources**: Name, description, location, capacity, availability status
- **Bookings**: Resource reservations with conflict detection and recurring patterns
- **Notifications**: In-app messaging system with read status
- **AspNetUsers**: Extended Identity with full names and company info

### Key Features
- **Soft Deletes**: Bookings are cancelled, not deleted (audit trail)
- **UTC Storage**: All times stored in UTC, displayed in local time
- **Foreign Key Integrity**: Proper relationships with cascade rules
- **Automatic Seeding**: Sample data for immediate testing

## Technical Architecture

### Controllers
- **DashboardController**: Analytics and overview dashboard
- **ResourcesController**: Complete resource lifecycle management
- **BookingsController**: Booking CRUD with advanced conflict detection
- **NotificationsController**: In-app notification management
- **ReportsController**: Advanced analytics and reporting
- **EmailTestController**: Email system testing and verification
- **SearchController**: Global search functionality
- **HelpController**: User guidance and support

### Services
- **EmailService**: Professional email delivery with MailKit
- **NotificationService**: In-app notification management with deduplication
- **CalculationService**: Analytics, statistics, and utilization tracking
- **RecurringBookingService**: Complex recurring pattern generation

### Key Technologies
- **Entity Framework Core**: Advanced ORM with migrations
- **Identity Framework**: Secure user authentication and authorization
- **MailKit**: Robust email delivery system
- **FullCalendar**: Interactive calendar component
- **Bootstrap 5**: Modern responsive CSS framework
- **jQuery**: Client-side interactions and AJAX

## Demo Accounts
Pre-seeded demo users for immediate testing:
- **sipho@company.com** / **Demo123!** (Admin user)
- **thabo@company.com** / **Demo123!** (Regular user)
- **amanda@company.com** / **Demo123!** (Regular user)

## Responsive Design Breakpoints

### Desktop (≥1200px)
- Full navigation with text labels and icons
- Complete dashboard with 4-column stats
- Expanded tables with all columns visible

### Tablet (992-1199px)  
- Icon-only navigation (text labels hidden)
- Responsive dashboard layout
- Collapsible table columns

### Mobile (≤991px)
- Hamburger navigation menu
- Stacked dashboard cards
- Mobile-optimized forms and tables

## Testing Features

### Email Testing (`/EmailTest`)
- Send custom test emails
- Preview email templates
- Verify SMTP configuration  
- Test booking notification emails

### Sample Data
- 6 pre-configured resources (rooms, vehicles, equipment)
- Sample bookings with various time ranges
- Demonstration of recurring patterns
- Resource unavailability examples

## Analytics & Reporting

### Dashboard Metrics
- Total/Available/Unavailable resource counts
- Today's active bookings
- Monthly booking statistics
- Average resource utilization percentage

### Advanced Reports
- **Popular Resources**: Star ratings based on usage frequency and duration
- **User Activity**: Top users by booking count and hours
- **Utilization Tracking**: Efficiency metrics and trends
- **Monthly Comparisons**: Growth analysis and insights

## Security Features

- **User Authorization**: Users can only edit/cancel their own bookings
- **Input Validation**: Client and server-side validation
- **SQL Injection Protection**: Entity Framework parameterized queries
- **XSS Prevention**: Proper HTML encoding and validation
- **CSRF Protection**: Anti-forgery tokens on all forms

## Performance Optimizations

- **Efficient Queries**: Optimized Entity Framework queries with proper includes
- **Async Operations**: All database operations are asynchronous
- **Caching**: ViewBag caching for resource lists
- **Lazy Loading**: Strategic data loading to minimize database hits
- **Compact CSS**: Minimized stylesheet with responsive design
- **Pagination**: Efficient data handling for large datasets

## Development Tools

### Database Management
```sh
# Create new migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update

# Reset database (removes all data)
dotnet ef database drop --force
dotnet ef database update
```

### Git Workflow
```sh
git init
git add .
git commit -m "Initial commit: ADS Corp Resource Booking System"
git branch -M main
git remote add origin https://github.com/AyaSox/ADS-Corp-Resource-Booking.git
git push -u origin main
```

## Project Structure
```
ResourceBooking/
├── Controllers/           # MVC Controllers
├── Views/                # Razor Views
├── Models/               # Data Models and ViewModels
├── Services/             # Business Logic Services
├── Data/                 # Entity Framework DbContext and Seeders
├── Helpers/              # Utility Classes
├── Migrations/           # Entity Framework Migrations
├── wwwroot/              # Static Files (CSS, JS, Images)
├── Documentation/        # Project Documentation
├── README.md             # This file
├── LICENSE               # MIT License
└── .gitignore            # Git exclusions
```

## Complete Testing Checklist

### Core Functionality
- [x] Create/edit/deactivate resources
- [x] Create bookings with conflict detection
- [x] Create recurring bookings (daily/weekly/monthly)
- [x] Edit bookings with time validation
- [x] Cancel bookings with proper notifications
- [x] Search and filter across all entities
- [x] Pagination for large datasets

### Advanced Features  
- [x] Dashboard analytics and statistics
- [x] Email notifications (confirmation/cancellation)
- [x] In-app notifications with live updates
- [x] Calendar view with resource filtering
- [x] CSV export with custom filtering
- [x] ICS calendar file downloads
- [x] Professional reports and insights

### UI/UX Testing
- [x] Responsive design (desktop/tablet/mobile)
- [x] Progressive navbar (full → icons → hamburger)
- [x] Toast notifications positioning
- [x] Form validation and error handling
- [x] Professional email templates
- [x] Accessibility features

## Production Deployment

### Required Configuration
1. **Database**: Update connection string for production SQL Server
2. **Email**: Configure SMTP settings in `appsettings.json`
3. **Security**: Update JWT secret keys and Identity settings
4. **Logging**: Configure application insights or file logging

### Email Configuration Example
```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "EnableSsl": true,
    "Username": "your-email@gmail.com",
    "Password": "your-app-password",
    "FromEmail": "your-email@gmail.com",
    "FromName": "ADS Corp Resource Booking"
  }
}
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request


## Contact & Support

- **Developer**: AyaSox
- **GitHub**: [@AyaSox](https://github.com/AyaSox)
- **Project**: [ADS Corp Resource Booking System](https://github.com/AyaSox/ADS-Corp-Resource-Booking)

### Company Information (Demo)
- **Company**: ADS Corp (Advanced Digital Solutions Corporation)
- **Email**: support@adscorp.com
- **Phone**: +27 (0) 10 123-4567
- **System**: Professional Resource Management Solution

---

## Acknowledgments

- Built with .NET 8 and Entity Framework Core
- UI powered by Bootstrap 5 and Bootstrap Icons
- Calendar integration using FullCalendar
- Email functionality via MailKit
- Professional development practices and enterprise architecture

**Built with ❤️ using .NET 8, Entity Framework Core, and modern web technologies.**
