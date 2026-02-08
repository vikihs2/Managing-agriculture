# Implementation Plan: Complete Feature Set

## Overview
This plan addresses all the requested features for the ManagingAgriculture application.

## Tasks Breakdown

### 1. ✅ Plant Tracking Edit Page Fix
- **Status**: Check and fix the Edit POST method
- **Files**: `Controllers/PlantsController.cs`, `Views/Plants/Edit.cshtml`

### 2. ✅ Crop Type Searchable Dropdown
- **Status**: Already implemented with datalist
- **Enhancement**: Verify all crop categories are present
- **Files**: `Services/CropDataService.cs`

### 3. ⏳ Growth Algorithm
- **Status**: Partially implemented, needs enhancement
- **Requirements**: Calculate growth % based on:
  - Crop type
  - Soil type (Clay, Sandy, Loamy, Silty, Peaty, Chalky)
  - Indoor/Outdoor
  - Average Temperature (-50 to 60°C)
  - Watering Frequency (0-365 days)
  - Sunlight Exposure (Full Sun, Partial Sun, Partial Shade, Full Shade)
- **Files**: `Services/CropDataService.cs`

### 4. ⏳ Dark/Light Theme with Cookie
- **Requirements**:
  - Theme switcher at the top of pages
  - Cookie persistence (survives login/logout)
  - Apply to all pages
- **Files**: `Views/Shared/_Layout.cshtml`, `wwwroot/css/site.css`, `wwwroot/js/theme.js`

### 5. ⏳ Company Calendar Features
- **Requirements**:
  - Boss/Manager/Employee calendars
  - Mark leave days
  - Track remaining days off (e.g., 20 total - 7 used = 13 left)
  - Boss can set days off and salary
  - Boss can raise salaries
  - Job requests show days off and salary
  - Track if salary is paid this month
- **Files**: `Controllers/BossController.cs`, `Views/Boss/*.cshtml`, `Models/ApplicationUser.cs`

### 6. ⏳ Role Restrictions for Companies
- **Employee**: View only (no add/edit/delete)
- **Manager**: Can add but not edit or delete
- **Boss**: Full access + can assign tasks
- **Task System**:
  - Boss assigns tasks to managers/employees
  - Tasks display on user profiles
  - "Done" button sends approval request to boss
  - Boss approves/rejects task completion
- **Files**: Multiple controllers, views

### 7. ⏳ Weather API for Bulgaria
- **Requirements**: Add weather widget to dashboard
- **API**: Use OpenWeatherMap or similar
- **Files**: `Controllers/DashboardController.cs`, `Views/Dashboard/Index.cshtml`

### 8. ⏳ Custom Error Pages
- **Requirements**: Custom 404/500 error pages
- **Example**: Handle broken links like `http://localhost:5145/Plant`
- **Files**: `Program.cs`, `Views/Shared/Error.cshtml`, create custom error pages

### 9. ⏳ Arduino Connection Status
- **Requirements**: 
  - Show "Arduino not connected" message when not connected
  - Display centered, large text
- **Files**: `Controllers/SensorsController.cs`, `Views/Sensors/Index.cshtml`

### 10. ⏳ Start Tracking Button Fix
- **Requirements**: Only redirect to Plant Tracking when logged in
- **Files**: `Views/Home/Index.cshtml`

### 11. ⏳ Database Updates
- **Requirements**: Update schema for:
  - Leave tracking (LeaveRecord table)
  - Task assignments (TaskAssignment table)
  - Salary paid status
  - Theme preference
- **Files**: Create new migration

## Priority Order
1. Fix Plant Edit page (Critical)
2. Start Tracking button authentication (Quick fix)
3. Arduino connection message (Quick fix)
4. Dark/Light theme (User experience)
5. Growth algorithm enhancement
6. Company calendar features
7. Task management system
8. Weather API integration
9. Custom error pages
10. Database migration

## Notes
- Some features are already partially implemented
- Need to create migrations for new database fields
- Test each feature after implementation
